using System.Collections.Generic;
using Contract.Core;
using Contract.Data;
using UnityEngine;

namespace Contract.Map
{
    /// <summary>
    /// Anything shown on the minimap and the full map. Both views read this one registry, so they
    /// always agree. Positions are read live (moving NPCs update every frame); objective icons flip
    /// to a "completed" indicator when the matching objective signal is raised, and stay on the
    /// map even if their object disappears (e.g. a collected pickup).
    /// </summary>
    public sealed class MapIcon : MonoBehaviour
    {
        private static readonly List<MapIcon> registry = new List<MapIcon>();
        public static IReadOnlyList<MapIcon> All => registry;

        [Header("Identity")]
        [SerializeField] private string iconId;
        [SerializeField] private MapIconType iconType = MapIconType.PointOfInterest;
        [SerializeField] private string label;
        [Tooltip("Mission this icon belongs to (for the 'active mission only' filter).")]
        [SerializeField] private string missionId;

        [Header("Objective tracking")]
        [Tooltip("Objective id that completes this icon (e.g. secure_terminal). Empty = never completes.")]
        [SerializeField] private string trackId;

        [Header("Visibility")]
        [SerializeField] private bool showOnMinimap = true;
        [SerializeField] private bool showOnFullMap = true;
        [Tooltip("Keep on the minimap rim when out of range (objectives, extraction).")]
        [SerializeField] private bool clampToMinimapEdge;

        private Vector3 lastPosition;

        public string IconId => string.IsNullOrEmpty(iconId) ? name : iconId;
        public MapIconType Type => iconType;
        public string Label => string.IsNullOrEmpty(label) ? name : label;
        public string MissionId => missionId;
        public string TrackId => trackId;
        public bool ShowOnMinimap => showOnMinimap;
        public bool ShowOnFullMap => showOnFullMap;
        public bool ClampToMinimapEdge => clampToMinimapEdge;
        public bool Completed { get; private set; }

        /// <summary>Identified mission character / alerted guard: drawn emphasised.</summary>
        public bool Highlighted { get; set; }

        /// <summary>Live position, or the last known one once the object is hidden (completed pickups).</summary>
        public Vector3 Position => this != null && isActiveAndEnabled ? (lastPosition = transform.position) : lastPosition;

        public MapFilter Category => iconType switch
        {
            MapIconType.Objective or MapIconType.ExtractionPoint => MapFilter.Objectives,
            MapIconType.MissionSubject => MapFilter.MissionCharacters,
            MapIconType.NPC => MapFilter.Civilians,
            MapIconType.Guard => MapFilter.Security,
            MapIconType.Collectible => MapFilter.Collectibles,
            _ => MapFilter.Places
        };

        public void Configure(string id, MapIconType type, string displayLabel, string trackObjectiveId = null, string mission = null, bool clampToEdge = false)
        {
            iconId = id;
            iconType = type;
            label = displayLabel;
            trackId = trackObjectiveId;
            missionId = mission;
            clampToMinimapEdge = clampToEdge;
        }

        public void SetLabel(string displayLabel) => label = displayLabel;

        public void MarkCompleted() => Completed = true;

        private void OnEnable()
        {
            lastPosition = transform.position;
            if (!registry.Contains(this)) registry.Add(this);
            GameEvents.ObjectiveSignal += OnObjectiveSignal;
            GameEvents.MissionStarted += OnMissionStarted;
        }

        private void OnMissionStarted(string startedMissionId)
        {
            if (!string.IsNullOrEmpty(missionId) && startedMissionId == missionId) Completed = false; // retry
        }

        private void OnDisable()
        {
            lastPosition = transform.position;
            GameEvents.ObjectiveSignal -= OnObjectiveSignal;
            GameEvents.MissionStarted -= OnMissionStarted;
            if (!Completed) registry.Remove(this); // completed objectives stay visible as "done"
        }

        private void OnDestroy() => registry.Remove(this);

        private void OnObjectiveSignal(ObjectiveType type, string id)
        {
            if (!string.IsNullOrEmpty(trackId) && id == trackId) Completed = true;
        }
    }
}
