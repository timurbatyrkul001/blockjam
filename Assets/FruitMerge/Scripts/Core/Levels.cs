using System;

namespace FruitMerge
{
    // Bölüm verisi (Resources/Levels/levels.json). Unity'nin JsonUtility'siyle okunur; alan adları JSON ile aynı.
    [Serializable]
    public class TierCount
    {
        public int tier;
        public int count;
        public TierCount() { }
        public TierCount(int tier, int count) { this.tier = tier; this.count = count; }
    }

    [Serializable]
    public class LevelDef
    {
        public int id;
        public string title = "";
        public string hint = "";
        public TierCount[] start = new TierCount[0];   // başlangıç meyveleri (seviye, adet)
        public TierCount[] goals = new TierCount[0];   // bu bölümde OLUŞTURULACAK meyveler (tier 99: sepete atılacak taş)
        public int[] bag = new int[0];                 // her birleşmede/sepete atmada sırayla düşen meyveler
        public int moves;                              // hamle sınırı; 0 = sınırsız
        public int baskets;                            // sepete atma hakkı
        public TierCount[] ice = new TierCount[0];     // buzlu meyve: tier = meyve, count = kaç birleşmede çözülür
        public int[] leaves = new int[0];              // yaprakla kapalı yer: kaç birleşmede açılır
        public int[] leafTiers = new int[0];           // yaprağın altındaki meyve (-1 ya da eksik: boş yer)
        public int stones;                             // sadece sepete atılabilen taş sayısı
        public int seed = 1;                           // yerleşim tohumu
        public int par;                                // çözücünün bulduğu en az hamle (yıldız için)
    }

    [Serializable]
    public class LevelFile
    {
        public LevelDef[] levels = new LevelDef[0];
    }
}
