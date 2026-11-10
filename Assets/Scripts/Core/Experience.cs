using UnityEngine;

namespace Contract.Core
{
    /// <summary>
    /// Career XP and levels. Level 1 starts at 0 XP; reaching the next level needs 100 XP more than
    /// the previous step (100, 200, 300 ...), so levels slow down gently. XP is never lost.
    /// </summary>
    public static class Experience
    {
        public const int MaxLevel = 99;

        /// <summary>Total XP needed to reach <paramref name="level"/> (level 1 = 0, 2 = 100, 3 = 300, 4 = 600 ...).</summary>
        public static int XpForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, MaxLevel);
            return 50 * (level - 1) * level;
        }

        public static int LevelFor(int xp)
        {
            int level = 1;
            while (level < MaxLevel && XpForLevel(level + 1) <= xp) level++;
            return level;
        }

        /// <summary>0-1 progress from the current level to the next.</summary>
        public static float Progress(int xp)
        {
            int level = LevelFor(xp);
            if (level >= MaxLevel) return 1f;
            int from = XpForLevel(level), to = XpForLevel(level + 1);
            return (xp - from) / (float)(to - from);
        }

        public static int Level => LevelFor(GameManager.Instance?.Session?.xp ?? 0);

        /// <summary>Adds XP (ignored if not positive), raising XpGained and LevelUp.</summary>
        public static void Add(int amount, string source)
        {
            GameSession session = GameManager.Instance?.Session;
            if (session == null || amount <= 0) return;
            int before = LevelFor(session.xp);
            session.xp = (int)Mathf.Min(int.MaxValue - 1, (long)session.xp + amount);
            GameEvents.RaiseXpGained(amount, source);
            int after = LevelFor(session.xp);
            for (int level = before + 1; level <= after; level++) GameEvents.RaiseLevelUp(level);
        }
    }
}
