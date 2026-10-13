using System;
using UnityEngine;

namespace PuzzleKit
{
    // Oyuncunun kalıcı ilerlemesi: kaldığı level ve coin. Oyun kapanıp açılınca devam eder.
    public static class Progress
    {
        const string LevelKey = "pk.level";
        const string CoinsKey = "pk.coins";

        public static event Action<int> CoinsChanged;

        public static int Level
        {
            get => PlayerPrefs.GetInt(LevelKey, 0);
            set { PlayerPrefs.SetInt(LevelKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static int Coins => PlayerPrefs.GetInt(CoinsKey, 0);

        public static void AddCoins(int amount)
        {
            if (amount == 0) return;
            PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, Coins + amount));
            PlayerPrefs.Save();
            CoinsChanged?.Invoke(Coins);
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(LevelKey);
            PlayerPrefs.DeleteKey(CoinsKey);
            PlayerPrefs.Save();
            CoinsChanged?.Invoke(0);
        }
    }
}
