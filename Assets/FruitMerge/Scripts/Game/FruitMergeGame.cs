using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FruitMerge
{
    // Sahnede hiçbir şey hazırlamaya gerek yok: oyun açılınca kendini kurar.
    public static class FruitMergeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindAnyObjectByType<FruitMergeGame>() != null) return;
            new GameObject("FruitMerge").AddComponent<FruitMergeGame>();
        }
    }

    // Ağaçta meyve birleştirme: meyveyi tut, aynı meyvenin üstüne bırak → bir üst meyve.
    // Mantık MergeTree'de (Core); burası sadece görüntü, sürükle-bırak ve ses/titreşim.
    // Akış: harita → bölüm (LevelDef) ya da sonsuz mod ("Serbest Bahçe") → sonuç kartı → harita.
    public class FruitMergeGame : MonoBehaviour
    {
        const string BestKey = "fm.classic.best";   // Klasik/Rekor moduna özel (zamanlı Klasik)
        const string OldBestKey = "fm.best";
        const string RelaxedBestKey = "fm.classic.best.v2";   // rahat Klasik: puan ekonomisi farklı, rekor ayrı

        // Klasik modun oynanışı: true = rahat prototip (iki dokunuş, otomatik zincir, hamle başına 1 meyve);
        // false = eski zamanlı Klasik. Oyuncuya gösterilmez; karşılaştırma için koddan değiştirilir.
        const bool RelaxedClassic = true;
        const float TapSlop = 12f;            // bu kadar (canvas birimi) kayan basış sürüklemedir, daha azı dokunuş
        const float ChainStepDelay = 0.16f;   // otomatik zincir adımları arası
        const int SlotCount = 12;

        MergeTree tree;
        MergeHud hud;
        RectTransform[] slots;
        Image[] views;
        Image[] overlays;       // bölüm modunda buz/yaprak örtüsü (yuvanın üstünde)
        LevelDef level;         // null: sonsuz mod
        bool paused, gameOverShown;
        float idleTime;         // ipucu için: son dokunuştan beri geçen süre
        int hintA = -1, hintB = -1;
        Image hand;
        int lastComboShown, lastBoosters;

        int dragSlot = -1;
        RectTransform dragView;
        int busy;   // süren hamle animasyonu sayısı (bu sırada yeni sürükleme yok)

        bool RelaxedNow => tree != null ? tree.Relaxed : RelaxedClassic;

        int Best
        {
            get => RelaxedNow ? PlayerPrefs.GetInt(RelaxedBestKey, 0)
                              : Mathf.Max(PlayerPrefs.GetInt(BestKey, 0), PlayerPrefs.GetInt(OldBestKey, 0));
            set => PlayerPrefs.SetInt(RelaxedNow ? RelaxedBestKey : BestKey, value);   // Save yalnız oyun sonunda / arka planda
        }

        // ---- rahat Klasik durumu ----
        int[][] neighbors;
        int selected = -1;
        Image selectRing;
        bool pressActive;
        int pressSlot = -1;
        Vector2 pressPos;
        bool actionRunning;              // oyuncu hamlesi + zinciri + yeni meyve sürüyor
        Vector2Int? pendingAction;       // zincir sürerken yapılan tek bekleyen hamle (from, to)
        float comboShowUntil;

        int bestAtStart;

        void Awake()
        {
            Application.targetFrameRate = 60;
            EnsureCamera();
            EnsureEventSystem();
            hud = MergeHud.Create();
            hud.BoosterPressed += UseBooster;
            hud.RestartPressed += Restart;
            hud.PausePressed += TogglePause;
            hud.MapPressed += ShowMap;
            hud.MenuPressed += ShowMenu;
            hud.LevelsPressed += ShowMap;
            hud.ClassicPressed += NewGame;
            hud.LevelPressed += StartLevel;
            hud.NextPressed += NextLevel;

            var layout = FruitArt.Slots();
            int count = Mathf.Min(SlotCount, layout.Length);
            slots = new RectTransform[count];
            views = new Image[count];
            overlays = new Image[count];
            for (int i = 0; i < count; i++)
            {
                var rt = new GameObject($"Slot{i}", typeof(RectTransform)).GetComponent<RectTransform>();
                rt.SetParent(hud.FruitLayer, false);
                rt.anchorMin = rt.anchorMax = new Vector2(layout[i].x, 1f - layout[i].y);
                rt.sizeDelta = Vector2.zero;
                slots[i] = rt;
            }
            neighbors = BuildNeighbors(layout, count);
        }

        // Otomatik zincir komşuları: her yuvanın en yakın 2 yuvası, ilişki simetrik; yakından uzağa,
        // eşitlikte küçük index önce. Yuva düzeni sabit olduğu için bir kez hesaplanır.
        int[][] BuildNeighbors(Vector2[] layout, int count)
        {
            var size = hud.TreeArea.rect.size;
            var pos = new Vector2[count];
            for (int i = 0; i < count; i++) pos[i] = new Vector2(layout[i].x * size.x, -layout[i].y * size.y);
            var sets = new System.Collections.Generic.List<int>[count];
            for (int i = 0; i < count; i++) sets[i] = new System.Collections.Generic.List<int>();
            for (int i = 0; i < count; i++)
            {
                var order = new System.Collections.Generic.List<int>();
                for (int j = 0; j < count; j++) if (j != i) order.Add(j);
                int a = i;
                order.Sort((x, y) => { int c = (pos[a] - pos[x]).sqrMagnitude.CompareTo((pos[a] - pos[y]).sqrMagnitude); return c != 0 ? c : x.CompareTo(y); });
                for (int k = 0; k < 2 && k < order.Count; k++)
                {
                    int j = order[k];
                    if (!sets[i].Contains(j)) sets[i].Add(j);
                    if (!sets[j].Contains(i)) sets[j].Add(i);
                }
            }
            var result = new int[count][];
            var log = new System.Text.StringBuilder("[FruitMerge] zincir komşuları:");
            for (int i = 0; i < count; i++)
            {
                int a = i;
                sets[i].Sort((x, y) => { int c = (pos[a] - pos[x]).sqrMagnitude.CompareTo((pos[a] - pos[y]).sqrMagnitude); return c != 0 ? c : x.CompareTo(y); });
                result[i] = sets[i].ToArray();
                log.Append($" {i}:[{string.Join(",", result[i])}]");
            }
            Debug.Log(log.ToString());
            return result;
        }

        void Start() => ShowMenu();

        void OnApplicationPause(bool pause)
        {
            if (!pause) return;
            PlayerPrefs.Save();
            // telefon arka plana geçince oyun da duraklasın
            if (tree != null && !tree.GameOver && !paused) TogglePause();
        }

        // ---- akış ----

        public void ShowMenu()
        {
            StopPlay();
            hud.HideMap();
            hud.ShowMenu(Best);
        }

        public void ShowMap()
        {
            StopPlay();
            hud.HideMenu();
            hud.ShowMap();
        }

        // Oyunu kapatır (meyveler, efektler, panel); menü/harita açılmadan önce.
        void StopPlay()
        {
            ClearBoard();
            tree = null;
            level = null;
            paused = false;
            hud.ShowPause(false);
            hud.HideGameOver();
            hud.HideResult();
            PlayerPrefs.Save();
        }

        // Klasik / Rekor modu: görev, bölüm, yıldız ve hamle sınırı yok; sadece skor, kombo ve rekor.
        public void NewGame()
        {
            level = null;
            var t = new MergeTree(slots.Length, System.Environment.TickCount, quests: false, relaxed: RelaxedClassic);
            if (t.Relaxed) t.SetNeighbors(neighbors);
            Begin(t);
        }

        public void StartLevel(int id)
        {
            var def = LevelLibrary.Get(id);
            if (def == null) return;
            level = def;
            Begin(new MergeTree(slots.Length, def));
        }

#if UNITY_EDITOR
        public string DevState() =>
            $"mode={(tree == null ? (hud.MenuVisible ? "menu" : hud.MapVisible ? "map" : "none") : level != null ? "level" + level.id : "classic")}\n" +
            $"paused={paused}\ngameOver={tree?.GameOver}\nscore={tree?.Score}\ncombo={tree?.Combo}\ncomboTimer={tree?.ComboTimer:0.00}\n" +
            $"spawnTimer={tree?.SpawnTimer:0.00}\noverflow={tree?.OverflowTime:0.00}\nquest={(tree?.Quest == null ? "yok" : "var")}\n" +
            $"best(fm.classic.best)={PlayerPrefs.GetInt(BestKey, 0)}\nsound={PlayerPrefs.GetInt("fm.sound", 1)} sfxMuted={Sfx.Muted}\n" +
            $"music={PlayerPrefs.GetInt("fm.music", 1)} musicOn={Music.Enabled} musicSources={Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length}\n" +
            $"levelUnlocked={LevelProgress.Unlocked} stars={LevelProgress.TotalStars}\ntimeScale={Time.timeScale}\n" +
            $"relaxed={tree?.Relaxed} best(v2)={PlayerPrefs.GetInt(RelaxedBestKey, 0)}\n{DevBoard()}\n";

        // Editör testi: ağacı kurar ("0:0,3:0,1:1" → yuva:meyve), görüntüleri yeniler.
        public void DevSetup(string spec, int baskets)
        {
            if (tree == null) return;
            var tiers = new int[slots.Length];
            for (int i = 0; i < tiers.Length; i++) tiers[i] = -1;
            foreach (var part in spec.Split(','))
            {
                var kv = part.Split(':');
                if (kv.Length == 2) tiers[int.Parse(kv[0])] = int.Parse(kv[1]);
            }
            StopAllCoroutines();
            busy = 0;
            actionRunning = false;
            pendingAction = null;
            Deselect();
            tree.DebugSetBoard(tiers, baskets);
            for (int i = 0; i < slots.Length; i++)
            {
                if (views[i] != null) Destroy(views[i].gameObject);
                views[i] = tree.IsEmpty(i) ? null : hud.MakeFruit(slots[i], tree[i]);
            }
        }

        public void DevTap(int slot) => OnTap(slot >= 0 ? (Vector2)slots[slot].position : new Vector2(5, 5));

        public void DevDrag(int from, int to)
        {
            if (views[from] == null || actionRunning || busy > 0) return;
            BeginDrag(from);
            Release(slots[to].position);
        }

        public void DevFinish() => tree?.FinishAction();

        public string DevBoard()
        {
            if (tree == null) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < slots.Length; i++) sb.Append(tree.IsEmpty(i) ? "." : tree[i].ToString()).Append(i == 7 ? "|" : " ");
            int filled = 0;
            for (int i = 0; i < slots.Length; i++) if (!tree.IsEmpty(i)) filled++;
            return $"board=[{sb}] filled={filled} active={tree.ActiveSlots} harvests={tree.Harvests} baskets={tree.Baskets} selected={selected} action={actionRunning} pending={(pendingAction.HasValue ? pendingAction.Value.ToString() : "-")}";
        }

        // Editör testi: kaybetme kartını hemen gösterir.
        public void DevShowLose()
        {
            if (level == null) return;
            hud.ShowLose(level, "HAMLE BİTTİ");
        }
#endif

        void NextLevel()
        {
            if (level != null && LevelLibrary.Get(level.id + 1) != null) StartLevel(level.id + 1);
            else ShowMap();
        }

        void Restart()
        {
            if (level != null) StartLevel(level.id);
            else NewGame();
        }

        void ClearBoard()
        {
            StopAllCoroutines();
            busy = 0;
            dragSlot = -1;
            Deselect();
            pressActive = false;
            pendingAction = null;
            actionRunning = false;
            comboShowUntil = 0f;
            if (dragView != null) Destroy(dragView.gameObject);
            dragView = null;
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] != null) Destroy(views[i].gameObject);
                if (overlays[i] != null) Destroy(overlays[i].gameObject);
                views[i] = null;
                overlays[i] = null;
            }
            // uçuşta kalan efektler
            for (int i = hud.DragLayer.childCount - 1; i >= 0; i--) Destroy(hud.DragLayer.GetChild(i).gameObject);
        }

        void Begin(MergeTree t)
        {
            ClearBoard();
            tree = t;
            tree.Spawned += OnSpawned;
            tree.QuestCompleted += OnQuestCompleted;
            tree.SlotUnlocked += OnSlotUnlocked;
            tree.IceChanged += OnIceChanged;
            tree.LeafChanged += OnLeafChanged;
            tree.GoalProgress += OnGoalProgress;
            lastBoosters = 0;
            for (int i = 0; i < slots.Length; i++)
                if (!tree.IsEmpty(i)) StartCoroutine(DropIn(i, tree[i], i * 0.05f));
            if (level != null) BuildOverlays();

            paused = gameOverShown = false;
            lastComboShown = 0;
            idleTime = 0f;
            hintA = hintB = -1;
            hud.HideMap();
            hud.HideMenu();
            bestAtStart = Best;
            hud.ShowPause(false);
            hud.HideGameOver();
            hud.HideResult();
            hud.SetMode(level != null);
            hud.SetRelaxedClassic(level == null && tree.Relaxed);
            if (level != null)
            {
                hud.SetLevel(level);
                hud.SetGoals(level, tree.GoalHave);
            }
            else if (tree.Quest != null) hud.SetQuest(tree.Quest);
            RefreshHud();
        }

        void Update()
        {
#if UNITY_EDITOR
            DevTools.Poll(this);
#endif
            if (tree == null || paused) return;

            tree.Tick(Time.deltaTime);
            RefreshHud();
            Sway();

            if (tree.GameOver)
            {
                // bölümde son birleşme animasyonu bitince kart açılır
                if (!gameOverShown && (level == null || busy == 0))
                    StartCoroutine(level == null ? GameOverRoutine() : LevelEndRoutine());
                return;
            }

            if (level != null) Hint();
            if (tree.Relaxed) { RelaxedInput(); return; }
            if (PointerInput.Pressed && dragSlot < 0 && busy == 0) TryGrab(PointerInput.Position);
            if (dragSlot < 0) return;
            if (PointerInput.Held) Drag(PointerInput.Position);
            else Release(PointerInput.Position);
        }

        // Daldaki meyveler rüzgârda hafifçe sallanır; her biri farklı ritimde.
        void Sway()
        {
            float t = Time.time;
            for (int i = 0; i < views.Length; i++)
            {
                var v = views[i];
                if (v == null || i == dragSlot) continue;
                v.rectTransform.localEulerAngles = new Vector3(0, 0, 5f * Mathf.Sin(t * 1.8f + i * 1.3f));
                if (i == selected) v.rectTransform.localScale = Vector3.one * (1.1f + 0.03f * Mathf.Sin(t * 8f));
            }
        }

        void RefreshHud()
        {
            if (level != null)
            {
                hud.SetMoves(tree.MovesLeft);
                hud.SetNextBag(tree.NextBag, tree.BagLeft);
                hud.SetBaskets(tree.Baskets);
                return;
            }
            if (tree.Score > Best) Best = tree.Score;      // rekor geçildi: bellekte güncel, diske oyun sonunda
            hud.SetScore(tree.Score, Best);
            hud.SetNextFruit(tree.NextSpawnTier);
            if (tree.Relaxed)
            {
                // kombo = bu hamlenin zincir uzunluğu; zincir bitince kısa görünür, sonra söner (süre baskısı yok)
                float left = actionRunning ? 1f : Mathf.Clamp01((comboShowUntil - Time.time) / 1.2f);
                hud.SetCombo(left > 0f ? tree.Combo : 0, left);
            }
            else hud.SetCombo(tree.Combo, tree.ComboTimer / MergeTree.ComboWindow);
            hud.SetNext(tree.SpawnTimer / tree.SpawnInterval, tree.SpawnInterval - tree.SpawnTimer);
            hud.SetBaskets(tree.Baskets);
            hud.SetBoosters(tree.Boosters);
            if (tree.Boosters > lastBoosters) { hud.PunchBooster(); Sfx.Play("order", 0.7f, 0f); }
            lastBoosters = tree.Boosters;
            hud.SetHarvests(tree.Harvests, tree.Level);
            hud.SetWarning(tree.Overflowing && !tree.GameOver, tree.OverflowTime / MergeTree.OverflowGrace);
        }

        // ---- sürükle-bırak ----

        void TryGrab(Vector2 screen)
        {
            idleTime = 0f;
            ClearHint();
            int slot = SlotAt(screen, filled: true, except: -1);
            if (slot < 0 || views[slot] == null) return;
            BeginDrag(slot);
        }

        void BeginDrag(int slot)
        {
            if (tree.IsFrozen(slot))
            {
                // buzlu meyve tutulamaz: buz titrer
                Sfx.Play("wrong", 0.5f);
                if (overlays[slot] != null) StartCoroutine(Shake(overlays[slot].rectTransform));
                return;
            }

            dragSlot = slot;
            dragView = views[slot].rectTransform;
            dragView.SetParent(hud.DragLayer, true);
            Sfx.Play("pickup", 0.7f);
            Haptics.Light();
        }

        void Drag(Vector2 screen)
        {
            // meyve parmağın biraz üstünde dursun ki parmak kapatmasın
            var target = screen + Vector2.up * (60f * hud.Scale);
            float k = 1f - Mathf.Exp(-30f * Time.deltaTime);
            dragView.position = Vector3.Lerp(dragView.position, target, k);
            dragView.localScale = Vector3.Lerp(dragView.localScale, Vector3.one * 1.15f, k);

            // aynı meyveler hafifçe nabız atar: nereye bırakabileceğini gösterir
            int tier = tree[dragSlot];
            float pulse = 1f + 0.07f * Mathf.Sin(Time.time * 10f);
            for (int i = 0; i < views.Length; i++)
                if (i != dragSlot && views[i] != null)
                    views[i].rectTransform.localScale = Vector3.one * (tree[i] == tier ? pulse : 1f);
        }

        void Release(Vector2 screen)
        {
            int from = dragSlot;
            var view = dragView;
            dragSlot = -1;
            dragView = null;
            foreach (var v in views) if (v != null) v.rectTransform.localScale = Vector3.one;

            var dropPoint = (Vector2)view.position;
            if (hud.Basket.gameObject.activeInHierarchy &&
                (RectTransformUtility.RectangleContainsScreenPoint(hud.Basket, screen, null) ||
                 RectTransformUtility.RectangleContainsScreenPoint(hud.Basket, dropPoint, null)))
            {
                Discard(from, view);
                return;
            }

            int to = SlotAt(dropPoint, filled: null, except: from);
            if (to < 0) to = SlotAt(screen, filled: null, except: from);
            if (to < 0) { StartCoroutine(ReturnToSlot(from, view)); return; }

            Apply(from, to, view);
        }

        // Hamleyi mantığa uygular ve sonucunu canlandırır. view: hareket eden meyvenin görüntüsü.
        void Apply(int from, int to, RectTransform view)
        {
            var result = tree.Move(from, to);
            switch (result.Kind)
            {
                case MoveKind.Moved:
                    views[to] = views[from];
                    views[from] = null;
                    StartCoroutine(MoveTo(view, to, 0.12f, () => Settle(to)));
                    Sfx.Play("drop", 0.5f);
                    break;

                case MoveKind.Merged:
                case MoveKind.Harvested:
                    var other = views[to];
                    views[from] = null;
                    views[to] = null;
                    if (tree.Relaxed) StartCoroutine(ActionRoutine(view, other, to, result));
                    else StartCoroutine(MergeRoutine(view, other, to, result));
                    break;

                default:
                    if (tree.Relaxed) Sfx.Play("click", 0.4f);   // rahat modda sert hata sesi yok
                    else Sfx.Play("wrong", 0.6f);
                    StartCoroutine(ReturnToSlot(from, view));
                    break;
            }
        }

        void Discard(int from, RectTransform view)
        {
            var result = tree.Discard(from);
            if (result.Kind != MoveKind.Discarded)
            {
                Sfx.Play("wrong", 0.6f);
                hud.PunchBasket();
                StartCoroutine(ReturnToSlot(from, view));
                return;
            }
            views[from] = null;
            if (tree.Relaxed) StartCoroutine(DiscardAction(view));
            else StartCoroutine(IntoBasket(view));
        }

        void UseBooster()
        {
            if (tree.GameOver || paused || busy > 0 || dragSlot >= 0) return;
            if (tree.Boosters <= 0 || !tree.FindPair(out int from, out int to))
            {
                Sfx.Play("wrong", 0.6f);
                return;
            }
            if (views[from] == null || !tree.SpendBooster()) return;
            var view = views[from].rectTransform;
            view.SetParent(hud.DragLayer, true);
            StartCoroutine(FlyThenApply(from, to, view));
        }

        IEnumerator FlyThenApply(int from, int to, RectTransform view)
        {
            busy++;
            yield return MoveTo(view, to, 0.3f, null, arc: 140f);
            busy--;
            Apply(from, to, view);
        }

        // Editör testi: bilgisayar bir çift birleştirir (DevTools'tan çağrılır).
        public bool AutoMove()
        {
            if (tree == null || tree.GameOver || paused || busy > 0 || dragSlot >= 0 || actionRunning) return false;
            if (!tree.FindPair(out int from, out int to) || views[from] == null) return false;
            Deselect();
            var view = views[from].rectTransform;
            view.SetParent(hud.DragLayer, true);
            StartCoroutine(FlyThenApply(from, to, view));
            return true;
        }

        // ---- rahat Klasik: iki dokunuş + sürükle, otomatik zincir, hamle başına 1 meyve ----

        void RelaxedInput()
        {
            if (dragSlot >= 0)
            {
                if (PointerInput.Held) Drag(PointerInput.Position);
                else Release(PointerInput.Position);
                return;
            }
            if (PointerInput.Pressed)
            {
                pressActive = true;
                pressPos = PointerInput.Position;
                pressSlot = SlotAt(pressPos, filled: true, except: -1);
            }
            if (!pressActive) return;
            if (PointerInput.Held)
            {
                // yeterince kayarsa sürükleme başlar; bırakınca ayrıca dokunuş sayılmaz
                if (pressSlot >= 0 && busy == 0 && !actionRunning && views[pressSlot] != null &&
                    (PointerInput.Position - pressPos).magnitude > TapSlop * hud.Scale)
                {
                    pressActive = false;
                    Deselect();
                    BeginDrag(pressSlot);
                }
                return;
            }
            pressActive = false;
            OnTap(pressPos);
        }

        void OnTap(Vector2 screen)
        {
            if (hud.Basket.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(hud.Basket, screen, null))
            {
                if (selected >= 0 && !actionRunning && busy == 0 && views[selected] != null)
                {
                    int s = selected;
                    Deselect();
                    var v = views[s].rectTransform;
                    v.SetParent(hud.DragLayer, true);
                    Discard(s, v);
                }
                return;
            }
            int slot = SlotAt(screen, filled: true, except: -1);
            if (slot < 0 || views[slot] == null) { Deselect(); return; }
            if (selected < 0 || tree[slot] != tree[selected]) { Select(slot); return; }
            if (slot == selected) { Deselect(); return; }

            int from = selected;
            Deselect();
            if (actionRunning || busy > 0) { pendingAction = new Vector2Int(from, slot); return; }   // en fazla bir bekleyen
            ExecuteTap(from, slot);
        }

        void Select(int slot)
        {
            Deselect();
            selected = slot;
            if (selectRing == null)
            {
                selectRing = new GameObject("Selection", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                selectRing.sprite = FruitArt.Ring();
                selectRing.color = FruitArt.Gold;
                selectRing.raycastTarget = false;
                selectRing.rectTransform.sizeDelta = new Vector2(250, 250);
            }
            selectRing.transform.SetParent(slots[slot], false);
            selectRing.transform.SetAsFirstSibling();
            selectRing.rectTransform.anchoredPosition = Vector2.zero;
            selectRing.gameObject.SetActive(true);
            Sfx.Play("pickup", 0.6f);
            Haptics.Light();
        }

        void Deselect()
        {
            if (selected >= 0 && views != null && views[selected] != null) views[selected].rectTransform.localScale = Vector3.one;
            selected = -1;
            if (selectRing != null) selectRing.gameObject.SetActive(false);
        }

        void ExecuteTap(int from, int to)
        {
            if (tree == null || tree.GameOver || from == to || views[from] == null || tree[from] < 0 || tree[from] != tree[to]) return;
            var view = views[from].rectTransform;
            view.SetParent(hud.DragLayer, true);
            StartCoroutine(TapMove(from, to, view));
        }

        IEnumerator TapMove(int from, int to, RectTransform view)
        {
            busy++;
            yield return MoveTo(view, to, 0.14f, null, arc: 40f);
            busy--;
            Apply(from, to, view);
        }

        // Bir oyuncu hamlesi: birleşme → komşu eşlerle otomatik zincir → 1 yeni meyve → (varsa) bekleyen hamle.
        IEnumerator ActionRoutine(RectTransform moving, Image other, int to, MoveResult result)
        {
            actionRunning = true;
            yield return MergeRoutine(moving, other, to, result, step: 1, pop: 0.12f);
            int step = 1;
            while (result.Kind == MoveKind.Merged)
            {
                yield return WaitUnpaused(ChainStepDelay);
                if (tree == null || tree.GameOver || !tree.ChainStep(to, out int partner, out var next)) break;
                step++;
                var pv = views[partner];
                var tv = views[to];
                views[partner] = null;
                views[to] = null;
                if (pv != null) pv.rectTransform.SetParent(hud.DragLayer, true);
                if (selected == partner || selected == to) Deselect();
                result = next;
                yield return MergeRoutine(pv != null ? pv.rectTransform : null, tv, to, next, step, pop: 0.12f);
            }
            comboShowUntil = Time.time + 1.2f;
            while (busy > 0) yield return null;
            yield return FinishActionRoutine();
        }

        IEnumerator DiscardAction(RectTransform view)
        {
            actionRunning = true;
            yield return IntoBasket(view);
            yield return FinishActionRoutine();
        }

        IEnumerator FinishActionRoutine()
        {
            while (paused) yield return null;
            if (tree != null) tree.FinishAction();   // 1 meyve (gerekirse çift oluşana dek); yer ve hamle yoksa oyun biter
            actionRunning = false;
            if (pendingAction is Vector2Int p)
            {
                pendingAction = null;
                yield return null;
                if (tree != null && !tree.GameOver && views[p.x] != null && views[p.y] != null && tree[p.x] >= 0 && tree[p.x] == tree[p.y])
                    ExecuteTap(p.x, p.y);
            }
        }

        IEnumerator WaitUnpaused(float seconds)
        {
            for (float t = 0; t < seconds;)
            {
                if (!paused) t += Time.deltaTime;
                yield return null;
            }
        }

        void TogglePause()
        {
            if (tree == null || tree.GameOver) return;
            paused = !paused;
            hud.ShowPause(paused);
        }

        // ---- olaylar ----

        void OnSpawned(int slot, int tier)
        {
            // bölümde torbadan düşen meyve hamlenin içinde (Move/Discard sırasında) doğar; o anda hareket eden
            // meyvenin görüntüsü hâlâ bu yuvada kayıtlı olabilir. Hamle bitince düşsün diye kısa bekletilir.
            if (level != null) StartCoroutine(DropAfterMove(slot, tier));
            else StartCoroutine(DropIn(slot, tier, 0f));
        }

        IEnumerator DropAfterMove(int slot, int tier)
        {
            busy++;
            yield return null;
            yield return new WaitForSeconds(0.12f);
            busy--;
            if (tree != null && tree[slot] == tier) yield return DropIn(slot, tier, 0f);
        }

        // Hasatla seviye atlandı: ağaçta yeni bir yuva açıldı.
        void OnSlotUnlocked(int slot)
        {
            var at = slots[slot].position;
            StartCoroutine(Burst(at, FruitArt.Hex("#B6F28A"), 16));
            hud.Floating("YENİ YER!", at, Color.white, 44);
        }

        void OnQuestCompleted(Quest done)
        {
            Sfx.Play("order", 0.9f, 0f);
            Haptics.Success();
            hud.PunchQuest();
            hud.Floating($"GÖREV +{done.Bonus}", hud.TreeArea.position + Vector3.up * (420f * hud.Scale), FruitArt.Gold, 60);
            hud.SetQuest(tree.Quest);
        }

        // ---- bölüm modu ----

        // Buz meyvenin, yaprak boş yerin üstüne örtü olarak konur (yuvanın son çocuğu: meyvenin önünde).
        void BuildOverlays()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (tree.IceAt(i) > 0) overlays[i] = MakeOverlay(i, ice: true, tree.IceAt(i));
                else if (tree.LeafAt(i) > 0) overlays[i] = MakeOverlay(i, ice: false, tree.LeafAt(i));
            }
        }

        Image MakeOverlay(int slot, bool ice, int count)
        {
            var sprite = FruitArt.Load(ice ? "Art/obstacle_ice" : "Art/obstacle_leaf");
            var img = new GameObject(ice ? "Ice" : "Leaf", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(slots[slot], false);
            img.raycastTarget = false;
            img.sprite = sprite != null ? sprite : FruitArt.Circle();
            img.color = sprite != null ? Color.white : ice ? new Color(0.75f, 0.92f, 1f, 0.6f) : FruitArt.Hex("#5DA34A");
            img.preserveAspect = true;
            img.rectTransform.sizeDelta = new Vector2(230, 230);

            // kalan birleşme sayısı: küçük rozet
            var badge = new GameObject("Badge", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            badge.transform.SetParent(img.transform, false);
            badge.sprite = FruitArt.Circle();
            badge.color = ice ? FruitArt.Hex("#2E86C1") : FruitArt.Hex("#3E7D2C");
            badge.raycastTarget = false;
            var brt = badge.rectTransform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.85f, 0.15f);
            brt.sizeDelta = new Vector2(64, 64);
            var label = new GameObject("Count", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(badge.transform, false);
            label.font = FruitArt.Font(Weight.Bold);
            label.fontSize = 34;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = count.ToString();
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;

            StartCoroutine(PopScale(img.rectTransform, 0.3f, 0.2f + slot * 0.03f));
            return img;
        }

        void OnIceChanged(int slot, int left) => UpdateOverlay(slot, left, ice: true);
        void OnLeafChanged(int slot, int left) => UpdateOverlay(slot, left, ice: false);

        void UpdateOverlay(int slot, int left, bool ice)
        {
            var o = overlays[slot];
            if (o == null) return;
            var label = o.GetComponentInChildren<Text>();
            if (label != null) label.text = left.ToString();
            if (ice && left == 1)
            {
                var crack = FruitArt.Load("Art/obstacle_ice_crack");
                if (crack != null) o.sprite = crack;
            }
            if (left > 0) { StartCoroutine(Shake(o.rectTransform)); return; }

            // çözüldü / açıldı: parçalara ayrılıp kaybolur
            overlays[slot] = null;
            var at = slots[slot].position;
            var bits = new[] { FruitArt.Load(ice ? "Art/fx_ice_shard" : "Art/fx_leaf_bit") };
            StartCoroutine(Burst(at, ice ? new Color(0.8f, 0.95f, 1f) : FruitArt.Hex("#7BC96F"), 14, bits));
            Sfx.Play(ice ? "clear" : "step", 0.8f, 0.05f);
            Haptics.Light();
            hud.Floating(ice ? "BUZ ÇÖZÜLDÜ!" : "YER AÇILDI!", at + Vector3.up * (80f * hud.Scale), Color.white, 40);
            StartCoroutine(FadeAway(o));
        }

        void OnGoalProgress(int goal)
        {
            // hedef meyvesi hedef kutusuna uçar; sayaç uçuş bitince güncellenir
            int tier = level.goals[goal].tier;
            int slot = -1;
            for (int i = 0; i < slots.Length; i++) if (tree[i] == tier) slot = i;
            StartCoroutine(FlyToGoal(goal, tier, slot >= 0 ? slots[slot].position : hud.TreeArea.position));
        }

        IEnumerator FlyToGoal(int goal, int tier, Vector3 from)
        {
            var have = (int[])tree.GoalHave.Clone();
            busy++;
            yield return new WaitForSeconds(0.15f);
            var icon = hud.MakeFruit(hud.DragLayer, tier);
            icon.rectTransform.position = from;
            Sfx.Play("fly", 0.6f);
            yield return FlyAndVanish(icon.rectTransform, hud.GoalCell(goal).position, 0.45f);
            hud.SetGoals(level, have);
            hud.PunchGoal(goal);
            if (have[goal] >= level.goals[goal].count) { Sfx.Play("order", 0.8f, 0f); Haptics.Success(); }
            busy--;
        }

        // Bir süre dokunulmazsa birleşebilecek iki meyve nabız gibi atar (ilk bölümlerde daha erken).
        void Hint()
        {
            if (busy > 0 || dragSlot >= 0) { idleTime = 0f; return; }
            idleTime += Time.deltaTime;
            float wait = level.id == 1 ? 1f : level.id <= 3 ? 2.5f : level.id <= 10 ? 5f : 8f;
            if (idleTime < wait) return;
            if (hintA < 0 && tree.FindPair(out int a, out int b)) { hintA = a; hintB = b; }
            if (hintA < 0) return;
            float pulse = 1f + 0.12f * Mathf.Max(0f, Mathf.Sin(Time.time * 6f));
            if (views[hintA] != null) views[hintA].rectTransform.localScale = Vector3.one * pulse;
            if (views[hintB] != null) views[hintB].rectTransform.localScale = Vector3.one * pulse;
            if (level.id <= 3) MoveHand(slots[hintA].position, slots[hintB].position);
        }

        // Öğretici el: meyveden meyveye sürükleme hareketini gösterir (ilk bölümler).
        void MoveHand(Vector3 from, Vector3 to)
        {
            if (hand == null)
            {
                var sprite = FruitArt.Load("Art/hand_pointer");
                if (sprite == null) return;
                hand = new GameObject("Hand", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                hand.sprite = sprite;
                hand.raycastTarget = false;
                hand.preserveAspect = true;
                hand.rectTransform.sizeDelta = new Vector2(170, 170);
                hand.rectTransform.pivot = new Vector2(0.62f, 0.92f);   // işaret parmağının ucu
            }
            hand.transform.SetParent(hud.DragLayer, false);
            hand.transform.SetAsLastSibling();
            hand.gameObject.SetActive(true);
            // 0–0.25 bas, 0.25–1.1 sürükle, 1.1–1.5 bekle
            float t = Mathf.Repeat(Time.time, 1.5f);
            float k = Mathf.Clamp01((t - 0.25f) / 0.85f);
            var offset = Vector3.down * (40f * hud.Scale);
            hand.rectTransform.position = Vector3.Lerp(from, to, Ease.OutQuad(k)) + offset;
            hand.rectTransform.localScale = Vector3.one * (t < 0.25f ? 1.1f - 0.4f * t : 1f);
            hand.canvasRenderer.SetAlpha(t > 1.2f ? 1f - (t - 1.2f) / 0.3f : 1f);
        }

        Sprite[] confetti;
        Sprite[] Confetti()
        {
            if (confetti != null) return confetti;
            confetti = new[] { FruitArt.Load("Art/confetti_1"), FruitArt.Load("Art/confetti_2"), FruitArt.Load("Art/confetti_3") };
            if (confetti[0] == null) confetti = null;
            return confetti;
        }

        void ClearHint()
        {
            if (hand != null) hand.gameObject.SetActive(false);
            if (hintA >= 0 && views[hintA] != null) views[hintA].rectTransform.localScale = Vector3.one;
            if (hintB >= 0 && views[hintB] != null) views[hintB].rectTransform.localScale = Vector3.one;
            hintA = hintB = -1;
        }

        IEnumerator LevelEndRoutine()
        {
            gameOverShown = true;
            if (dragView != null) { Destroy(dragView.gameObject); dragView = null; dragSlot = -1; }
            ClearHint();
            if (tree.Won)
            {
                int stars = tree.Stars;
                LevelProgress.Complete(level.id, stars);
                yield return new WaitForSeconds(0.4f);
                Sfx.Play("win", 0.9f, 0f);
                Haptics.Success();
                // konfeti: ağacın üstünde birkaç patlama
                for (int i = 0; i < 4; i++)
                {
                    var at = hud.TreeArea.position + new Vector3(Random.Range(-380f, 380f), Random.Range(200f, 900f)) * hud.Scale;
                    StartCoroutine(Burst(at, FruitArt.Colors[Random.Range(0, FruitArt.Colors.Length)], 18, Confetti()));
                    yield return new WaitForSeconds(0.12f);
                }
                yield return new WaitForSeconds(0.3f);
                hud.ShowWin(level, stars, tree.GoalHave, LevelLibrary.Get(level.id + 1) != null);
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
                Sfx.Play("lose", 0.8f, 0f);
                Haptics.Heavy();
                hud.ShowLose(level, tree.MovesLeft == 0 ? "HAMLE BİTTİ" : "ÇİFT KALMADI");
            }
        }

        IEnumerator Shake(RectTransform rt)
        {
            var basePos = rt.anchoredPosition;
            for (float t = 0; t < 0.25f && rt != null; t += Time.deltaTime)
            {
                rt.anchoredPosition = basePos + Vector2.right * (12f * Ease.Punch(t / 0.25f, 4f));
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = basePos;
        }

        IEnumerator FadeAway(Image img)
        {
            var rt = img.rectTransform;
            for (float t = 0; t < 0.3f && img != null; t += Time.deltaTime)
            {
                float k = t / 0.3f;
                rt.localScale = Vector3.one * (1f + 0.3f * k);
                img.canvasRenderer.SetAlpha(1f - k);
                foreach (var g in img.GetComponentsInChildren<Graphic>()) g.canvasRenderer.SetAlpha(1f - k);
                yield return null;
            }
            if (img != null) Destroy(img.gameObject);
        }

        static IEnumerator PopScale(RectTransform rt, float duration, float delay)
        {
            rt.localScale = Vector3.zero;
            yield return new WaitForSeconds(delay);
            for (float t = 0; t < duration && rt != null; t += Time.deltaTime)
            {
                rt.localScale = Vector3.one * Mathf.LerpUnclamped(0f, 1f, Ease.OutBack(t / duration, 2f));
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }

        // Küçük meyveler (kiraz→üzüm) hafif, tiz bir "pop"; büyükler (portakal→armut) dolgun, kalın bir ses;
        // karpuz oluşunca kutlama. merge_small / merge_big / merge_melon dosyaları yoksa mevcut seslere düşer.
        void PlayMergeSound(int newTier, int step = 1)
        {
            float chainPitch = Mathf.Min(1.3f, 1f + 0.07f * (step - 1));   // zincir ilerledikçe hafif tizleşir, tavanlı
            if (newTier >= FruitChain.MaxTier)
            {
                Sfx.Play(Sfx.Has("merge_melon") ? "merge_melon" : "order", 0.9f, 0f);
                Haptics.Success();
            }
            else if (newTier >= 4)
            {
                // tier 4→6: perde her seviyede biraz düşer, ses "ağırlaşır"
                Sfx.Play(Sfx.Has("merge_big") ? "merge_big" : "clear", 0.85f, 0.03f, 0f, (1.1f - (newTier - 4) * 0.1f) * chainPitch);
                Haptics.Success();
            }
            else
            {
                // tier 1→3: kiraz birleşmesi en tiz
                Sfx.Play(Sfx.Has("merge_small") ? "merge_small" : "drop", 0.8f, 0.04f, 0f, (1.15f - (newTier - 1) * 0.07f) * chainPitch);
                Haptics.Light();
            }
        }

        // ---- animasyonlar ----

        IEnumerator MergeRoutine(RectTransform moving, Image other, int to, MoveResult result, int step = 1, float pop = 0.28f)
        {
            busy++;
            if (moving != null) yield return MoveTo(moving, to, 0.08f, null);
            if (moving != null) Destroy(moving.gameObject);
            if (other != null) Destroy(other.gameObject);

            var at = slots[to].position;
            if (result.Kind == MoveKind.Harvested)
            {
                Sfx.Play("win", 0.8f, 0f);
                Haptics.Success();
                StartCoroutine(Burst(at, FruitArt.Gold, 22));
                var melon = hud.MakeFruit(hud.DragLayer, FruitChain.MaxTier);
                melon.rectTransform.position = at;
                hud.Floating($"HASAT +{result.Points}", at, FruitArt.Gold, 66);
                busy--;
                yield return FlyAndVanish(melon.rectTransform, hud.HarvestIcon.position, 0.5f);
                hud.PunchHarvest();
                yield break;
            }

            var view = hud.MakeFruit(slots[to], result.NewTier);
            views[to] = view;
            StartCoroutine(Burst(at, FruitArt.Colors[result.NewTier], 12));
            PlayMergeSound(result.NewTier, step);
            if (result.Combo >= (tree.Relaxed ? 2 : 3)) Sfx.Play("combo", 0.5f, 0.05f, 0.3f);
            hud.Floating($"+{result.Points}", at + Vector3.up * (50f * hud.Scale), Color.white);
            hud.PunchScore();
            if (result.Combo != lastComboShown && result.Combo >= 2) hud.PunchCombo();
            lastComboShown = result.Combo;
            if (level == null && tree.Quest != null) hud.SetQuest(tree.Quest);
            busy--;

            // yeni meyve: büyüyerek "pop" eder
            var rt = view.rectTransform;
            for (float t = 0; t < pop && rt != null; t += Time.deltaTime)
            {
                rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.4f, 1f, Ease.OutBack(t / pop, 3f));
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }

        // Birleşme efekti: genişleyen beyaz halka + meyve renginde saçılan taneler.
        IEnumerator Burst(Vector3 at, Color color, int count, Sprite[] pieces = null)
        {
            var ring = new GameObject("Ring", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            ring.transform.SetParent(hud.DragLayer, false);
            ring.sprite = FruitArt.Circle();
            ring.raycastTarget = false;
            ring.rectTransform.position = at;

            var dots = new Image[count];
            var dirs = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                var d = new GameObject("Spark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                d.transform.SetParent(hud.DragLayer, false);
                d.raycastTarget = false;
                bool piece = pieces != null && pieces.Length > 0 && pieces[i % pieces.Length] != null;
                d.sprite = piece ? pieces[i % pieces.Length] : FruitArt.Circle();
                d.color = piece ? Color.white : i % 3 == 0 ? Color.white : color;
                d.rectTransform.localEulerAngles = new Vector3(0, 0, Random.Range(0f, 360f));
                float size = piece ? Random.Range(34f, 52f) : Random.Range(16f, 30f);
                d.rectTransform.sizeDelta = new Vector2(size, size);
                d.rectTransform.position = at;
                float a = (i + Random.value * 0.5f) / count * Mathf.PI * 2f;
                dirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(110f, 190f);
                dots[i] = d;
            }

            const float duration = 0.45f;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                float s = hud.Scale;
                ring.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(60f, 260f, Ease.OutCubic(k));
                ring.color = new Color(1, 1, 1, 0.55f * (1f - k));
                for (int i = 0; i < count; i++)
                {
                    var rt = dots[i].rectTransform;
                    rt.position = at + (Vector3)(dirs[i] * Ease.OutCubic(k) * s) + Vector3.down * (k * k * 120f * s);
                    var c = dots[i].color;
                    c.a = 1f - k * k;
                    dots[i].color = c;
                    rt.localScale = Vector3.one * (1f - 0.5f * k);
                }
                yield return null;
            }
            Destroy(ring.gameObject);
            foreach (var d in dots) Destroy(d.gameObject);
        }

        IEnumerator DropIn(int slot, int tier, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (views[slot] != null) Destroy(views[slot].gameObject);
            var view = hud.MakeFruit(slots[slot], tier);
            views[slot] = view;
            view.transform.SetAsFirstSibling();   // buz örtüsü önde kalsın
            var rt = view.rectTransform;
            var end = Vector2.zero;
            var start = end + Vector2.up * 260f;
            Sfx.Play("step", 0.35f, 0.15f, 0.08f);

            const float fall = 0.28f;
            for (float t = 0; t < fall && rt != null; t += Time.deltaTime)
            {
                rt.anchoredPosition = Vector2.LerpUnclamped(start, end, Ease.InQuad(t / fall));
                view.canvasRenderer.SetAlpha(Mathf.Clamp01(t / fall * 3f));
                yield return null;
            }
            if (rt == null) yield break;
            rt.anchoredPosition = end;
            view.canvasRenderer.SetAlpha(1f);
            // yere değince ezilip yaylanır
            for (float t = 0; t < 0.22f && rt != null; t += Time.deltaTime)
            {
                float p = Ease.Punch(t / 0.22f);
                rt.localScale = new Vector3(1f + 0.18f * p, 1f - 0.18f * p, 1f);
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }

        IEnumerator ReturnToSlot(int slot, RectTransform view)
        {
            busy++;
            yield return MoveTo(view, slot, 0.2f, null, arc: 60f);
            busy--;
            Settle(slot);
        }

        IEnumerator IntoBasket(RectTransform view)
        {
            Sfx.Play("fly", 0.7f);
            hud.PunchBasket();
            yield return FlyAndVanish(view, hud.Basket.position + Vector3.up * (60f * hud.Scale), 0.25f);
        }

        // Görüntüyü yuvasına yerleştirir (sürükleme katmanından ağaca geri).
        void Settle(int slot)
        {
            var v = views[slot];
            if (v == null) return;
            var rt = v.rectTransform;
            rt.SetParent(slots[slot], false);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        IEnumerator MoveTo(RectTransform view, int slot, float duration, System.Action done, float arc = 0f)
        {
            if (view == null) yield break;
            var start = view.position;
            var startScale = view.localScale;
            for (float t = 0; t < duration && view != null; t += Time.deltaTime)
            {
                float k = Ease.OutQuad(t / duration);
                var end = slots[slot].position;
                view.position = Vector3.Lerp(start, end, k) + Vector3.up * (Ease.Bump(k) * arc * hud.Scale);
                view.localScale = Vector3.Lerp(startScale, Vector3.one, k);
                yield return null;
            }
            if (view == null) yield break;
            view.position = slots[slot].position;
            view.localScale = Vector3.one;
            done?.Invoke();
        }

        static IEnumerator FlyAndVanish(RectTransform view, Vector3 target, float duration)
        {
            if (view == null) yield break;
            var start = view.position;
            for (float t = 0; t < duration && view != null; t += Time.deltaTime)
            {
                float k = Ease.InQuad(t / duration);
                view.position = Vector3.Lerp(start, target, k);
                view.localScale = Vector3.one * Mathf.Lerp(1.1f, 0.3f, k);
                yield return null;
            }
            if (view != null) Destroy(view.gameObject);
        }

        IEnumerator GameOverRoutine()
        {
            gameOverShown = true;
            if (dragView != null) { Destroy(dragView.gameObject); dragView = null; dragSlot = -1; }
            Sfx.Play("lose", 0.9f, 0f);
            Haptics.Heavy();
            hud.SetWarning(false, 0f);
            yield return new WaitForSeconds(0.8f);
            bool newBest = tree.Score > bestAtStart;
            if (tree.Score > Best) Best = tree.Score;
            PlayerPrefs.Save();
            hud.ShowGameOver(tree.Score, Best, newBest);
        }

        // ---- yardımcılar ----

        // Ekran noktasına en yakın yuva (meyve boyunun ~yarısı kadar tolerans).
        int SlotAt(Vector2 screen, bool? filled, int except)
        {
            int best = -1;
            float bestDist = float.MaxValue;
            float radius = 122f * hud.Scale;   // yuvalar arası en az 250: dokunma alanları çakışmaz
            for (int i = 0; i < slots.Length; i++)
            {
                if (i == except) continue;
                if (!tree.IsActive(i) || (filled == true && tree.IsEmpty(i))) continue;
                float d = Vector2.Distance(screen, slots[i].position);
                if (d < radius && d < bestDist) { best = i; bestDist = d; }
            }
            return best;
        }

        static void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FruitArt.Hex("#CFEFD8");
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
        }

        static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
