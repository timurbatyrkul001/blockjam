using System;
using System.Collections.Generic;

namespace FruitMerge
{
    // Meyve zinciri: aynı iki meyve birleşince bir üst seviyeye çıkar. Son meyve (karpuz) ikiyle hasat edilir.
    public static class FruitChain
    {
        public static readonly string[] Names =
            { "cherry", "strawberry", "grape", "orange", "apple", "peach", "pear", "watermelon" };

        public static int MaxTier => Names.Length - 1;

        // Seviye başına puan: her seviye bir öncekinin iki katı (10, 20, 40 ... 1280).
        public static int BasePoints(int tier) => 10 << tier;
    }

    public enum MoveKind { Invalid, Moved, Merged, Harvested, Discarded }

    public struct MoveResult
    {
        public MoveKind Kind;
        public int NewTier;     // Merged: oluşan meyve; Harvested: -1
        public int Points;
        public int Combo;
        public static MoveResult Invalid => new() { Kind = MoveKind.Invalid, NewTier = -1 };
    }

    // "2 armut yap" gibi küçük görev. Tamamlanınca puan + booster hakkı verir.
    public class Quest
    {
        public readonly int Tier;
        public readonly int Need;
        public int Have;
        public readonly int Bonus;
        public bool Done => Have >= Need;

        public Quest(int tier, int need, int bonus) { Tier = tier; Need = need; Bonus = bonus; }
    }

    // Ağaç: sabit sayıda yuva, her yuvada bir meyve (seviye) ya da boş (-1).
    // Zaman Tick(dt) ile ilerler; böylece mantık Unity'siz test edilebilir.
    // İki mod: sonsuz (zamanla meyve düşer) ve bölüm (LevelDef: sabit başlangıç, hedefler, torba, engeller).
    public class MergeTree
    {
        public const float ComboWindow = 3f;        // bu süre içinde yeni birleştirme kombo'yu artırır
        public const float StartSpawnInterval = 3.5f;
        public const float MinSpawnInterval = 1.1f;
        public const float SpeedUpPerSecond = 0.006f; // ~6,5 dakikada en hızlı tempoya ulaşır
        public const int MaxCombo = 10;
        public const int StartBaskets = 3;
        public const int StartSlots = 8;            // kalan yuvalar seviye atladıkça (her hasatta) açılır
        public const int HarvestsPerGolden = 5;     // her 5 hasatta bir altın karpuz (booster)

        public const int StoneTier = 99;             // taş: birleşmez, sadece sepete atılır

        readonly int[] slots;
        readonly int[] ice;     // >0: buzlu meyve, bu kadar birleşme sonra çözülür
        readonly int[] leaf;    // >0: yaprakla kapalı yer, bu kadar birleşme sonra açılır
        readonly Random rng;

        // ---- bölüm modu ----
        public LevelDef Def { get; private set; }
        public bool IsLevel => Def != null;
        public int[] GoalHave { get; private set; }
        public bool Won { get; private set; }
        public int MovesUsed { get; private set; }
        public int MovesLeft => Def == null || Def.moves <= 0 ? -1 : Def.moves - MovesUsed;
        public int BagLeft => Def == null ? 0 : Def.bag.Length - bagIndex;
        public int NextBag => BagLeft > 0 ? Def.bag[bagIndex] : -1;
        int bagIndex, owed;
        int[] dropOrder;

        public int IceAt(int slot) => ice[slot];
        public int LeafAt(int slot) => leaf[slot];
        public bool IsStone(int slot) => slots[slot] == StoneTier;
        public bool IsFrozen(int slot) => ice[slot] > 0;

        public event Action<int, int> IceChanged;           // (yuva, kalan)
        public event Action<int, int> LeafChanged;          // (yuva, kalan)
        public event Action<int> GoalProgress;              // (hedef sırası)

        public int SlotCount => slots.Length;
        public int ActiveSlots { get; private set; }
        public int Level => Harvests + 1;
        public bool IsActive(int slot) => slot < ActiveSlots && leaf[slot] <= 0;
        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int Harvests { get; private set; }
        public int Boosters { get; private set; }
        public int Merges { get; private set; }
        public int Baskets { get; private set; } = StartBaskets;   // sepete atma hakkı
        public float Elapsed { get; private set; }
        public int Pending { get; private set; }                     // ağaç dolu olduğu için düşemeyen meyveler
        public bool Overflowing => Pending > 0;
        public float OverflowTime { get; private set; }             // kuyruk doluyken geçen süre
        public const float OverflowGrace = 2f;      // ağaç doluyken yer açmak için son şans
        public const int MaxPending = 3;
        public bool GameOver { get; private set; }
        public Quest Quest { get; private set; }
        public int QuestsDone { get; private set; }

        public float SpawnInterval => Math.Max(MinSpawnInterval, StartSpawnInterval - Elapsed * SpeedUpPerSecond);
        public float SpawnTimer { get; private set; }       // 0 → SpawnInterval
        public float ComboTimer { get; private set; }       // kombo'nun bitmesine kalan süre

        public event Action<int, int> Spawned;               // (yuva, seviye)
        public event Action<Quest> QuestCompleted;
        public event Action<int> SlotUnlocked;               // (yeni açılan yuva)

        // quests: false → Klasik/Rekor modu: görev yok; sepet hakkı görev yerine her hasatta artar.
        // relaxed: true → rahat Klasik: zamanla meyve düşmez; her geçerli hamleden sonra FinishAction() 1 meyve getirir,
        //   kombo süreyle bitmez (tek hamlenin zincir uzunluğu), puan çarpılmaz.
        public MergeTree(int slotCount, int seed, int startFruits = 5, bool quests = true, bool relaxed = false)
        {
            Relaxed = relaxed;
            slots = new int[slotCount];
            ice = new int[slotCount];
            leaf = new int[slotCount];
            for (int i = 0; i < slotCount; i++) slots[i] = -1;
            ActiveSlots = Math.Min(StartSlots, slotCount);
            rng = new Random(seed);
            NextSpawnTier = RollTier();
            for (int i = 0; i < startFruits; i++) SpawnOne();
            if (relaxed) EnsurePlayable();
            Quest = quests ? MakeQuest() : null;
        }

        // Bir sonraki düşecek meyve (ekranda "SIRADAKİ MEYVE" olarak gösterilir).
        public int NextSpawnTier { get; private set; }

        int RollTier()
        {
            double roll = rng.NextDouble();               // %70 kiraz, %25 çilek, %5 üzüm
            return roll < 0.70 ? 0 : roll < 0.95 ? 1 : 2;
        }

        // Bölüm: başlangıç düzeni tohumla karışık yuvalara dağıtılır; zamanla meyve düşmez.
        public MergeTree(int slotCount, LevelDef def)
        {
            slots = new int[slotCount];
            ice = new int[slotCount];
            leaf = new int[slotCount];
            for (int i = 0; i < slotCount; i++) slots[i] = -1;
            ActiveSlots = slotCount;
            Def = def;
            Baskets = def.baskets;
            GoalHave = new int[def.goals.Length];
            rng = new Random(def.seed);

            var order = Shuffled(slotCount);
            int k = 0;
            for (int i = 0; i < def.leaves.Length && k < slotCount; i++)
            {
                int s = order[k++];
                leaf[s] = Math.Max(1, def.leaves[i]);
                if (i < def.leafTiers.Length && def.leafTiers[i] >= 0) slots[s] = def.leafTiers[i];   // gizli meyve
            }
            for (int i = 0; i < def.stones && k < slotCount; i++) slots[order[k++]] = StoneTier;
            foreach (var f in def.ice)
            {
                if (k >= slotCount) break;
                int s = order[k++];
                slots[s] = f.tier;
                ice[s] = Math.Max(1, f.count);
            }
            foreach (var f in def.start)
                for (int i = 0; i < f.count && k < slotCount; i++) slots[order[k++]] = f.tier;
            dropOrder = Shuffled(slotCount);
        }

        // Çözücü/ipucu için kopya (sadece bölüm modunda anlamlı; rastgelelik kullanılmaz).
        public MergeTree Clone() => new MergeTree(this);

        MergeTree(MergeTree o)
        {
            slots = (int[])o.slots.Clone();
            ice = (int[])o.ice.Clone();
            leaf = (int[])o.leaf.Clone();
            rng = new Random(0);
            ActiveSlots = o.ActiveSlots;
            Def = o.Def;
            GoalHave = o.GoalHave != null ? (int[])o.GoalHave.Clone() : null;
            dropOrder = o.dropOrder;
            bagIndex = o.bagIndex;
            owed = o.owed;
            MovesUsed = o.MovesUsed;
            Won = o.Won;
            GameOver = o.GameOver;
            Baskets = o.Baskets;
            Score = o.Score;
            Merges = o.Merges;
            Harvests = o.Harvests;
            Quest = o.Quest;
        }

        int[] Shuffled(int n)
        {
            var a = new int[n];
            for (int i = 0; i < n; i++) a[i] = i;
            for (int i = n - 1; i > 0; i--) { int j = rng.Next(i + 1); (a[i], a[j]) = (a[j], a[i]); }
            return a;
        }

        public bool GoalsDone
        {
            get
            {
                if (GoalHave == null) return false;
                for (int i = 0; i < GoalHave.Length; i++) if (GoalHave[i] < Def.goals[i].count) return false;
                return true;
            }
        }

        // 1–3 yıldız: hamle sınırsızsa her zaman 3; yoksa en az hamleye (par) ne kadar yakın bitirildiğine göre.
        public int Stars
        {
            get
            {
                if (!Won) return 0;
                if (Def.moves <= 0 || Def.par <= 0) return 3;
                int extra = MovesUsed - Def.par, slack = Math.Max(1, Def.moves - Def.par);
                return extra <= slack / 3 ? 3 : extra <= slack * 2 / 3 ? 2 : 1;
            }
        }

        // ---- rahat Klasik ----
        public bool Relaxed { get; private set; }
        int[][] neighbors;   // her yuvanın komşuları, yakından uzağa (eşitlikte küçük index önce)

        public void SetNeighbors(int[][] list) => neighbors = list;

        // Otomatik zincirin bir adımı: hedefteki meyvenin komşularında aynı meyve varsa (en yakını) hedefe birleşir.
        public bool ChainStep(int target, out int partner, out MoveResult result)
        {
            partner = -1;
            result = MoveResult.Invalid;
            if (!Relaxed || GameOver || neighbors == null || slots[target] < 0) return false;
            int tier = slots[target];
            foreach (int n in neighbors[target])
            {
                if (!IsActive(n) || slots[n] != tier) continue;
                partner = n;
                break;
            }
            if (partner < 0) return false;
            Combo = Math.Min(Combo + 1, MaxCombo);
            result = MergeInto(partner, target, tier);
            return true;
        }

        // Oyuncu hamlesi (ve zinciri) bitti: boş yer varsa 1 meyve gelir; yer yok, çift yok, sepet hakkı yoksa oyun biter.
        public void FinishAction()
        {
            if (!Relaxed || GameOver) return;
            if (FilledCount < ActiveSlots) SpawnOne();
            EnsurePlayable();
            if (FilledCount >= ActiveSlots && !FindPair(out _, out _) && Baskets <= 0) GameOver = true;
        }

        // Boş yer varken hiç çift yoksa oyuncunun yapacak hamlesi kalmaz (meyve yalnız hamleden sonra gelir):
        // bu durumda çift oluşana ya da ağaç dolana kadar meyve gelir. Böylece oyun takılmaz.
        void EnsurePlayable()
        {
            while (!FindPair(out _, out _) && FilledCount < ActiveSlots) SpawnOne();
        }

        // Test/araç: ağacı verilen meyvelerle kurar (-1 boş). Oyunda kullanılmaz.
        public void DebugSetBoard(int[] tiers, int baskets)
        {
            for (int i = 0; i < slots.Length; i++) { slots[i] = i < tiers.Length ? tiers[i] : -1; ice[i] = leaf[i] = 0; }
            Baskets = baskets;
            GameOver = false;
            Combo = 0;
        }

        public int this[int slot] => slots[slot];
        public bool IsEmpty(int slot) => slots[slot] < 0;

        public int FilledCount
        {
            get { int n = 0; for (int i = 0; i < ActiveSlots; i++) if (slots[i] >= 0) n++; return n; }
        }

        // Yuvadaki meyveyi başka yuvaya bırak: aynı meyve → birleşir, boş yuva → taşınır, farklı meyve → geçersiz.
        public MoveResult Move(int from, int to)
        {
            if (GameOver || from == to || slots[from] < 0 || !IsActive(to)) return MoveResult.Invalid;
            if (ice[from] > 0 || ice[to] > 0 || leaf[from] > 0 || slots[from] == StoneTier) return MoveResult.Invalid;
            int tier = slots[from];

            if (slots[to] < 0)
            {
                slots[to] = tier;
                slots[from] = -1;
                return new MoveResult { Kind = MoveKind.Moved, NewTier = tier, Combo = Combo };
            }
            if (slots[to] != tier) return MoveResult.Invalid;

            if (Relaxed) Combo = 1;      // yeni oyuncu hamlesi: zincir 1'den başlar
            else BumpCombo();
            return MergeInto(from, to, tier);
        }

        // Birleştirme (oyuncu hamlesi ve otomatik zincir aynı yolu kullanır).
        MoveResult MergeInto(int from, int to, int tier)
        {
            Merges++;
            slots[from] = -1;

            if (tier == FruitChain.MaxTier)
            {
                // iki karpuz: hasat, ikisi de ağaçtan kalkar; seviye atlanır ve yeni yuva açılır
                slots[to] = -1;
                Harvests++;
                if (!IsLevel && Harvests % HarvestsPerGolden == 0) Boosters++;
                if (!IsLevel && Quest == null) Baskets++;          // görevsiz modda sepet hakkı hasatla gelir
                if (!IsLevel && ActiveSlots < slots.Length) SlotUnlocked?.Invoke(ActiveSlots++);
                int harvest = Award(FruitChain.BasePoints(tier) * 4);
                if (IsLevel) AfterLevelMove(merged: true);
                return new MoveResult { Kind = MoveKind.Harvested, NewTier = -1, Points = harvest, Combo = Combo };
            }

            int newTier = tier + 1;
            slots[to] = newTier;
            int points = Award(FruitChain.BasePoints(newTier));
            if (IsLevel) { CountGoals(newTier); AfterLevelMove(merged: true); }
            else if (Quest != null) CountQuest(newTier);
            return new MoveResult { Kind = MoveKind.Merged, NewTier = newTier, Points = points, Combo = Combo };
        }

        // Sepete at: yer açar ama puan vermez, kombo'yu bozar ve bir sepet hakkı harcar.
        public MoveResult Discard(int slot)
        {
            if (GameOver || slots[slot] < 0 || Baskets <= 0 || ice[slot] > 0 || leaf[slot] > 0) return MoveResult.Invalid;
            if (IsLevel && slots[slot] == StoneTier) CountGoals(StoneTier);
            Baskets--;
            slots[slot] = -1;
            Combo = 0;
            ComboTimer = 0f;
            if (IsLevel) AfterLevelMove(merged: false);
            return new MoveResult { Kind = MoveKind.Discarded, NewTier = -1 };
        }

        // Booster (altın karpuz): ağaçtaki en düşük seviyeli eşleşebilen çifti kendiliğinden birleştirir.
        public bool UseBooster(out int from, out int to, out MoveResult result)
        {
            from = to = -1;
            result = MoveResult.Invalid;
            if (Boosters <= 0 || !FindPair(out from, out to)) return false;
            Boosters--;
            result = Move(from, to);
            return true;
        }

        // Booster hakkını harcar; hangi çiftin birleşeceğini oyun seçip Move ile uygular (animasyon için).
        public bool SpendBooster()
        {
            if (Boosters <= 0) return false;
            Boosters--;
            return true;
        }

        public void Tick(float dt)
        {
            if (GameOver) return;
            Elapsed += dt;

            if (Relaxed) return;   // rahat Klasik: süre yok (kombo da zamanla bitmez)
            if (ComboTimer > 0f && (ComboTimer -= dt) <= 0f) { Combo = 0; ComboTimer = 0f; }
            if (IsLevel) return;   // bölümde zamanla meyve düşmez

            // son şans sırasında yeni meyve sayacı durur: aynı anda ikinci meyve sıraya girmez
            if (!Overflowing) SpawnTimer += dt;
            if (SpawnTimer >= SpawnInterval)
            {
                SpawnTimer = 0f;
                Pending++;                  // yeni meyve sıraya girer
            }

            // yer varsa bekleyen meyveler hemen düşer
            while (Pending > 0 && FilledCount < ActiveSlots)
            {
                Pending--;
                SpawnOne();
            }

            // ağaç dolu ve meyve bekliyor: kısa süre içinde yer açılmazsa oyun biter
            OverflowTime = Overflowing ? OverflowTime + dt : 0f;
            if (OverflowTime >= OverflowGrace || Pending >= MaxPending) GameOver = true;
        }

        public bool FindPair(out int a, out int b)
        {
            for (int tier = 0; tier <= FruitChain.MaxTier; tier++)
            {
                a = b = -1;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != tier || ice[i] > 0 || leaf[i] > 0) continue;
                    if (a < 0) a = i;
                    else { b = i; return true; }
                }
            }
            a = b = -1;
            return false;
        }

        void SpawnOne()
        {
            int empty = 0;
            for (int i = 0; i < ActiveSlots; i++) if (slots[i] < 0) empty++;
            if (empty == 0) return;

            int pick = rng.Next(empty);
            for (int i = 0; i < ActiveSlots; i++)
            {
                if (slots[i] >= 0 || pick-- > 0) continue;
                slots[i] = NextSpawnTier;
                NextSpawnTier = RollTier();
                Spawned?.Invoke(i, slots[i]);
                return;
            }
        }

        void BumpCombo()
        {
            Combo = ComboTimer > 0f ? Math.Min(Combo + 1, MaxCombo) : 1;
            ComboTimer = ComboWindow;
        }

        // Puan = temel puan × kombo (1×, 2×, 3× … en çok 10×); görev bonusu çarpılmaz.
        int Award(int basePoints)
        {
            int points = Relaxed ? basePoints : basePoints * Math.Max(1, Combo);   // rahatta kombo yalnız görsel
            Score += points;
            return points;
        }

        // ---- bölüm modu yardımcıları ----

        void CountGoals(int tier)
        {
            for (int i = 0; i < GoalHave.Length; i++)
            {
                if (Def.goals[i].tier != tier || GoalHave[i] >= Def.goals[i].count) continue;
                GoalHave[i]++;
                GoalProgress?.Invoke(i);
            }
        }

        // Her hamle (birleşme ya da sepete atma) sonrası: buz/yaprak sayacı, torbadan meyve, bitiş kontrolü.
        void AfterLevelMove(bool merged)
        {
            MovesUsed++;
            if (merged)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (ice[i] > 0) IceChanged?.Invoke(i, --ice[i]);
                    if (leaf[i] > 0) LeafChanged?.Invoke(i, --leaf[i]);
                }
            }
            if (BagLeft > 0) owed++;
            FlushBag();

            // önce kazanma, sonra takılma kontrolü
            if (GoalsDone) { Won = true; GameOver = true; }
            else if (MovesLeft == 0 || !HasMove()) GameOver = true;
        }

        bool StoneGoalOpen()
        {
            for (int i = 0; i < GoalHave.Length; i++)
                if (Def.goals[i].tier == StoneTier && GoalHave[i] < Def.goals[i].count) return true;
            return false;
        }

        void FlushBag()
        {
            while (owed > 0 && BagLeft > 0)
            {
                int target = -1;
                foreach (int s in dropOrder) if (slots[s] < 0 && leaf[s] <= 0) { target = s; break; }
                if (target < 0) return;   // yer yok: bir sonraki hamlede düşer
                owed--;
                slots[target] = Def.bag[bagIndex++];
                Spawned?.Invoke(target, slots[target]);
            }
        }

        // Yapılabilecek anlamlı hamle var mı? (birleşecek çift ya da torbadan yeni meyve getirecek sepet hakkı)
        public bool HasMove()
        {
            if (FindPair(out _, out _)) return true;
            if (Baskets <= 0) return false;
            if (StoneGoalOpen()) for (int i = 0; i < slots.Length; i++) if (slots[i] == StoneTier) return true;
            if (BagLeft <= 0) return false;
            for (int i = 0; i < slots.Length; i++) if (slots[i] >= 0 && ice[i] <= 0 && leaf[i] <= 0) return true;
            return false;
        }

        void CountQuest(int tier)
        {
            if (Quest.Tier != tier || Quest.Done) return;
            if (++Quest.Have < Quest.Need) return;

            Score += Quest.Bonus;
            Baskets++;
            QuestsDone++;
            var done = Quest;
            Quest = MakeQuest();
            QuestCompleted?.Invoke(done);
        }

        // Görevler zorlaşır: önce düşük meyveler, sonra büyükler.
        Quest MakeQuest()
        {
            int tier = Math.Min(2 + QuestsDone / 2 + rng.Next(2), FruitChain.MaxTier - 1);
            int need = 2 + (QuestsDone % 3);
            return new Quest(tier, need, FruitChain.BasePoints(tier) * need * 2);
        }
    }
}
