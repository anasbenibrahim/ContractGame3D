using System.Collections.Generic;
using System.Linq;
using Contract.Core;
using Contract.Data;
using Contract.Map;
using Contract.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Contract.NPCs
{
    public enum NpcType
    {
        Civilian,
        MissionSubject,
        Guard,
        Target
    }

    /// <summary>Non-lethal behaviour states.</summary>
    public enum NpcState
    {
        Idle,
        Walking,
        Dwelling,
        Talking,
        Alert,     // guard: approaching the player to warn them off
        Following  // escorted by the player
    }

    /// <summary>
    /// Crowd / guard / mission-character behaviour on the NavMesh. Decisions run on a staggered
    /// timer (not every frame) so dozens of NPCs stay cheap. All reactions are non-lethal:
    /// civilians talk and step aside, guards walk over and warn trespassers.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NpcController : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string npcId;
        [SerializeField] private NpcType npcType;
        [SerializeField] private NpcArchetype archetype = NpcArchetype.Pedestrian;

        [Header("Appearance")]
        [Tooltip("Generate a look from the archetype (crowd). Off = use 'appearance' (guards, mission characters).")]
        [SerializeField] private bool randomAppearance = true;
        [SerializeField] private CharacterAppearance appearance = new CharacterAppearance();
        [SerializeField] private Material baseMaterial;
        [SerializeField] private int appearanceSeed;

        [Header("Navigation")]
        [Tooltip("Explicit route (guards, mission characters). Empty = wander between points of interest.")]
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private Vector2 routeDwell = new Vector2(4f, 9f);
        [SerializeField] private float walkSpeed = 1.4f;
        [SerializeField] private float hurrySpeed = 3.2f;
        [SerializeField] private float maxDestinationDistance = 70f;

        [Header("Guard")]
        [SerializeField] private string guardGroup = "";
        [SerializeField] private float maxSuspicion = 100f;
        [SerializeField] private float suspicionGainPerSecond = 45f;
        [SerializeField] private float suspicionDecayRate = 15f;
        [SerializeField] private float visionRange = 14f;
        [SerializeField] private float visionAngle = 110f;

        private const float ThinkInterval = 0.25f;
        private const float VisibleDistance = 75f; // beyond this the night fog hides them anyway

        private NavMeshAgent agent;
        private NpcSpeech speech;
        private PlaceholderRig rig;
        private Transform player;
        private ThirdPersonController playerController;
        private PlayerCharacterApplier playerCharacter;

        private NpcState currentState = NpcState.Idle;
        private float suspicion;
        private float thinkTimer;
        private float stateTimer;
        private float bumpCooldown;
        private int routeIndex;
        private NpcPointOfInterest currentPoi;
        private Transform followTarget;
        private NpcState resumeState;
        private bool alerted;

        public string NpcId => npcId;
        public NpcType Type => npcType;
        public NpcArchetype Archetype => archetype;
        public NpcState CurrentState => currentState;
        public float Suspicion => suspicion;
        public float NormalizedSuspicion => suspicion / maxSuspicion;
        public NpcPointOfInterest CurrentDestination => currentPoi;
        public string GuardGroup => guardGroup;
        public NpcSpeech Speech => speech;
        public bool IsFollowing => currentState == NpcState.Following;

        // ---- setup ----------------------------------------------------------------------------

        public void SetSeed(int seed) => appearanceSeed = seed;

        /// <summary>How long to linger at each explicit route point.</summary>
        public void SetRouteDwell(Vector2 seconds) => routeDwell = seconds;

        public void Configure(string id, NpcType type, NpcArchetype kind, CharacterAppearance look, Material material, string group = "", Transform[] route = null)
        {
            npcId = id;
            npcType = type;
            archetype = kind;
            randomAppearance = look == null;
            appearance = look ?? new CharacterAppearance();
            baseMaterial = material;
            guardGroup = group;
            patrolPoints = route;

            if (begun) // pooled NPC being reused: rebuild its look and restart its routine
            {
                if (rig != null) Destroy(rig.gameObject);
                rig = null;
                begun = false;
                currentState = NpcState.Idle;
                Begin();
            }
        }

        /// <summary>Legacy entry point kept for INpcService callers.</summary>
        public void Initialize(NpcType type, Transform[] route)
        {
            npcType = type;
            patrolPoints = route;
            Begin();
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            speech = GetComponent<NpcSpeech>();
            if (speech == null) speech = gameObject.AddComponent<NpcSpeech>();
            if (GetComponent<NpcLocomotion>() == null) gameObject.AddComponent<NpcLocomotion>();
        }

        private void Start() => Begin();

        private bool begun;

        private void Begin()
        {
            if (begun) return;
            begun = true;

            if (string.IsNullOrEmpty(npcId)) npcId = $"npc_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}";
            speech.SetSpeakerId(npcId);
            BuildVisual();

            agent.speed = walkSpeed * (archetype == NpcArchetype.Tourist ? 0.8f : 1f) * Random.Range(0.9f, 1.15f);
            agent.angularSpeed = 360f;
            agent.acceleration = 8f;
            agent.stoppingDistance = 0.3f;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            agent.avoidancePriority = Random.Range(30, 70);

            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                player = p.transform;
                playerController = p.GetComponent<ThirdPersonController>();
                playerCharacter = p.GetComponent<PlayerCharacterApplier>();
            }

            thinkTimer = Random.Range(0f, ThinkInterval); // stagger decisions across the crowd
            ChooseNextDestination();
        }

        private void BuildVisual()
        {
            if (rig != null) return;
            Transform visual = transform.Find("Visual");
            if (visual == null)
            {
                visual = new GameObject("Visual").transform;
                visual.SetParent(transform, false);
            }

            CharacterAppearance look = randomAppearance
                ? NpcAppearanceFactory.Create(archetype, new System.Random(appearanceSeed != 0 ? appearanceSeed : Random.Range(1, int.MaxValue)))
                : appearance;

            Material material = baseMaterial != null ? baseMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            rig = PlaceholderHumanoidBuilder.Build(visual, look, material, "Body", PlaceholderHumanoidBuilder.Options.Crowd);
            foreach (Transform t in rig.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;
        }

        // ---- per-frame --------------------------------------------------------------------

        private float attachRetry;

        /// <summary>
        /// In a player build the scene's NavMesh can register after the NPCs enable ("Failed to create
        /// agent because there is no valid NavMesh", seen on the Galaxy A22): retry attaching instead
        /// of standing frozen (a frozen guard would also never see the player).
        /// </summary>
        private bool EnsureOnNavMesh()
        {
            if (agent.isOnNavMesh) return true;
            if ((attachRetry -= Time.deltaTime) > 0f) return false;
            attachRetry = 1f;
            if (!agent.enabled || !NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas)) return false;
            if (!agent.Warp(hit.position)) return false;
            ChooseNextDestination();
            return agent.isOnNavMesh;
        }

        private void Update()
        {
            if (!EnsureOnNavMesh()) return;
            float dt = Time.deltaTime;
            bumpCooldown -= dt;
            stateTimer -= dt;

            if (npcType == NpcType.Guard) UpdateVision(dt);
            if (currentState == NpcState.Talking || currentState == NpcState.Dwelling) FaceTarget();

            thinkTimer -= dt;
            if (thinkTimer > 0f) return;
            thinkTimer = ThinkInterval;
            Think();
        }

        private void Think()
        {
            UpdateVisibility();
            CheckBump();

            switch (currentState)
            {
                case NpcState.Idle:
                    ChooseNextDestination();
                    break;
                case NpcState.Walking:
                    if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f) Arrive();
                    break;
                case NpcState.Dwelling:
                    if (stateTimer <= 0f) ChooseNextDestination();
                    break;
                case NpcState.Talking:
                    if (stateTimer <= 0f) Resume();
                    break;
                case NpcState.Alert:
                    UpdateAlert();
                    break;
                case NpcState.Following:
                    UpdateFollow();
                    break;
            }
        }

        private void UpdateVisibility()
        {
            if (rig == null) return;
            Camera cam = Camera.main;
            bool visible = cam == null || (cam.transform.position - transform.position).sqrMagnitude < VisibleDistance * VisibleDistance;
            if (rig.gameObject.activeSelf != visible) rig.gameObject.SetActive(visible);
        }

        // ---- wandering ----------------------------------------------------------------------

        private void ChooseNextDestination()
        {
            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                Transform next = patrolPoints[routeIndex % patrolPoints.Length];
                routeIndex++;
                currentPoi = next.GetComponent<NpcPointOfInterest>();
                GoTo(next.position);
                return;
            }

            NpcPointOfInterest poi = PickPoi();
            if (poi == null)
            {
                currentState = NpcState.Idle;
                return;
            }

            currentPoi = poi;
            Vector2 jitter = Random.insideUnitCircle * poi.Radius;
            GoTo(poi.transform.position + new Vector3(jitter.x, 0f, jitter.y));
        }

        private NpcPointOfInterest PickPoi()
        {
            Vector3 here = transform.position;
            float maxSqr = maxDestinationDistance * maxDestinationDistance;
            List<NpcPointOfInterest> candidates = NpcPointOfInterest.All
                .Where(p => p != currentPoi && Weight(p) > 0f && (p.transform.position - here).sqrMagnitude < maxSqr)
                .ToList();
            if (candidates.Count == 0) return null;

            float total = candidates.Sum(Weight);
            float roll = Random.value * total;
            foreach (NpcPointOfInterest p in candidates)
            {
                roll -= Weight(p);
                if (roll <= 0f) return p;
            }

            return candidates[candidates.Count - 1];
        }

        private float Weight(NpcPointOfInterest poi)
        {
            if (npcType == NpcType.Guard) return poi.Category == PoiCategory.Patrol && poi.Group == guardGroup ? 1f : 0f;
            if (poi.Category == PoiCategory.Patrol) return 0f;

            return (archetype, poi.Category) switch
            {
                (NpcArchetype.Shopper, PoiCategory.Shop) => 4f,
                (NpcArchetype.Diner, PoiCategory.Food) => 4f,
                (NpcArchetype.Tourist, PoiCategory.Viewpoint) => 4f,
                (NpcArchetype.Tourist, PoiCategory.Shop) => 1.5f,
                (NpcArchetype.Pedestrian, PoiCategory.Transit) => 3f,
                (NpcArchetype.Pedestrian, PoiCategory.Lobby) => 2f,
                (_, PoiCategory.Crossing) => 1.5f,
                (_, PoiCategory.Bench) => 1f,
                _ => 0.5f
            };
        }

        private void GoTo(Vector3 target)
        {
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, NavMesh.AllAreas) && agent.SetDestination(hit.position))
            {
                agent.isStopped = false;
                currentState = NpcState.Walking;
            }
            else
            {
                currentState = NpcState.Idle; // retry next think
            }
        }

        private void Arrive()
        {
            currentState = NpcState.Dwelling;
            stateTimer = currentPoi != null ? currentPoi.RandomDwell() : Random.Range(routeDwell.x, routeDwell.y);

            if (rig == null) return;
            if (archetype == NpcArchetype.Tourist && currentPoi != null && currentPoi.Category == PoiCategory.Viewpoint)
                rig.PlayGesture(RigGesture.Photo, 2.5f); // tourists photograph landmarks
            else if (currentPoi != null && (currentPoi.Category == PoiCategory.Food || currentPoi.Category == PoiCategory.Shop) && Random.value < 0.4f)
                rig.PlayGesture(RigGesture.Talk, 3f);
        }

        private void FaceTarget()
        {
            Vector3 dir;
            if (currentState == NpcState.Talking && player != null) dir = player.position - transform.position;
            else if (currentPoi != null) dir = currentPoi.transform.forward;
            else return;

            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 240f * Time.deltaTime);
        }

        // ---- reactions ------------------------------------------------------------------------

        /// <summary>Stops, turns to the player and says a line (Talk interaction).</summary>
        public void Talk()
        {
            if (currentState == NpcState.Following) return;
            if (currentState != NpcState.Talking) resumeState = currentState == NpcState.Alert ? NpcState.Alert : NpcState.Idle;
            currentState = NpcState.Talking;
            stateTimer = 3.5f;
            if (agent.isOnNavMesh) agent.isStopped = true;
            speech.Say(npcType == NpcType.Guard ? SpeechContext.GuardGreeting : archetype == NpcArchetype.Tourist ? SpeechContext.Tourist : SpeechContext.Greeting);
            rig?.PlayGesture(RigGesture.Talk, 3f);
        }

        private void Resume()
        {
            if (agent.isOnNavMesh) agent.isStopped = false;
            currentState = resumeState == NpcState.Alert ? NpcState.Alert : NpcState.Idle;
        }

        private void CheckBump()
        {
            if (player == null || bumpCooldown > 0f || currentState == NpcState.Talking) return;
            if ((player.position - transform.position).sqrMagnitude > 1.1f * 1.1f) return;
            if (playerController == null || playerController.PlanarSpeed < 3f) return;

            bumpCooldown = 6f;
            speech.Say(SpeechContext.Bumped);
            // Step aside, away from the player.
            Vector3 away = (transform.position - player.position);
            away.y = 0f;
            GoTo(transform.position + away.normalized * 1.5f + Vector3.Cross(Vector3.up, away.normalized) * 0.8f);
        }

        // ---- escort ----------------------------------------------------------------------------

        public void Follow(Transform target)
        {
            followTarget = target;
            currentState = NpcState.Following;
            agent.isStopped = false;
            agent.stoppingDistance = 1.8f;
            agent.speed = Mathf.Max(agent.speed, 3.5f);
        }

        public void StopFollowing()
        {
            followTarget = null;
            agent.stoppingDistance = 0.3f;
            agent.speed = walkSpeed;
            currentState = NpcState.Idle;
        }

        private void UpdateFollow()
        {
            if (followTarget == null)
            {
                StopFollowing();
                return;
            }

            agent.SetDestination(followTarget.position);
            float distance = Vector3.Distance(transform.position, followTarget.position);
            agent.speed = distance > 6f ? hurrySpeed + 1f : Mathf.Max(walkSpeed, 3.5f); // keep up when sprinted away from
        }

        // ---- guards: non-lethal suspicion -------------------------------------------------------

        private void UpdateVision(float dt)
        {
            bool seen = CanSeePlayerTrespassing(out float distance);
            if (seen)
            {
                float stealth = playerCharacter != null ? playerCharacter.StealthMultiplier : 1f;
                bool crouched = playerController != null && playerController.IsCrouching;
                float crouch = crouched ? 0.5f * PlayerUpgrades.Reduction(UpgradeEffect.CrouchStealth) : 1f;
                float upgrades = PlayerUpgrades.Reduction(UpgradeEffect.DetectionResistance);
                float proximity = Mathf.Lerp(1.5f, 0.5f, distance / visionRange);
                AddSuspicion(suspicionGainPerSecond * proximity * crouch * upgrades / stealth * dt);
            }
            else if (suspicion > 0f)
            {
                suspicion = Mathf.Max(0f, suspicion - suspicionDecayRate * PlayerUpgrades.Multiplier(UpgradeEffect.SuspicionCooldown) * dt);
                GameEvents.RaiseSuspicionChanged(npcId, NormalizedSuspicion);
                if (suspicion <= 0f && currentState == NpcState.Alert) StandDown();
            }
        }

        private bool CanSeePlayerTrespassing(out float distance)
        {
            distance = float.MaxValue;
            if (player == null || !RestrictedZone.PlayerInside(guardGroup)) return false;

            Vector3 eye = transform.position + Vector3.up * 1.6f;
            Vector3 target = player.position + Vector3.up * 1.2f;
            Vector3 toPlayer = target - eye;
            distance = toPlayer.magnitude;
            if (distance > visionRange) return false;
            if (Vector3.Angle(transform.forward, new Vector3(toPlayer.x, 0f, toPlayer.z)) > visionAngle * 0.5f && distance > 2.5f) return false;

            foreach (RaycastHit hit in Physics.RaycastAll(eye, toPlayer.normalized, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(player)) continue;
                return false; // walls, desks, racks block sight
            }

            return true;
        }

        public void AddSuspicion(float amount)
        {
            suspicion = Mathf.Clamp(suspicion + amount, 0f, maxSuspicion);
            GameEvents.RaiseSuspicionChanged(npcId, NormalizedSuspicion);
            if (suspicion >= maxSuspicion && !alerted) Alert();
        }

        public void SetSuspicion(float level)
        {
            suspicion = Mathf.Clamp(level * maxSuspicion, 0f, maxSuspicion);
        }

        /// <summary>The guard spotted the player: walk over and ask them to leave (no violence).</summary>
        public void Alert()
        {
            SetMapAlert(true);
            alerted = true;
            currentState = NpcState.Alert;
            agent.isStopped = false;
            agent.speed = hurrySpeed;
            agent.stoppingDistance = 1.8f;
            speech.Say(SpeechContext.GuardWarning);
            GameEvents.RaiseNpcAlerted(npcId);
        }

        private void UpdateAlert()
        {
            if (player == null) return;
            agent.SetDestination(player.position);
            if (!RestrictedZone.PlayerInside(guardGroup) && suspicion <= 0f) StandDown();
        }

        private void SetMapAlert(bool on)
        {
            MapIcon icon = GetComponent<MapIcon>();
            if (icon != null) icon.Highlighted = on; // alerted guards turn red on the map
        }

        private void StandDown()
        {
            SetMapAlert(false);
            alerted = false;
            agent.speed = walkSpeed;
            agent.stoppingDistance = 0.3f;
            currentState = NpcState.Idle;
        }
    }
}
