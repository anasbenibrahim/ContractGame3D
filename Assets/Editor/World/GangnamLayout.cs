using UnityEngine;

namespace Contract.Editor.World
{
    /// <summary>
    /// Shared coordinates for Gangnam interiors (local to the owning tower, front = +Z) so the
    /// district builder (geometry) and the population builder (gameplay) always agree.
    /// </summary>
    public static class GangnamLayout
    {
        public const string MiraeTower = "District_Gangnam/Buildings/MiraeTower";
        public const string HangyeolTower = "District_Gangnam/Buildings/HangyeolGroup";

        // Mirae Tower (podium 22 x 22): lobby z -5..10.7, staff office z -10.7..-5, door x 5..8.
        public const float MiraePartitionZ = -5f;
        public static readonly Vector2 MiraeOfficeDoor = new Vector2(5f, 8f);
        public static readonly Vector3 MiraeSecureTerminal = new Vector3(3f, 0.9f, -8.35f);
        public static readonly Vector3 MiraeDocuments = new Vector3(-7f, 0.92f, -8f);
        public static readonly Vector3 MiraeReception = new Vector3(0f, 0f, 3.6f);
        public static readonly Bounds MiraeLobbyVolume = new Bounds(new Vector3(0f, 2f, 2.8f), new Vector3(21f, 4f, 15.5f));
        public static readonly Bounds MiraeOfficeVolume = new Bounds(new Vector3(0f, 2f, -7.85f), new Vector3(21f, 4f, 5.5f));

        // Hangyeol Group (podium 20 x 22): lobby z -1..10.7, server room z -10.7..-1, door x 2..5.
        public const float HangyeolPartitionZ = -1f;
        public static readonly Vector2 HangyeolServerDoor = new Vector2(2f, 5f);
        public static readonly Vector3 HangyeolServerRack = new Vector3(0f, 1.1f, -3.42f);
        public static readonly Bounds HangyeolServerVolume = new Bounds(new Vector3(0f, 2f, -5.85f), new Vector3(19f, 4f, 9.5f));
        public static readonly Bounds HangyeolLobbyVolume = new Bounds(new Vector3(0f, 2f, 4.85f), new Vector3(19f, 4f, 11.5f));
    }
}
