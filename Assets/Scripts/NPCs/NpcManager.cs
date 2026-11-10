using System.Collections.Generic;
using System.Linq;
using Contract.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Contract.NPCs
{
    /// <summary>
    /// Scene-owned NPC service: spawns a pooled civilian crowd on the NavMesh around points of
    /// interest, and registers scene-placed guards and mission characters.
    /// </summary>
    public sealed class NpcManager : MonoBehaviour, INpcService
    {
        public static NpcManager Instance { get; private set; }

        [Header("Prefabs")]
        [SerializeField] private GameObject civilianNpcPrefab;
        [SerializeField] private GameObject missionNpcPrefab; // legacy field, mission characters are scene-placed
        [SerializeField] private Material npcBaseMaterial;

        [Header("Crowd")]
        [SerializeField] private int maxCivilians = 24;
        [SerializeField] private int seed = 1207;
        [Tooltip("Pedestrian, Shopper, Diner, Tourist weights")]
        [SerializeField] private Vector4 archetypeMix = new Vector4(0.4f, 0.2f, 0.2f, 0.2f);

        private readonly Dictionary<string, NpcController> npcs = new Dictionary<string, NpcController>();
        private readonly List<NpcController> civilians = new List<NpcController>();
        private readonly Stack<NpcController> pool = new Stack<NpcController>();

        public IReadOnlyList<NpcController> Civilians => civilians;
        public int MaxCivilians => maxCivilians;
        public int CivilianCount => civilians.Count;

        private void Awake()
        {
            // Scene-owned service: every gameplay scene provides its own instance.
            Instance = this;
            GameManager.Instance?.RegisterService<INpcService>(this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                GameManager.Instance?.Services.Unregister<INpcService>();
            }
        }

        private void Start()
        {
            SpawnMissionNPCs();
            SpawnCivilians();
        }

        // ---- INpcService --------------------------------------------------------------------

        /// <summary>Fills the crowd up to <see cref="maxCivilians"/> (reusing pooled NPCs).</summary>
        public void SpawnCivilians()
        {
            if (civilianNpcPrefab == null)
            {
                Debug.LogWarning("[NpcManager] No civilian prefab assigned.", this);
                return;
            }

            List<NpcPointOfInterest> spots = NpcPointOfInterest.All.Where(p => p.Category != PoiCategory.Patrol).ToList();
            if (spots.Count == 0) return;

            var rng = new System.Random(seed);
            int budget = PerformanceBudget.MaxCivilians > 0 ? System.Math.Min(maxCivilians, PerformanceBudget.MaxCivilians) : maxCivilians;
            while (civilians.Count < budget)
            {
                NpcPointOfInterest spot = spots[rng.Next(spots.Count)];
                Vector3 around = spot.transform.position + new Vector3((float)rng.NextDouble() * 4f - 2f, 0f, (float)rng.NextDouble() * 4f - 2f);
                if (!NavMesh.SamplePosition(around, out NavMeshHit hit, 4f, NavMesh.AllAreas)) continue;

                Quaternion facing = Quaternion.Euler(0f, rng.Next(360), 0f);
                NpcController npc;
                if (pool.Count > 0)
                {
                    npc = pool.Pop();
                    npc.gameObject.SetActive(true);
                    npc.GetComponent<NavMeshAgent>().Warp(hit.position);
                    npc.transform.rotation = facing;
                }
                else
                {
                    npc = Instantiate(civilianNpcPrefab, hit.position, facing, transform).GetComponent<NpcController>(); // spawn on the NavMesh
                }

                int index = civilians.Count;
                npc.name = $"Civilian_{index:00}";
                npc.SetSeed(seed * 31 + index);
                npc.Configure($"civilian_{index:00}", NpcType.Civilian, PickArchetype(rng), null, npcBaseMaterial);
                civilians.Add(npc);
                RegisterNPC(npc);
            }
        }

        /// <summary>Registers guards and mission characters already placed in the scene.</summary>
        public void SpawnMissionNPCs()
        {
            foreach (NpcController npc in FindObjectsByType<NpcController>(FindObjectsSortMode.None))
            {
                if (npc.Type != NpcType.Civilian) RegisterNPC(npc);
            }
        }

        private float budgetCheck;

        /// <summary>A lower graphics preset chosen mid-game shrinks the crowd (farthest from the camera first).</summary>
        private void Update()
        {
            if ((budgetCheck -= Time.unscaledDeltaTime) > 0f) return;
            budgetCheck = 2f;
            TrimToBudget();
        }

        public void TrimToBudget()
        {
            int budget = PerformanceBudget.MaxCivilians;
            if (budget <= 0 || civilians.Count <= budget) return;
            Vector3 eye = UnityEngine.Camera.main != null ? UnityEngine.Camera.main.transform.position : Vector3.zero;
            foreach (NpcController npc in civilians.OrderByDescending(c => c != null ? (c.transform.position - eye).sqrMagnitude : float.MaxValue).Take(civilians.Count - budget).ToList())
                Despawn(npc);
        }

        public void Despawn(NpcController npc)
        {
            if (npc == null || !civilians.Remove(npc)) return;
            npcs.Remove(npc.NpcId);
            npc.gameObject.SetActive(false);
            pool.Push(npc);
        }

        /// <summary>Returns every civilian to the pool (cutscenes, tests that need an empty street).</summary>
        public void DespawnAllCivilians()
        {
            foreach (NpcController npc in civilians.ToList()) Despawn(npc);
        }

        private NpcArchetype PickArchetype(System.Random rng)
        {
            float total = archetypeMix.x + archetypeMix.y + archetypeMix.z + archetypeMix.w;
            float roll = (float)rng.NextDouble() * total;
            if ((roll -= archetypeMix.x) < 0f) return NpcArchetype.Pedestrian;
            if ((roll -= archetypeMix.y) < 0f) return NpcArchetype.Shopper;
            if ((roll -= archetypeMix.z) < 0f) return NpcArchetype.Diner;
            return NpcArchetype.Tourist;
        }

        public void RegisterNPC(NpcController npc)
        {
            if (npc != null && !string.IsNullOrEmpty(npc.NpcId)) npcs[npc.NpcId] = npc;
        }

        public void UpdateSuspicion(string npcId, float delta)
        {
            if (npcs.TryGetValue(npcId, out NpcController npc)) npc.AddSuspicion(delta);
        }

        public void SetSuspicion(string npcId, float level)
        {
            if (npcs.TryGetValue(npcId, out NpcController npc)) npc.SetSuspicion(level);
        }

        public float GetSuspicion(string npcId) => npcs.TryGetValue(npcId, out NpcController npc) ? npc.Suspicion : 0f;

        public void AlertNPC(string npcId)
        {
            if (npcs.TryGetValue(npcId, out NpcController npc)) npc.Alert(); // Alert raises NpcAlerted itself
        }

        public NpcController GetNPC(string npcId)
        {
            npcs.TryGetValue(npcId, out NpcController npc);
            return npc;
        }

        public IEnumerable<NpcController> GetAllNPCs() => npcs.Values;
    }
}
