using System;
using UnityEngine;

namespace Contract.Map
{
    public enum MapIconType
    {
        Player,
        Objective,
        MissionSubject,
        NPC,
        Guard,
        Collectible,
        Safehouse,
        ExtractionPoint,
        PointOfInterest
    }

    /// <summary>Filter categories shown as toggles on the full map.</summary>
    [Flags]
    public enum MapFilter
    {
        None = 0,
        Objectives = 1,
        MissionCharacters = 2,
        Civilians = 4,
        Security = 8,
        Collectibles = 16,
        Places = 32,
        All = Objectives | MissionCharacters | Civilians | Security | Collectibles | Places
    }

    public interface IMapService
    {
        void OpenMap();
        void CloseMap();
        bool IsMapOpen { get; }
        void RegisterMapIcon(string iconId, MapIconType type, Vector3 position);
        void UnregisterMapIcon(string iconId);
        void SetZoom(float zoom);
    }
}
