using System.Collections.Generic;
using UnityEngine;

namespace ColorBlockJam
{
    // Oyunun level'ları nereden okuduğu: önce Resources/LevelCatalog, yoksa koddaki DefaultLevels.
    public static class LevelDatabase
    {
        // Editor'deki "▶ Bu level'ı oyna" butonu, açılacak level'ın sırasını buraya yazar.
        public const string TestLevelKey = "cbj.testLevel";

        static List<LevelDef> levels;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => levels = null;

        public static int Count => All.Count;

        public static LevelDef Get(int index) => All[((index % Count) + Count) % Count];

        // Editor'den test için seçilmiş level varsa onun sırasını döner (bir kere), yoksa -1.
        public static int ConsumeTestLevel()
        {
#if UNITY_EDITOR
            int index = PlayerPrefs.GetInt(TestLevelKey, -1);
            PlayerPrefs.DeleteKey(TestLevelKey);
            return index;
#else
            return -1;
#endif
        }

        static List<LevelDef> All
        {
            get
            {
                if (levels != null) return levels;

                var catalog = Resources.Load<LevelCatalog>(LevelCatalog.ResourcePath);
                if (catalog != null && catalog.levels.Count > 0)
                {
                    levels = new List<LevelDef>();
                    foreach (var asset in catalog.levels)
                        if (asset != null) levels.Add(asset.level);
                }
                if (levels == null || levels.Count == 0) levels = DefaultLevels.All();
                return levels;
            }
        }
    }
}
