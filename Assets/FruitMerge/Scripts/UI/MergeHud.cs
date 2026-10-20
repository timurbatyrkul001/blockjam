using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FruitMerge
{
    // Ekran yazıları ve butonlar; koddan kurulur. Yerleşim reference_review/gameplay_mockup.png'ye göre
    // (dikey, 1080 genişlik). Kenney Future fontunda Ş, Ğ, İ yok: metinler bu harfler olmadan yazıldı.
    public partial class MergeHud : MonoBehaviour
    {
        public RectTransform Root { get; private set; }         // güvenli alan (çentik dışı)
        public RectTransform TreeArea { get; private set; }
        public RectTransform FruitLayer { get; private set; }
        public RectTransform DragLayer { get; private set; }
        public RectTransform Basket { get; private set; }
        public RectTransform HarvestIcon { get; private set; }
        public Image TreeTint { get; private set; }
        public float Scale => canvas.scaleFactor;

        public event Action BoosterPressed, RestartPressed, PausePressed, MapPressed, MenuPressed;

        const string SoundKey = "fm.sound", MusicKey = "fm.music";

        Canvas canvas;
        CanvasScaler scaler;
        Text scoreText, bestText, comboText, questText, questCount, harvestCount, basketCount, boosterCount,
            warningText, nextText, levelText;
        Image questIcon, questFill, nextFill, comboBadge, boosterIcon;
        Sprite panelSprite, roundButton, badgeSprite, greenButton;
        RectTransform questCard, comboRect, scoreRect, boosterRect;
        GameObject gameOverPanel, pausePanel;
        RectTransform gameOverCard;
        Text gameOverScore, gameOverBest;
        Image nextBack, nextFruitIcon;
        RectTransform nextFruitBox;
        readonly System.Collections.Generic.List<Image> musicButtons = new(), soundButtons = new();
        GameObject[] endlessOnly;
        Text pauseExitLabel;
        bool levelMode;

        public static MergeHud Create()
        {
            var go = new GameObject("FruitMergeUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 2340);
            var hud = go.AddComponent<MergeHud>();
            hud.canvas = c;
            hud.scaler = scaler;
            hud.FitScreen();
            hud.Build();
            return hud;
        }

        // Uzun ekranda genişliğe, kısa/geniş ekranda (ör. iPhone SE, iPad) yüksekliğe uyar:
        // 1080x2340 tasarım hiçbir ekranda dikeyde taşmaz, yatayda kesilmez.
        void FitScreen()
        {
            float aspect = (float)Screen.height / Mathf.Max(1, Screen.width);
            scaler.matchWidthOrHeight = aspect >= 2340f / 1080f ? 0f : 1f;
        }

        int lastW, lastH;
        void Update()
        {
            if (Screen.width == lastW && Screen.height == lastH) return;
            lastW = Screen.width;
            lastH = Screen.height;
            FitScreen();
        }

        // ---- güncellemeler ----

        public void SetScore(int score, int best)
        {
            scoreText.text = Fmt(score);
            bestText.text = $"REKOR {Fmt(best)}";
        }

        // Türkçe binlik ayırıcı: 15.628 (cihaz diline bağlı kalmasın).
        public static string Fmt(int n) => n.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', '.');

        public void SetNextFruit(int tier) => SetFruitImage(nextFruitIcon, tier);

        // Rahat Klasik: geri sayım çubuğu yok; sıradaki meyve kutusunun yanında yalnız başlık.
        public void SetRelaxedClassic(bool relaxed)
        {
            nextBack.gameObject.SetActive(!relaxed);
            nextText.rectTransform.anchoredPosition = relaxed ? new Vector2(480, 140) : new Vector2(500, 178);
        }

        public void SetCombo(int combo, float timeLeft01)
        {
            comboText.text = combo >= 2 ? $"{combo}×" : "—";
            // kombo süresi azaldıkça rozet hafifçe solar
            var from = comboBadge.sprite != null ? new Color(0.75f, 0.75f, 0.75f) : FruitArt.Hex("#8C2F39");
            var to = comboBadge.sprite != null ? Color.white : FruitArt.Hex("#C2185B");
            comboBadge.color = Color.Lerp(from, to, combo >= 2 ? timeLeft01 : 0f);
        }

        public void SetQuest(Quest q)
        {
            questText.text = $"{q.Need} TANE YAP";
            questCount.text = $"{q.Have}/{q.Need}";
            questFill.fillAmount = q.Need > 0 ? (float)q.Have / q.Need : 0f;
            SetFruitImage(questIcon, q.Tier);
        }

        public void SetNext(float t01, float secondsLeft)
        {
            nextFill.fillAmount = Mathf.Clamp01(t01);
        }

        public void SetBaskets(int n) => basketCount.text = n.ToString();
        public void SetHarvests(int n, int level)
        {
            harvestCount.text = n.ToString();
            levelText.text = $"SEVİYE {level}";
        }

        public void SetBoosters(int n)
        {
            boosterCount.text = n.ToString();
            var c = boosterIcon.color;
            c.a = n > 0 ? 1f : 0.5f;
            boosterIcon.color = c;
        }

        public void SetWarning(bool on, float danger01)
        {
            warningText.gameObject.SetActive(on);
            float blink = Mathf.PingPong(Time.unscaledTime * (3f + danger01 * 5f), 1f);
            if (on) warningText.color = new Color(1, 1, 1, 0.6f + 0.4f * blink);
            TreeTint.color = on ? new Color(1f, 0.15f, 0.15f, (0.08f + 0.2f * danger01) * blink) : Color.clear;
        }

        public void PunchQuest() => StartCoroutine(Punch(questCard, 0.4f, 0.15f));
        public void PunchCombo() => StartCoroutine(Punch(comboRect, 0.3f, 0.2f));
        public void PunchScore() => StartCoroutine(Punch(scoreRect, 0.25f, 0.08f));
        public void PunchBasket() => StartCoroutine(Punch(Basket, 0.3f, 0.15f));
        public void PunchHarvest() => StartCoroutine(Punch(HarvestIcon, 0.4f, 0.25f));
        public void PunchBooster() => StartCoroutine(Punch(boosterRect, 0.4f, 0.25f));

        // Ekranda yukarı süzülüp kaybolan yazı (puan, görev, hasat).
        public void Floating(string text, Vector2 screenPos, Color color, int size = 64)
        {
            var label = MakeLabel("Floating", DragLayer, size, TextAnchor.MiddleCenter, shadow: true);
            label.text = text;
            label.color = color;
            var rt = label.rectTransform;
            rt.sizeDelta = new Vector2(800, 120);
            rt.position = screenPos;
            StartCoroutine(FloatAway(label));
        }

        public void ShowGameOver(int score, int best, bool newBest)
        {
            gameOverScore.text = $"SKOR {Fmt(score)}";
            gameOverBest.text = newBest ? "YENİ REKOR!" : $"REKOR {Fmt(best)}";
            gameOverBest.color = newBest ? FruitArt.Hex("#E08A00") : FruitArt.Hex("#8A6A4F");
            gameOverPanel.SetActive(true);
            gameOverPanel.transform.SetAsLastSibling();
            StartCoroutine(PopIn(gameOverCard));
        }

        public void HideGameOver() => gameOverPanel.SetActive(false);

        public void ShowPause(bool on)
        {
            pausePanel.SetActive(on);
            if (on) pausePanel.transform.SetAsLastSibling();
        }

        // Meyve görseli; yoksa renkli daire + seviye numarası.
        public void SetFruitImage(Image img, int tier)
        {
            if (tier == MergeTree.StoneTier)
            {
                var stone = FruitArt.Load("Art/obstacle_stone");
                img.sprite = stone != null ? stone : FruitArt.Circle();
                img.color = stone != null ? Color.white : FruitArt.Hex("#9A9087");
                img.preserveAspect = true;
                return;
            }
            var sprite = FruitArt.Fruit(tier);
            var label = img.transform.Find("Tier")?.GetComponent<Text>();
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.preserveAspect = true;
                if (label != null) label.gameObject.SetActive(false);
                return;
            }
            img.sprite = FruitArt.Circle();
            img.color = FruitArt.Colors[tier];
            if (label == null)
            {
                label = MakeLabel("Tier", img.transform, 44, TextAnchor.MiddleCenter, shadow: true);
                Stretch(label.rectTransform);
            }
            label.gameObject.SetActive(true);
            label.text = (tier + 1).ToString();
        }

        public Image MakeFruit(Transform parent, int tier)
        {
            var img = MakeImage($"Fruit{tier}", parent, Color.white);
            img.raycastTarget = false;
            float size = tier == MergeTree.StoneTier ? 170f : FruitArt.SizeFor(tier);
            img.rectTransform.sizeDelta = new Vector2(size, size);
            SetFruitImage(img, tier);
            return img;
        }

        // ---- kurulum ----

        void Build()
        {
            panelSprite = FruitArt.Load("Art/panel_wood") ?? FruitArt.Load("UI/panel");
            roundButton = FruitArt.Load("Art/button_round") ?? FruitArt.Load("UI/button_round_blue");
            badgeSprite = FruitArt.Load("Art/combo_badge");
            greenButton = FruitArt.Load("UI/button_green");

            // arka plan (tam ekran, güvenli alanın dışına da taşar)
            var bg = MakeImage("Background", transform, FruitArt.Hex("#CFEFD8"));
            Stretch(bg.rectTransform);
            var bgSprite = FruitArt.Load("Art/background");
            if (bgSprite != null) { bg.sprite = bgSprite; bg.color = Color.white; }
            bg.raycastTarget = false;

            // bahçe katmanı (çit, çalılar, çiçekler): ağacın arkasında, ekranın alt yarısında
            BuildBackScenery(transform);

            Root = MakeRect("SafeArea", transform);
            Stretch(Root);
            Root.gameObject.AddComponent<SafeArea>();

            BuildTree();

            // ---- Klasik/Rekor modu üst: skor+rekor kartı, duraklatma, kombo, altın karpuz (modes_design/record_mode_layout.png) ----
            var score = MakePanel("Score", Root, panelSprite);
            scoreRect = score.rectTransform;
            Place(scoreRect, new Vector2(0, 1), new Vector2(36, -40), new Vector2(340, 210), new Vector2(0, 1));
            var scoreLabel = MakeLabel("Label", scoreRect, 32, TextAnchor.MiddleCenter, false, Weight.SemiBold);
            scoreLabel.text = "SKOR";
            scoreLabel.color = FruitArt.Hex("#8A5A2B");
            Place(scoreLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -46), new Vector2(300, 42));
            scoreText = MakeLabel("Value", scoreRect, 68, TextAnchor.MiddleCenter, false, Weight.Bold);
            scoreText.color = FruitArt.DarkText;
            scoreText.resizeTextForBestFit = true;
            scoreText.resizeTextMinSize = 44;
            scoreText.resizeTextMaxSize = 68;
            scoreText.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(scoreText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -4), new Vector2(300, 80));
            bestText = MakeLabel("Best", scoreRect, 28, TextAnchor.MiddleCenter, false, Weight.SemiBold);
            bestText.color = FruitArt.Hex("#8A5A2B");
            Place(bestText.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 42), new Vector2(300, 38));

            // görev paneli: Klasik modda görev yok; bölüm dışı eski sonsuz mod için kurulu kalır ama gizlidir
            var quest = MakePanel("Quest", Root, panelSprite);
            questCard = quest.rectTransform;
            Place(questCard, new Vector2(0, 1), new Vector2(400, -40), new Vector2(480, 170), new Vector2(0, 1));
            questIcon = MakeImage("Icon", questCard, Color.white);
            questIcon.raycastTarget = false;
            Place(questIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(80, 0), new Vector2(92, 92));
            questText = MakeLabel("Text", questCard, 32, TextAnchor.MiddleCenter);
            questText.color = FruitArt.DarkText;
            Place(questText.rectTransform, new Vector2(0, 0.5f), new Vector2(290, 26), new Vector2(300, 44));
            var barBack = MakeImage("Bar", questCard, FruitArt.Hex("#6D4C30"));
            Place(barBack.rectTransform, new Vector2(0, 0.5f), new Vector2(290, -26), new Vector2(280, 34));
            questFill = MakeFill(barBack.rectTransform, FruitArt.Hex("#9BD24A"));
            questCount = MakeLabel("Count", barBack.rectTransform, 24, TextAnchor.MiddleCenter, shadow: true);
            Stretch(questCount.rectTransform);
            quest.gameObject.SetActive(false);

            var pause = MakeButton("Pause", Root, roundButton, FruitArt.Hex("#8BC34A"), () => PausePressed?.Invoke());
            Place(pause, new Vector2(1, 1), new Vector2(-40, -50), new Vector2(130, 130), new Vector2(1, 1));
            AddIcon(pause, "Art/icon_pause", "II", 64);

            comboBadge = MakeImage("Combo", Root, FruitArt.Hex("#8C2F39"));
            comboBadge.raycastTarget = false;
            if (badgeSprite != null) { comboBadge.sprite = badgeSprite; comboBadge.color = Color.white; }
            comboRect = comboBadge.rectTransform;
            Place(comboRect, new Vector2(0, 1), new Vector2(46, -270), new Vector2(200, 180), new Vector2(0, 1));
            var comboLabel = MakeLabel("Label", comboRect, 30, TextAnchor.MiddleCenter, true, Weight.Bold);
            comboLabel.text = "KOMBO";
            comboLabel.color = FruitArt.CreamText;
            Place(comboLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -38), new Vector2(190, 40));
            comboText = MakeLabel("Value", comboRect, 80, TextAnchor.MiddleCenter, shadow: true, Weight.Bold);
            comboText.color = FruitArt.CreamText;
            Place(comboText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(190, 96));

            var booster = MakeButton("Booster", Root, null, Color.clear, () => BoosterPressed?.Invoke());
            boosterRect = booster;
            Place(booster, new Vector2(1, 1), new Vector2(-45, -215), new Vector2(130, 130), new Vector2(1, 1));
            boosterIcon = booster.GetComponent<Image>();
            var boosterSprite = FruitArt.Load("Art/booster_melon");
            if (boosterSprite != null) { boosterIcon.sprite = boosterSprite; boosterIcon.color = Color.white; boosterIcon.preserveAspect = true; }
            else SetFruitImage(boosterIcon, FruitChain.MaxTier);
            boosterCount = MakeBadge(booster, "0");

            // ---- alt: sıradaki meyve kutusu + geliş çubuğu, müzik/ses ----
            var nextBox = MakePanel("NextFruit", Root, panelSprite);
            nextFruitBox = nextBox.rectTransform;
            Place(nextFruitBox, new Vector2(0, 0), new Vector2(46, 56), new Vector2(170, 170), new Vector2(0, 0));
            nextFruitIcon = MakeImage("Icon", nextFruitBox, Color.white);
            nextFruitIcon.raycastTarget = false;
            Place(nextFruitIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(118, 118));

            nextText = MakeLabel("NextLabel", Root, 32, TextAnchor.MiddleCenter, shadow: true, Weight.Bold);
            nextText.text = "SIRADAKİ MEYVE";
            nextText.color = FruitArt.CreamText;
            Place(nextText.rectTransform, new Vector2(0, 0), new Vector2(500, 178), new Vector2(520, 46));
            nextBack = MakeImage("NextBar", Root, FruitArt.Hex("#6D4C30"));
            Place(nextBack.rectTransform, new Vector2(0, 0), new Vector2(500, 120), new Vector2(480, 44));
            nextFill = MakeFill(nextBack.rectTransform, FruitArt.Hex("#9BD24A"));

            // hasat kutusu artık gösterilmez; hasat edilen karpuz skor kartına uçar
            var harvest = MakeImage("HarvestBox", Root, Color.clear);
            harvest.raycastTarget = false;
            Place(harvest.rectTransform, new Vector2(0, 0), new Vector2(50, 60), new Vector2(10, 10), new Vector2(0, 0));
            harvestCount = MakeLabel("Count", harvest.transform, 20, TextAnchor.MiddleCenter);
            levelText = MakeLabel("Level", harvest.transform, 20, TextAnchor.MiddleCenter);
            harvest.gameObject.SetActive(false);
            HarvestIcon = scoreRect;

            var music = MakeButton("Music", Root, roundButton, FruitArt.Hex("#8BC34A"), ToggleMusic);
            Place(music, new Vector2(1, 0), new Vector2(-170, 66), new Vector2(110, 110), new Vector2(1, 0));
            AddAudioButton(music, "Art/icon_music", "M", isMusic: true);
            var sound = MakeButton("Sound", Root, roundButton, FruitArt.Hex("#8BC34A"), ToggleSound);
            Place(sound, new Vector2(1, 0), new Vector2(-40, 66), new Vector2(110, 110), new Vector2(1, 0));
            AddAudioButton(sound, "Art/icon_sound", "S", isMusic: false);
            ApplyAudioSettings();

            warningText = MakeLabel("Warning", Root, 64, TextAnchor.MiddleCenter, shadow: true, Weight.Bold);
            warningText.text = "YER KALMADI!";
            Place(warningText.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -470), new Vector2(900, 90));
            warningText.gameObject.SetActive(false);

            DragLayer = MakeRect("DragLayer", Root);
            Stretch(DragLayer);

            // sadece Klasik modda görünenler (bölüm modunda yerlerine bölüm paneli gelir)
            endlessOnly = new[]
            {
                scoreRect.gameObject, comboRect.gameObject, boosterRect.gameObject,
                nextText.gameObject, nextBack.gameObject, nextFruitBox.gameObject,
            };
            BuildLevelHud();
            DragLayer.SetAsLastSibling();

            BuildPause();
            BuildGameOver(panelSprite, greenButton);
            BuildResult();
            BuildMap();
            BuildMenu();
        }

        // Ağaç: 1040 genişlik, alt kenarı ekran altından 420 (garden_preview.png ile aynı ölçüler).
        // Bahçe gibi ekran koordinatında durur; HUD ise güvenli alana göre yerleşir.
        // ---- bahçe (garden_parts): background → arka bahçe ağaçları → garden_base → ana ağaç → gölgeler → el arabası, sepet ----
        // Koordinatlar 1080x2340, SOL ALT köşeye göre [x, y, w, h] (CLAUDE_GARDEN_HANDOFF.md); sprite oranı korunur.
        public const float TreeWidth = 1250f, TreeBottom = 310f;   // yaprak bölgesi oyun alanı: ağaç büyük, kökler alt kenarda

        void BuildBackScenery(Transform parent)
        {
            Decor(parent, "Art/orchard_tree_left", 5, 640, 230, 350, 0.9f);
            Decor(parent, "Art/orchard_tree_right", 845, 640, 230, 350, 0.9f);
            var baseSprite = FruitArt.Load("Art/garden_base");
            if (baseSprite != null)
            {
                // geniş ekranda kenarlar boş kalmasın diye yatayda uzar
                var garden = MakeImage("GardenBase", parent, Color.white);
                garden.sprite = baseSprite;
                garden.raycastTarget = false;
                var rt = garden.rectTransform;
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(1, 0);
                rt.pivot = new Vector2(0.5f, 0);
                rt.sizeDelta = new Vector2(0, 900);
                rt.anchoredPosition = new Vector2(0, 450);
            }
        }

        // Ana ağacın önündeki süsler; sepet (oyun hedefi) ayrıca eklenir.
        RectTransform BuildFrontScenery(Transform parent)
        {
            var front = MakeRect("Front", parent);
            Stretch(front);
            Decor(front, "Art/ground_shadow", 45, 352, 255, 64, 0.8f);
            Decor(front, "Art/ground_shadow", 790, 352, 260, 64, 0.8f);
            Decor(front, "Art/wheelbarrow", 15, 365, 315, 210, 1f);
            return front;
        }

        Image Decor(Transform parent, string sprite, float x, float y, float w, float h, float alpha)
        {
            var s = FruitArt.Load(sprite);
            if (s == null) return null;
            var img = MakeImage(sprite.Substring(4), parent, new Color(1, 1, 1, alpha));
            img.sprite = s;
            img.preserveAspect = true;
            img.raycastTarget = false;
            Place(img.rectTransform, new Vector2(0.5f, 0), new Vector2(x + w / 2f - 540f, y), new Vector2(w, h), new Vector2(0.5f, 0));
            return img;
        }

        void BuildTree()
        {
            var treeSprite = FruitArt.Load("Art/tree");
            float aspect = treeSprite != null ? treeSprite.rect.width / treeSprite.rect.height : 1080f / 1500f;
            TreeArea = MakeRect("Tree", transform);
            TreeArea.SetSiblingIndex(Root.GetSiblingIndex());   // HUD'un arkasında
            Place(TreeArea, new Vector2(0.5f, 0), new Vector2(0, TreeBottom), new Vector2(TreeWidth, TreeWidth / aspect), new Vector2(0.5f, 0));

            if (treeSprite != null)
            {
                var tree = MakeImage("TreeImage", TreeArea, Color.white);
                tree.sprite = treeSprite;
                tree.raycastTarget = false;
                Stretch(tree.rectTransform);
            }
            else
            {
                var trunk = MakeImage("Trunk", TreeArea, FruitArt.Hex("#8D5A34"));
                trunk.raycastTarget = false;
                trunk.rectTransform.anchorMin = new Vector2(0.42f, 0f);
                trunk.rectTransform.anchorMax = new Vector2(0.58f, 0.5f);
                trunk.rectTransform.offsetMin = trunk.rectTransform.offsetMax = Vector2.zero;
                var crown = MakeImage("Leaves", TreeArea, FruitArt.Hex("#7DC36B"));
                crown.sprite = FruitArt.Circle();
                crown.raycastTarget = false;
                crown.rectTransform.anchorMin = new Vector2(0f, 0.3f);
                crown.rectTransform.anchorMax = new Vector2(1f, 1f);
                crown.rectTransform.offsetMin = crown.rectTransform.offsetMax = Vector2.zero;
            }

            TreeTint = MakeImage("Tint", TreeArea, Color.clear);
            TreeTint.raycastTarget = false;
            Stretch(TreeTint.rectTransform);

            // ön süsler ve sepet ağacın önünde, köklerden ayrı (sepetin bırakma alanı da onunla taşınır)
            var front = BuildFrontScenery(transform);
            front.SetSiblingIndex(Root.GetSiblingIndex());
            var basket = MakeImage("Basket", front, FruitArt.Hex("#C98B4A"));
            basket.raycastTarget = false;
            var basketSprite = FruitArt.Load("Art/basket");
            if (basketSprite != null) { basket.sprite = basketSprite; basket.color = Color.white; basket.preserveAspect = true; }
            Basket = basket.rectTransform;
            Place(Basket, new Vector2(0.5f, 0), new Vector2(775 + 145 - 540, 365), new Vector2(290, 225), new Vector2(0.5f, 0f));
            basketCount = MakeBadge(Basket, "3");

            FruitLayer = MakeRect("Fruits", TreeArea);
            Stretch(FruitLayer);
        }

        void BuildPause()
        {
            var dim = MakeImage("Pause", transform, new Color(0, 0, 0, 0.55f));
            Stretch(dim.rectTransform);
            pausePanel = dim.gameObject;

            var card = MakePanel("Card", dim.transform, panelSprite);
            Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 760));
            var label = MakeLabel("Label", card.transform, 80, TextAnchor.MiddleCenter, false, Weight.Bold);
            label.text = "MOLA";
            label.color = FruitArt.DarkText;
            Place(label.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(700, 110));

            var resume = MakeButton("Resume", card.transform, greenButton, FruitArt.Hex("#35C46A"), () => PausePressed?.Invoke());
            Place(resume, new Vector2(0.5f, 1), new Vector2(0, -290), new Vector2(560, 150));
            ButtonLabel(resume, "DEVAM ET", 48);
            var restart = MakeButton("Restart", card.transform, panelSprite, FruitArt.Cream, () => RestartPressed?.Invoke());
            Place(restart, new Vector2(0.5f, 1), new Vector2(0, -455), new Vector2(560, 120));
            ButtonLabel(restart, "TEKRAR OYNA", 40, FruitArt.DarkText);
            var exit = MakeButton("Exit", card.transform, panelSprite, FruitArt.Cream, () =>
            {
                if (levelMode) MapPressed?.Invoke();
                else MenuPressed?.Invoke();
            });
            Place(exit, new Vector2(0.5f, 1), new Vector2(0, -595), new Vector2(560, 120));
            pauseExitLabel = ButtonLabel(exit, "ANA MENÜ", 40, FruitArt.DarkText);
            pausePanel.SetActive(false);
        }

        Text ButtonLabel(RectTransform button, string text, int size, Color? color = null)
        {
            var label = MakeLabel("Label", button, size, TextAnchor.MiddleCenter, shadow: color == null, Weight.Bold);
            label.text = text;
            label.color = color ?? FruitArt.CreamText;
            Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 6), button.sizeDelta - new Vector2(20, 20));
            return label;
        }

        // Klasik mod sonucu: kompakt kart (karpuz, OYUN BİTTİ, skor, rekor, TEKRAR OYNA, ANA MENÜ).
        void BuildGameOver(Sprite panelSprite, Sprite greenButton)
        {
            var dim = MakeImage("GameOver", transform, new Color(0, 0, 0, 0.6f));
            Stretch(dim.rectTransform);
            gameOverPanel = dim.gameObject;

            var card = MakePanel("Card", dim.transform, panelSprite);
            gameOverCard = card.rectTransform;
            Place(gameOverCard, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 720));

            var icon = MakeImage("Melon", gameOverCard, Color.white);
            icon.raycastTarget = false;
            Place(icon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 40), new Vector2(190, 190));
            SetFruitImage(icon, FruitChain.MaxTier);

            var title = MakeLabel("Title", gameOverCard, 72, TextAnchor.MiddleCenter, false, Weight.Bold);
            title.text = "OYUN BİTTİ";
            title.color = FruitArt.DarkText;
            Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(780, 96));
            gameOverScore = MakeLabel("Score", gameOverCard, 56, TextAnchor.MiddleCenter, false, Weight.Bold);
            gameOverScore.color = FruitArt.DarkText;
            Place(gameOverScore.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -240), new Vector2(780, 76));
            gameOverBest = MakeLabel("Best", gameOverCard, 40, TextAnchor.MiddleCenter, false, Weight.SemiBold);
            Place(gameOverBest.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -312), new Vector2(780, 60));

            var again = MakeButton("Again", gameOverCard, greenButton, FruitArt.Hex("#35C46A"), () => RestartPressed?.Invoke());
            Place(again, new Vector2(0.5f, 0), new Vector2(0, 175), new Vector2(580, 150));
            ButtonLabel(again, "TEKRAR OYNA", 44);
            var menu = MakeButton("Menu", gameOverCard, null, Color.clear, () => MenuPressed?.Invoke());
            menu.GetComponent<Image>().color = new Color(1, 1, 1, 0.001f);
            Place(menu, new Vector2(0.5f, 0), new Vector2(0, 58), new Vector2(420, 80));
            ButtonLabel(menu, "ANA MENÜ", 38, FruitArt.DarkText);

            gameOverPanel.SetActive(false);
        }

        // ---- ses / müzik ----

        void ToggleSound()
        {
            PlayerPrefs.SetInt(SoundKey, PlayerPrefs.GetInt(SoundKey, 1) == 1 ? 0 : 1);
            ApplyAudioSettings();
            Sfx.Play("click", 0.8f);
        }

        void ToggleMusic()
        {
            PlayerPrefs.SetInt(MusicKey, PlayerPrefs.GetInt(MusicKey, 1) == 1 ? 0 : 1);
            ApplyAudioSettings();
        }

        // Müzik/ses düğmesi: kapalıyken soluk ve üstü çizili (iki düğme birbirinden bağımsız).
        void AddAudioButton(RectTransform button, string icon, string fallback, bool isMusic)
        {
            AddIcon(button, icon, fallback, 58);
            var slash = MakeImage("Off", button, FruitArt.Outline);
            slash.sprite = WhiteSprite();
            slash.raycastTarget = false;
            Place(slash.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92, 11));
            slash.rectTransform.localEulerAngles = new Vector3(0, 0, 45);
            var img = button.GetComponent<Image>();
            (isMusic ? musicButtons : soundButtons).Add(img);
        }

        void ApplyAudioSettings()
        {
            bool sound = PlayerPrefs.GetInt(SoundKey, 1) == 1;
            bool music = PlayerPrefs.GetInt(MusicKey, 1) == 1;
            Sfx.Muted = !sound;
            Music.Enabled = music;
            foreach (var b in soundButtons) ShowAudioState(b, sound);
            foreach (var b in musicButtons) ShowAudioState(b, music);
        }

        static void ShowAudioState(Image button, bool on)
        {
            if (button == null) return;
            button.color = on ? Color.white : new Color(0.62f, 0.62f, 0.6f, 1f);
            var icon = button.transform.Find("Icon");
            if (icon != null && icon.TryGetComponent<Graphic>(out var g)) g.color = new Color(1, 1, 1, on ? 1f : 0.55f);
            var off = button.transform.Find("Off");
            if (off != null) off.gameObject.SetActive(!on);
        }

        // ---- animasyonlar ----

        IEnumerator FloatAway(Text label)
        {
            var rt = label.rectTransform;
            var start = rt.anchoredPosition;
            const float duration = 0.9f;
            for (float t = 0; t < duration && rt != null; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                rt.anchoredPosition = start + Vector2.up * (Ease.OutCubic(k) * 140f);
                rt.localScale = Vector3.one * (k < 0.15f ? Ease.OutBack(k / 0.15f, 2.5f) : 1f);
                var c = label.color;
                c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                label.color = c;
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        static IEnumerator PopIn(RectTransform rt)
        {
            const float duration = 0.35f;
            for (float t = 0; t < duration && rt != null; t += Time.unscaledDeltaTime)
            {
                rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, Ease.OutBack(t / duration, 2f));
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }

        static IEnumerator Punch(RectTransform rt, float duration, float strength)
        {
            for (float t = 0; t < duration && rt != null; t += Time.unscaledDeltaTime)
            {
                rt.localScale = Vector3.one * (1f + strength * Ease.Punch(t / duration));
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
        }

        // ---- küçük uGUI yardımcıları ----

        static RectTransform MakeRect(string name, Transform parent)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        // Fredoka: büyük yazılar (44+) Bold, küçükler SemiBold. Gölge yalnız renkli zemin/gökyüzü üstündeki yazıya.
        Text MakeLabel(string name, Transform parent, int size, TextAnchor align, bool shadow = false, Weight? weight = null)
        {
            var font = FruitArt.Font(weight ?? (size >= 44 ? Weight.Bold : Weight.SemiBold));
            var t = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            t.transform.SetParent(parent, false);
            t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            if (shadow)
            {
                var sh = t.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0.39f, 0.21f, 0.12f, 0.55f);   // #63351E, hafif
                sh.effectDistance = new Vector2(0, size >= 44 ? -4 : -2);
            }
            return t;
        }

        // Butonun ortasına ikon; ikon dosyası yoksa yazı.
        Image AddIcon(RectTransform button, string path, string fallback, float size)
        {
            var sprite = FruitArt.Load(path);
            if (sprite == null)
            {
                var label = MakeLabel("Icon", button, 44, TextAnchor.MiddleCenter, shadow: true);
                label.text = fallback;
                Stretch(label.rectTransform);
                return null;
            }
            var icon = MakeImage("Icon", button, Color.white);
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(size, size));
            return icon;
        }

        static Image MakeImage(string name, Transform parent, Color color)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.color = color;
            return img;
        }

        static Image MakePanel(string name, Transform parent, Sprite sprite)
        {
            var img = MakeImage(name, parent, sprite != null ? Color.white : FruitArt.Cream);
            SetSliced(img, sprite);
            img.raycastTarget = false;
            return img;
        }

        static Sprite white;
        static Sprite WhiteSprite() =>
            white != null ? white : white = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);

        static Image MakeFill(RectTransform parent, Color color)
        {
            var fill = MakeImage("Fill", parent, color);
            fill.sprite = WhiteSprite();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;
            var rt = fill.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(4, 4);
            rt.offsetMax = new Vector2(-4, -4);
            return fill;
        }

        Text MakeBadge(RectTransform parent, string text)
        {
            var dot = MakeImage("Badge", parent, FruitArt.Hex("#E8572A"));
            dot.sprite = FruitArt.Circle();
            dot.raycastTarget = false;
            Place(dot.rectTransform, new Vector2(1, 1), new Vector2(-6, -6), new Vector2(56, 56));
            var label = MakeLabel("Count", dot.transform, 30, TextAnchor.MiddleCenter);
            label.text = text;
            Stretch(label.rectTransform);
            return label;
        }

        static RectTransform MakeButton(string name, Transform parent, Sprite sprite, Color fallback, Action onClick)
        {
            var img = MakeImage(name, parent, sprite != null ? Color.white : fallback);
            if (sprite != null) SetSliced(img, sprite);
            else img.sprite = FruitArt.Circle();
            img.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                Sfx.Play("click", 0.8f);
                onClick();
            });
            return img.rectTransform;
        }

        static void SetSliced(Image img, Sprite sprite)
        {
            if (sprite == null) return;
            img.sprite = sprite;
            img.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            // Codex'in ahşap paneli 48 px kalın çerçeveli; ekranda ince dursun diye küçültülür
            img.pixelsPerUnitMultiplier = sprite.name == "panel_wood" ? 2.2f : 0.8f;
        }
    }
}
