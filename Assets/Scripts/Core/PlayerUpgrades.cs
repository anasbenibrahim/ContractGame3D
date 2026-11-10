using System.Collections.Generic;
using Contract.Data;

namespace Contract.Core
{
    /// <summary>
    /// Single source of truth for what the player's purchased upgrades do. Game systems ask for
    /// the bonus of one <see cref="UpgradeEffect"/> (a fraction: 0.16 = +16%); the value is
    /// recomputed only when the session's upgrade list changes (purchase, new game, load), so it
    /// is cheap enough to call every frame from every guard.
    /// </summary>
    public static class PlayerUpgrades
    {
        private static readonly float[] bonuses = new float[System.Enum.GetValues(typeof(UpgradeEffect)).Length];
        private static Dictionary<string, UpgradeData> catalogue;
        private static object cachedList;
        private static int cachedCount = -1;
        private static int cachedVersion = -1;

        // Domain reload is disabled in Play Mode options: clear statics on every play start.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            catalogue = null;
            cachedList = null;
            cachedCount = -1;
            cachedVersion = -1;
        }

        private static GameSession Session => GameManager.Instance != null ? GameManager.Instance.Session : null;

        /// <summary>Total bonus for one effect, as a fraction (0 when nothing is owned).</summary>
        public static float Bonus(UpgradeEffect effect)
        {
            Refresh();
            return bonuses[(int)effect];
        }

        /// <summary>1 + bonus: convenient for speed / range multipliers.</summary>
        public static float Multiplier(UpgradeEffect effect) => 1f + Bonus(effect);

        /// <summary>1 - bonus, clamped: for "detected slower" style reductions.</summary>
        public static float Reduction(UpgradeEffect effect) => System.Math.Max(0.1f, 1f - Bonus(effect));

        public static UpgradeData Find(string upgradeId)
        {
            EnsureCatalogue();
            return upgradeId != null && catalogue.TryGetValue(upgradeId, out UpgradeData data) ? data : null;
        }

        /// <summary>The outfit currently worn (null = the operative's own clothes).</summary>
        public static UpgradeData EquippedOutfit
        {
            get
            {
                GameSession session = Session;
                if (session == null || string.IsNullOrEmpty(session.equippedOutfitId)) return null;
                UpgradeData outfit = Find(session.equippedOutfitId);
                return outfit != null && outfit.IsOutfit && session.HasUpgrade(outfit.upgradeId) ? outfit : null;
            }
        }

        /// <summary>The look an operative is shown with: their own appearance plus the equipped outfit.</summary>
        public static CharacterAppearance Dress(CharacterAppearance appearance) =>
            appearance == null ? null : appearance.WithOutfit(EquippedOutfit);

        private static void EnsureCatalogue()
        {
            if (catalogue != null) return;
            catalogue = new Dictionary<string, UpgradeData>();
            GameDatabase database = GameDatabase.Load();
            if (database == null || database.Upgrades == null) return;
            foreach (UpgradeData upgrade in database.Upgrades)
            {
                if (upgrade != null && !string.IsNullOrEmpty(upgrade.upgradeId)) catalogue[upgrade.upgradeId] = upgrade;
            }
        }

        private static void Refresh()
        {
            GameSession session = Session;
            List<string> owned = session?.purchasedUpgradeIds;
            int count = owned?.Count ?? 0;
            int version = session?.upgradesVersion ?? 0;
            if (ReferenceEquals(owned, cachedList) && count == cachedCount && version == cachedVersion) return;

            cachedList = owned;
            cachedCount = count;
            cachedVersion = version;
            System.Array.Clear(bonuses, 0, bonuses.Length);
            if (owned == null) return;

            EnsureCatalogue();
            var levels = new Dictionary<string, int>();
            foreach (string id in owned)
            {
                levels.TryGetValue(id, out int level);
                levels[id] = level + 1;
            }

            foreach (KeyValuePair<string, int> pair in levels)
            {
                if (catalogue.TryGetValue(pair.Key, out UpgradeData upgrade) && !upgrade.IsOutfit)
                {
                    bonuses[(int)upgrade.effect] += upgrade.BonusAt(pair.Value);
                }
            }
        }
    }
}
