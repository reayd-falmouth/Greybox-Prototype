using System;
using UnityEngine;

namespace Runtime.RMC.Backgammon.Stats
{
    public static class MoneyBalanceService
    {
        const string KeyBalance = "bg_money_balance";
        const int    DefaultBalance = 100;

        public static event Action<int> OnBalanceChanged;

        public static int Balance
        {
            get => PlayerPrefs.GetInt(KeyBalance, DefaultBalance);
            private set
            {
                PlayerPrefs.SetInt(KeyBalance, value);
                PlayerPrefs.Save();
                OnBalanceChanged?.Invoke(value);
                Debug.Log($"[MoneyBalance] Balance updated: ${value:N0}");
            }
        }

        public static void AddWinnings(int amount)
        {
            Balance = Balance + amount;
        }

        public static void DeductLoss(int amount)
        {
            Balance = Balance - amount;
        }

        public static void ResetToDefault(int overrideAmount = DefaultBalance)
        {
            PlayerPrefs.SetInt(KeyBalance, overrideAmount);
            PlayerPrefs.Save();
            OnBalanceChanged?.Invoke(overrideAmount);
            Debug.Log($"[MoneyBalance] Reset to: {overrideAmount:N0}");
        }
    }
}
