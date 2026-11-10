using Contract.Data;
using UnityEngine;

namespace Contract.Missions
{
    /// <summary>
    /// Pure reward maths for a completed contract (unit-testable, no scene state):
    /// base reward + 20% stealth bonus (never spotted) + 10% speed bonus (within the time limit)
    /// + 10% per optional objective completed + the Negotiator upgrade bonus (a rate on the base
    /// reward). Bonuses are rounded to $100.
    /// </summary>
    public static class RewardCalculator
    {
        public const float StealthBonusRate = 0.2f;
        public const float SpeedBonusRate = 0.1f;
        public const float OptionalBonusRate = 0.1f;

        public static MissionResult Calculate(MissionData mission, bool detected, float elapsedSeconds, int optionalCompleted, int optionalTotal,
            float negotiatorRate = 0f)
        {
            int baseReward = Mathf.Max(0, mission.rewardMoney);
            bool inTime = mission.timeLimitMinutes > 0 && elapsedSeconds <= mission.timeLimitMinutes * 60f;
            var result = new MissionResult
            {
                missionId = mission.missionId,
                missionName = mission.missionName,
                success = true,
                baseReward = baseReward,
                stealthBonus = detected ? 0 : Round(baseReward * StealthBonusRate),
                speedBonus = inTime ? Round(baseReward * SpeedBonusRate) : 0,
                optionalBonus = Round(baseReward * OptionalBonusRate * optionalCompleted),
                negotiatorBonus = Round(baseReward * Mathf.Max(0f, negotiatorRate)),
                elapsedSeconds = elapsedSeconds,
                detected = detected,
                optionalCompleted = optionalCompleted,
                optionalTotal = optionalTotal
            };
            result.totalReward = result.baseReward + result.stealthBonus + result.speedBonus + result.optionalBonus + result.negotiatorBonus;
            return result;
        }

        private static int Round(float value) => Mathf.RoundToInt(value / 100f) * 100;
    }
}
