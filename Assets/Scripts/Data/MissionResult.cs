using System;

namespace Contract.Data
{
    /// <summary>Outcome of a finished contract, including the full reward breakdown.</summary>
    [Serializable]
    public sealed class MissionResult
    {
        public string missionId;
        public string missionName;
        public bool success;
        public string failureReason;

        public int baseReward;
        public int stealthBonus;
        public int speedBonus;
        public int optionalBonus;
        public int negotiatorBonus;
        public int totalReward;

        public float elapsedSeconds;
        public bool detected;
        public int optionalCompleted;
        public int optionalTotal;
        public int newBalance;
        public string regionCompletedId;
        public int regionBonus;
    }

    /// <summary>One line of the money ledger (financial dashboard / save file).</summary>
    [Serializable]
    public sealed class LedgerEntry
    {
        public string source;
        public int amount;
        public int balanceAfter;
        public long timestamp;
    }
}
