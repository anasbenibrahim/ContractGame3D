using System.Linq;
using Contract.Core;
using Contract.Data;
using Contract.Interaction;
using Contract.Map;
using Contract.Missions;
using TMPro;
using UnityEngine;

namespace Contract.NPCs
{
    public enum SubjectAction
    {
        Identify,
        Photograph,
        Escort,
        StopEscort,
        Talk
    }

    /// <summary>
    /// A mission character: unique id, name, description, region, mission status and reward, with
    /// non-lethal objective interactions (identify, photograph, escort, talk). One contextual
    /// action at a time so the prompt always tells the player what pressing E will do.
    /// </summary>
    [RequireComponent(typeof(NpcController))]
    public sealed class MissionSubject : InteractableBase
    {
        [Header("Identity")]
        [Tooltip("Matches MissionData.subjectId")]
        [SerializeField] private string subjectId;
        [Tooltip("Id used by this character's objectives (identify / photograph / escort)")]
        [SerializeField] private string targetId;
        [SerializeField] private string nameKo;
        [SerializeField] private string nameEn;
        [SerializeField, TextArea] private string description;
        [SerializeField] private string regionId;

        [Header("Objective capabilities")]
        [SerializeField] private bool identifiable = true;
        [SerializeField] private bool photographable;
        [SerializeField] private bool escortable;
        [Tooltip("If set, photographs are only possible while meeting this NPC (e.g. a handoff).")]
        [SerializeField] private NpcController meetingPartner;
        [SerializeField] private float meetingDistance = 3.5f;
        [SerializeField] private float escortDeliveryRadius = 6f;

        [Header("Dialogue")]
        [SerializeField] private string lineKo = "...";
        [SerializeField] private string lineEn = "...";

        private NpcController npc;
        private TextMeshPro nameLabel;
        private ExtractionInteractable[] extractions;

        public string SubjectId => subjectId;
        public string TargetId => targetId;
        public string DisplayName => $"{nameKo} · {nameEn}";
        public string SubjectDescription => description;
        public string RegionId => regionId;
        public bool Identified { get; private set; }
        public bool Photographed { get; private set; }
        public bool EscortDelivered { get; private set; }
        public NpcController Npc => npc;

        /// <summary>The mission this character belongs to (by subjectId).</summary>
        public MissionData Mission => GameDatabase.Load()?.Missions.FirstOrDefault(m => m != null && m.subjectId == subjectId);

        public MissionState Status
        {
            get
            {
                MissionData mission = Mission;
                return mission != null && MissionManager.Instance != null ? MissionManager.Instance.GetMissionState(mission.missionId) : MissionState.NotStarted;
            }
        }

        public int Reward => Mission?.rewardMoney ?? 0;

        public bool InExchange => meetingPartner != null
                                  && Vector3.Distance(transform.position, meetingPartner.transform.position) <= meetingDistance;

        public void Configure(string subject, string target, string ko, string en, string desc, string region,
            bool canIdentify, bool canPhotograph, bool canEscort, NpcController partner, string sayKo, string sayEn)
        {
            subjectId = subject; targetId = target; nameKo = ko; nameEn = en; description = desc; regionId = region;
            identifiable = canIdentify; photographable = canPhotograph; escortable = canEscort; meetingPartner = partner;
            lineKo = sayKo; lineEn = sayEn;
        }

        private void Awake() => npc = GetComponent<NpcController>();

        private void OnEnable() => GameEvents.MissionStarted += OnMissionStarted;

        private void OnDisable() => GameEvents.MissionStarted -= OnMissionStarted;

        /// <summary>Starting (or retrying) this character's contract resets its objective state.</summary>
        private void OnMissionStarted(string missionId)
        {
            if (Mission == null || Mission.missionId != missionId) return;
            Identified = Photographed = EscortDelivered = false;
            if (nameLabel != null) Destroy(nameLabel.gameObject);
            nameLabel = null;
            MapIcon icon = GetComponent<MapIcon>();
            if (icon != null)
            {
                icon.Highlighted = false;
                icon.SetLabel("미확인 인물 · Unidentified person");
            }
        }

        private void Start() => extractions = FindObjectsByType<ExtractionInteractable>(FindObjectsSortMode.None);

        /// <summary>Objective actions only while this character's contract is active (or if it has none).</summary>
        public bool ContractActive
        {
            get
            {
                MissionData mission = Mission;
                return mission == null || GameManager.Instance?.Session?.activeMissionId == mission.missionId;
            }
        }

        public SubjectAction CurrentAction
        {
            get
            {
                if (!ContractActive) return SubjectAction.Talk;
                if (identifiable && !Identified) return SubjectAction.Identify;
                if (photographable && !Photographed && InExchange) return SubjectAction.Photograph;
                if (escortable && !EscortDelivered) return npc.IsFollowing ? SubjectAction.StopEscort : SubjectAction.Escort;
                return SubjectAction.Talk;
            }
        }

        public override InteractionGesture Gesture => CurrentAction == SubjectAction.Photograph ? InteractionGesture.Photo : InteractionGesture.Talk;

        public override string Description => Identified
            ? $"{DisplayName} — {description}"
            : "신원 미확인 · Unidentified — compare with the dossier";

        public override bool CanInteract(GameObject interactor)
        {
            if (CurrentAction == SubjectAction.Photograph) return PhotographTarget.HasLineOfSight(interactor.transform, transform);
            return npc.CurrentState != NpcState.Talking || npc.IsFollowing;
        }

        public override string GetPrompt(GameObject interactor) => CurrentAction switch
        {
            SubjectAction.Identify => "[E] 신원 확인 · Identify",
            SubjectAction.Photograph => "[E] 사진 촬영 · Photograph the exchange",
            SubjectAction.Escort => $"[E] 동행 요청 · Ask {nameEn} to follow",
            SubjectAction.StopEscort => $"[E] 대기 요청 · Ask {nameEn} to wait",
            _ => Identified ? $"[E] 말 걸기 · Talk to {nameEn}" : "[E] 말 걸기 · Talk"
        };

        public override void Interact(GameObject interactor)
        {
            switch (CurrentAction)
            {
                case SubjectAction.Identify:
                    Identified = true;
                    ShowNameLabel();
                    MapIcon icon = GetComponent<MapIcon>();
                    if (icon != null)
                    {
                        icon.Highlighted = true;            // identified: solid marker with the real name
                        icon.SetLabel(DisplayName);
                    }
                    GameEvents.RaiseInteractionPerformed($"identify:{targetId}");
                    GameEvents.RaiseObjectiveSignal(ObjectiveType.IdentifySubject, targetId);
                    break;
                case SubjectAction.Photograph:
                    if (!PhotographTarget.HasLineOfSight(interactor.transform, transform)) return;
                    Photographed = true;
                    PhotographTarget.Snap(targetId);
                    break;
                case SubjectAction.Escort:
                    npc.Follow(interactor.transform);
                    npc.Speech.Say("네, 따라갈게요.", "Okay, I'll follow you.");
                    break;
                case SubjectAction.StopEscort:
                    npc.StopFollowing();
                    npc.Speech.Say("여기서 기다릴게요.", "I'll wait here.");
                    break;
                default:
                    npc.Talk();
                    npc.Speech.Say(lineKo, lineEn);
                    break;
            }
        }

        private void Update()
        {
            if (!npc.IsFollowing || extractions == null) return;
            foreach (ExtractionInteractable extraction in extractions)
            {
                if (extraction == null) continue;
                if (Vector3.Distance(transform.position, extraction.transform.position) > escortDeliveryRadius) continue;

                EscortDelivered = true;
                npc.StopFollowing();
                npc.Speech.Say("고마워요. 이제 안전해요.", "Thank you. I'm safe now.");
                GameEvents.RaiseObjectiveSignal(ObjectiveType.EscortSubject, targetId);
                return;
            }
        }

        private void ShowNameLabel()
        {
            if (nameLabel != null) return;
            GameObject go = new GameObject("NameLabel", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 2.05f;
            nameLabel = go.AddComponent<TextMeshPro>();
            nameLabel.text = $"<color=#FF4D5A>■</color> {DisplayName}";
            nameLabel.fontSize = 1.6f;
            nameLabel.alignment = TextAlignmentOptions.Center;
            nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            nameLabel.rectTransform.sizeDelta = new Vector2(5f, 0.6f);
        }

        private void LateUpdate()
        {
            if (nameLabel == null || Camera.main == null) return;
            nameLabel.transform.rotation = Quaternion.LookRotation(nameLabel.transform.position - Camera.main.transform.position);
        }
    }
}
