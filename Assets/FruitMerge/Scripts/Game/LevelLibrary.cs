using UnityEngine;

namespace FruitMerge
{
    // Bölümler Resources/Levels/levels.json'dan okunur (scratchpad'deki üretici/çözücü yazar).
    public static class LevelLibrary
    {
        static LevelDef[] all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => all = null;

        public static LevelDef[] All
        {
            get
            {
                if (all != null) return all;
                var json = Resources.Load<TextAsset>("Levels/levels");
                all = json != null ? JsonUtility.FromJson<LevelFile>(json.text)?.levels : null;
                if (all == null || all.Length == 0)
                {
                    Debug.LogWarning("[FruitMerge] Resources/Levels/levels.json bulunamadı");
                    all = new LevelDef[0];
                }
                return all;
            }
        }

        public static int Count => All.Length;

        public static LevelDef Get(int id) => id >= 1 && id <= All.Length ? All[id - 1] : null;
    }

    // İlerleme: açılan en yüksek bölüm ve her bölümün en iyi yıldızı (PlayerPrefs).
    public static class LevelProgress
    {
        const string UnlockedKey = "fm.level.unlocked";
        const string StarsKey = "fm.level.stars.";
        public const int EndlessUnlockLevel = 10;    // sonsuz mod ("Serbest Bahçe") bu bölüm bitince açılır

        public static int Unlocked => Mathf.Clamp(PlayerPrefs.GetInt(UnlockedKey, 1), 1, Mathf.Max(1, LevelLibrary.Count));
        public static int Completed => PlayerPrefs.GetInt(UnlockedKey, 1) - 1;
        public static bool EndlessOpen => Completed >= EndlessUnlockLevel;

        public static int Stars(int id) => PlayerPrefs.GetInt(StarsKey + id, 0);

        public static int TotalStars
        {
            get { int n = 0; for (int i = 1; i <= LevelLibrary.Count; i++) n += Stars(i); return n; }
        }

        public static void Complete(int id, int stars)
        {
            if (stars > Stars(id)) PlayerPrefs.SetInt(StarsKey + id, stars);
            if (id + 1 > PlayerPrefs.GetInt(UnlockedKey, 1)) PlayerPrefs.SetInt(UnlockedKey, id + 1);
            PlayerPrefs.Save();
        }

        public static void UnlockAll() => PlayerPrefs.SetInt(UnlockedKey, LevelLibrary.Count + 1);

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(UnlockedKey);
            for (int i = 1; i <= 200; i++) PlayerPrefs.DeleteKey(StarsKey + i);
        }
    }
}
