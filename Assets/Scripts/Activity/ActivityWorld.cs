using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Contract.Contracts;
using Contract.Core;
using Contract.Data;
using UnityEngine;

namespace Contract.Activity
{
    /// <summary>
    /// Real-world places during a walking session: checkpoints (designer ones at public places and
    /// spots the player marks), exploration of ~100 m areas, and the collectibles they reveal.
    /// Works on validated fixes only. Area visits are stored as salted hashes, so a save file
    /// never reveals where the player went; marked checkpoints keep their position because the
    /// feature needs it, and the player can delete them.
    /// </summary>
    public static class ActivityWorld
    {
        public static event Action<string> CheckpointReached;      // checkpoint id
        public static event Action<string> CollectibleDiscovered;  // item id
        public static event Action<string> CollectibleCollected;   // item id
        public static event Action<int> AreaExplored;              // total areas

        private const float StrictAccuracy = 15f; // checkpoints need a good fix (or one no worse than their radius)

        private static string insideId;
        private static int insideCount;

        private static ActivityRewardsConfig Config => ActivityRewardsConfig.Load();

        /// <summary>Called by the activity controller for every fix the distance filter accepted.</summary>
        public static void OnValidatedFix(GameSession session, GeoFix fix)
        {
            if (session == null || fix.HorizontalAccuracy <= 0f || fix.HorizontalAccuracy > Config.placeAccuracy) return;
            Explore(session, fix);
            CheckCheckpoints(session, fix);
        }

        // ---- exploration ---------------------------------------------------------------------------

        public static string AreaKey(GameSession session, double latitude, double longitude)
        {
            if (string.IsNullOrEmpty(session.explorationSalt)) session.explorationSalt = Guid.NewGuid().ToString("N");
            double size = Math.Max(10.0, Config.areaSize);
            long cy = (long)Math.Floor(latitude * 111320.0 / size);
            double centreLat = (cy + 0.5) * size / 111320.0;
            long cx = (long)Math.Floor(longitude * 111320.0 * Math.Cos(centreLat * Math.PI / 180.0) / size);
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes($"{session.explorationSalt}:{cx}:{cy}"));
            return BitConverter.ToString(hash, 0, 8).Replace("-", "");
        }

        private static void Explore(GameSession session, GeoFix fix)
        {
            string key = AreaKey(session, fix.Latitude, fix.Longitude);
            if (session.exploredAreas.Contains(key)) return;
            session.exploredAreas.Add(key);
            int count = session.exploredAreas.Count;
            AreaExplored?.Invoke(count);
            // The starting area is free; every N further new areas reveal a collectible.
            int perDiscovery = Math.Max(1, Config.areasPerDiscovery);
            if (count > 1 && (count - 1) % perDiscovery == 0) Discover(session);
        }

        // ---- collectibles ------------------------------------------------------------------------

        /// <summary>Reveals the next collectible not yet owned or pending. Returns its id, or null if all are found.</summary>
        public static string Discover(GameSession session)
        {
            ItemData next = Config.collectibles.FirstOrDefault(c => c != null && !session.inventoryItemIds.Contains(c.itemId) && !session.pendingCollectibleIds.Contains(c.itemId));
            if (next == null) return null;
            session.pendingCollectibleIds.Add(next.itemId);
            Notify(session, "발견!", $"Discovered: {next.displayName} — collect it from the activity screen");
            CollectibleDiscovered?.Invoke(next.itemId);
            return next.itemId;
        }

        /// <summary>Moves a discovered collectible into the inventory and pays its reward (once).</summary>
        public static bool Collect(GameSession session, string itemId)
        {
            if (session == null || !session.pendingCollectibleIds.Remove(itemId)) return false;
            if (!session.AddItem(itemId)) return false; // already owned: no second reward
            ItemData item = Config.collectibles.FirstOrDefault(c => c != null && c.itemId == itemId);
            string label = $"수집품 · {item?.displayName ?? itemId}";
            if (Config.collectibleMoney > 0) Pay(session, Config.collectibleMoney, label);
            Experience.Add(Config.collectibleXp, label);
            GameEvents.RaiseItemCollected(itemId);
            GameEvents.RaiseObjectiveSignal(ObjectiveType.FindCollectible, itemId);
            CollectibleCollected?.Invoke(itemId);
            Save();
            return true;
        }

        // ---- checkpoints -------------------------------------------------------------------------

        /// <summary>Marks the player's current position as a checkpoint to come back to later.</summary>
        public static PlayerCheckpoint PlaceCheckpoint(GameSession session, GeoFix here, string name)
        {
            if (session == null || here.HorizontalAccuracy <= 0f || here.HorizontalAccuracy > 20f) return null; // needs a good fix
            if (session.playerCheckpoints.Count(c => !session.completedCheckpointIds.Contains(c.id)) >= Config.maxPlayerCheckpoints) return null;
            var checkpoint = new PlayerCheckpoint
            {
                id = "cp_player_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                name = string.IsNullOrWhiteSpace(name) ? "Marked spot" : name,
                latitude = here.Latitude,
                longitude = here.Longitude,
                radius = Config.playerCheckpointRadius,
                metersAtPlacement = session.gpsTotalRealMeters
            };
            session.playerCheckpoints.Add(checkpoint);
            Save();
            return checkpoint;
        }

        public static bool RemoveCheckpoint(GameSession session, string id)
        {
            bool removed = session.playerCheckpoints.RemoveAll(c => c.id == id) > 0;
            if (removed) Save();
            return removed;
        }

        private static void CheckCheckpoints(GameSession session, GeoFix fix)
        {
            string inside = null;

            foreach (PlayerCheckpoint c in session.playerCheckpoints)
            {
                if (session.completedCheckpointIds.Contains(c.id)) continue;
                double d = Geo.DistanceMeters(fix.Latitude, fix.Longitude, c.latitude, c.longitude);
                if (!c.armed && d >= Config.playerCheckpointAway) c.armed = true; // went away: coming back now counts
                bool walkedEnough = session.gpsTotalRealMeters - c.metersAtPlacement >= Config.playerCheckpointWalk;
                if (c.armed && walkedEnough && d <= c.radius && fix.HorizontalAccuracy <= Mathf.Max(StrictAccuracy, c.radius)) inside = c.id;
            }

            foreach (GpsCheckpointDefinition c in Config.checkpoints)
            {
                if (c == null || string.IsNullOrEmpty(c.id) || session.completedCheckpointIds.Contains(c.id)) continue;
                double d = Geo.DistanceMeters(fix.Latitude, fix.Longitude, c.latitude, c.longitude);
                bool walkedEnough = (session.gpsSession?.realMeters ?? 0) >= c.requiredSessionMeters;
                if (walkedEnough && d <= c.radius && fix.HorizontalAccuracy <= Mathf.Max(StrictAccuracy, c.radius)) inside = c.id;
            }

            // Two consecutive good fixes inside the radius: one stray fix never triggers a checkpoint.
            if (inside == null || inside != insideId)
            {
                insideId = inside;
                insideCount = inside == null ? 0 : 1;
                return;
            }

            if (++insideCount < 2) return;
            Complete(session, inside);
            insideId = null;
            insideCount = 0;
        }

        private static void Complete(GameSession session, string id)
        {
            if (session.completedCheckpointIds.Contains(id)) return;
            session.completedCheckpointIds.Add(id); // recorded before paying: never twice
            GpsCheckpointDefinition designed = Config.checkpoints.FirstOrDefault(c => c != null && c.id == id);
            PlayerCheckpoint marked = session.playerCheckpoints.FirstOrDefault(c => c.id == id);
            string name = designed?.name ?? marked?.name ?? id;
            int money = designed?.money ?? Config.playerCheckpointMoney;
            int xp = designed?.xp ?? Config.playerCheckpointXp;
            string label = $"체크포인트 · {name}";
            if (money > 0) Pay(session, money, label);
            Experience.Add(xp, label);
            if (!string.IsNullOrEmpty(designed?.itemId) && !session.inventoryItemIds.Contains(designed.itemId) && !session.pendingCollectibleIds.Contains(designed.itemId))
                session.pendingCollectibleIds.Add(designed.itemId);
            Notify(session, "체크포인트 도착", $"Checkpoint reached: {name} (+${money:N0}, +{xp} XP)");
            GameEvents.RaiseObjectiveSignal(ObjectiveType.ReachCheckpoint, id);
            CheckpointReached?.Invoke(id);
            Save();
        }

        // ---- helpers --------------------------------------------------------------------------------

        private static void Pay(GameSession session, int amount, string label)
        {
            IEconomyService economy = GameManager.Instance?.GetService<IEconomyService>();
            if (economy != null) economy.AddMoney(amount, label);
            else session.AddMoney(amount);
        }

        private static void Notify(GameSession session, string korean, string english)
        {
            if (session.Settings.activityNotifications) GameEvents.RaiseMissionMessage(korean, english);
        }

        private static void Save() => GameManager.Instance?.GetService<ISaveService>()?.AutoSave();

        /// <summary>Clears the "inside a checkpoint" debounce (new session / tests).</summary>
        public static void ResetDebounce()
        {
            insideId = null;
            insideCount = 0;
        }
    }
}
