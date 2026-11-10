using System;
using Contract.Contracts;
using Contract.Core;
using UnityEngine;

namespace Contract.Economy
{
    /// <summary>
    /// Manages economy: money, rewards, spending, and victory condition.
    /// </summary>
    public sealed class EconomyManager : MonoBehaviour, IEconomyService
    {
        public static EconomyManager Instance { get; private set; }

        [Header("Victory")]
        [SerializeField] private int victoryMoney = GameConstants.VictoryMoney;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateInstance()
        {
            if (Instance != null) return;
            GameObject go = new GameObject("[EconomyManager]");
            EconomyManager service = go.AddComponent<EconomyManager>();
            Instance = service;
            DontDestroyOnLoad(go);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterService<IEconomyService>(service);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                DestroyImmediate(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterService<IEconomyService>(this);
            }
        }

        public int GetMoney() => GameManager.Instance?.Session?.money ?? 0;

        public void AddMoney(int amount) => AddMoney(amount, "기타 · Other");

        public void AddMoney(int amount, string source)
        {
            GameSession session = GameManager.Instance?.Session;
            if (session == null || amount <= 0) return;

            session.AddMoney(amount);
            Record(session, source, amount);
            GameEvents.RaiseMoneyChanged(session.money);

            if (session.money >= victoryMoney && !session.victoryAchieved)
            {
                session.victoryAchieved = true;
                GameEvents.RaiseVictoryReached();
            }
        }

        public bool TrySpend(int amount) => TrySpend(amount, "지출 · Purchase");

        public bool TrySpend(int amount, string source)
        {
            GameSession session = GameManager.Instance?.Session;
            if (session == null) return false;

            bool success = session.TrySpend(amount);
            if (success)
            {
                Record(session, source, -amount);
                GameEvents.RaiseMoneyChanged(session.money);
            }
            return success;
        }

        public void AwardMissionReward(int amount) => AddMoney(amount, "임무 보상 · Contract reward");

        public void AwardRegionBonus(int amount) => AddMoney(amount, "지역 보너스 · Region bonus");

        public float VictoryProgress => Mathf.Clamp01(GetMoney() / (float)victoryMoney);
        public int VictoryMoney => victoryMoney;

        private static void Record(GameSession session, string source, int amount)
        {
            session.ledger.Add(new Contract.Data.LedgerEntry
            {
                source = source,
                amount = amount,
                balanceAfter = session.money,
                timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }
    }
}