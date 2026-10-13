using System;
using System.Collections;
using System.Collections.Generic;
using PuzzleKit;
using UnityEngine;
using UnityEngine.UI;

namespace ColorBlockJam
{
    // Üst bar (level, süre, coin, yeniden başlat) + kazan/kaybet paneli. Tamamen koddan kurulur.
    public class Hud : MonoBehaviour
    {
        static readonly Color PillColor = new(0f, 0f, 0f, 0.35f);
        static readonly Color ButtonColor = Palette.Hex("#35C46A");
        static readonly Color DangerColor = Palette.Hex("#FF5C6C");
        static readonly Color CoinColor = Palette.Hex("#FFC43D");
        static readonly Color DarkTextColor = Palette.Hex("#2B3556");
        static readonly Color MutedTextColor = Palette.Hex("#6B7699");
        static readonly Color CoinTextColor = Palette.Hex("#E08A00");

        Font font;
        Text levelText;
        Text timerText;
        Text coinText;
        GameObject panel;
        RectTransform card;
        Image mascot;
        readonly List<Sprite> winMascots = new();
        Sprite loseMascot;
        Text panelTitle;
        Text panelSubtitle;
        Text panelButtonLabel;
        Action panelAction;
        Image timeBomb;
        bool mascotHops;
        static readonly Vector2 MascotRest = new(0, 40);

        public static Hud Create(GameManager game)
        {
            var go = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var hud = go.AddComponent<Hud>();
            hud.Build(game);
            return hud;
        }

        public void SetLevel(int number) => levelText.text = $"Level {number}";

        public void SetCoins(int amount) => coinText.text = amount.ToString();

        public void SetTime(float seconds)
        {
            int s = Mathf.CeilToInt(seconds);
            timerText.text = $"{s / 60}:{s % 60:00}";
            timerText.color = s <= 10 ? DangerColor : Color.white;
            if (timeBomb != null) timeBomb.enabled = s <= 10 && s > 0;
        }

        void Update()
        {
            // kazanınca karakter zeminin üstünde sevinçle zıplar
            if (mascotHops && panel.activeSelf)
            {
                float hop = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f));
                mascot.rectTransform.anchoredPosition = MascotRest + Vector2.up * (hop * 40f);
            }

            // son saniyelerde patlayıcı sandık nabız gibi atar
            if (timeBomb != null && timeBomb.enabled)
                timeBomb.rectTransform.localScale = Vector3.one * (1f + 0.15f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)));
        }

        // Not: Kenney Future fontunda ı/ş/ğ yok; ekrandaki metinler bu harfler olmadan seçildi.
        public void ShowResult(bool won, string subtitle, Action onButton)
        {
            panelTitle.text = won ? "Harika!" : "Süre bitti";
            mascotHops = won;
            mascot.rectTransform.anchoredPosition = MascotRest;
            panelSubtitle.text = subtitle;
            panelSubtitle.color = won ? CoinTextColor : MutedTextColor;
            panelButtonLabel.text = won ? "Sonraki level" : "Tekrar dene";
            panelAction = onButton;

            mascot.sprite = won && winMascots.Count > 0 ? winMascots[UnityEngine.Random.Range(0, winMascots.Count)] : loseMascot;
            mascot.enabled = mascot.sprite != null;

            panel.SetActive(true);
            StartCoroutine(PopIn(card, 0.6f, 0.35f));
            StartCoroutine(PopIn(mascot.rectTransform, 0f, 0.5f));
        }

        static IEnumerator PopIn(RectTransform rt, float from, float duration)
        {
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                rt.localScale = Vector3.one * Mathf.LerpUnclamped(from, 1f, Ease.OutBack(t / duration, 2f));
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        public void HidePanel() => panel.SetActive(false);

        void Build(GameManager game)
        {
            // Görseller, ses ve font: Assets/Art/Resources (Kenney, CC0 — Assets/Art/LICENSE_Kenney.txt)
            font = Resources.Load<Font>("Fonts/KenneyFuture") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var coinSprite = Resources.Load<Sprite>("UI/extra_box_coin");
            var greenButton = Resources.Load<Sprite>("UI/button_green");
            var roundButton = Resources.Load<Sprite>("UI/button_round_blue");
            var repeatIcon = Resources.Load<Sprite>("UI/icon_repeat");
            var panelSprite = Resources.Load<Sprite>("UI/panel");
            foreach (var m in Resources.LoadAll<Sprite>("Mascots"))
            {
                if (m.name == "extra_character_c") loseMascot = m;
                else winMascots.Add(m);
            }

            var safe = MakeRect("SafeArea", transform, Vector2.zero, Vector2.one);
            safe.gameObject.AddComponent<SafeArea>();

            // Üst bar
            var top = MakeRect("TopBar", safe, new Vector2(0, 1), new Vector2(1, 1));
            top.sizeDelta = new Vector2(0, 160);
            top.anchoredPosition = new Vector2(0, -90);

            levelText = MakeLabel("Level", top, 46, TextAnchor.MiddleLeft, shadow: true);
            Place(levelText.rectTransform, new Vector2(0, 0.5f), new Vector2(50, 0), new Vector2(300, 120), new Vector2(0, 0.5f));

            var pill = MakeImage("TimerPill", top, PillColor);
            Place(pill.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 104));
            timerText = MakeLabel("Timer", pill.transform, 64, TextAnchor.MiddleCenter);
            Stretch(timerText.rectTransform);

            var coinPill = MakeImage("CoinPill", top, PillColor);
            Place(coinPill.rectTransform, new Vector2(0, 0.5f), new Vector2(60, -115), new Vector2(220, 76), new Vector2(0, 0.5f));
            var coinIcon = MakeImage("CoinIcon", coinPill.transform, coinSprite != null ? Color.white : CoinColor);
            coinIcon.sprite = coinSprite;
            coinIcon.preserveAspect = true;
            Place(coinIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(-14, 4), new Vector2(84, 84), new Vector2(0, 0.5f));
            coinText = MakeLabel("Coins", coinPill.transform, 44, TextAnchor.MiddleLeft);
            Place(coinText.rectTransform, new Vector2(0, 0.5f), new Vector2(84, 0), new Vector2(130, 76), new Vector2(0, 0.5f));

            var restart = MakeButton("Restart", top, roundButton, PillColor, game.Restart);
            Place(restart, new Vector2(1, 0.5f), new Vector2(-50, 0), new Vector2(130, 130), new Vector2(1, 0.5f));
            var icon = MakeImage("Icon", restart, Color.white);
            icon.sprite = repeatIcon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(64, 64));

            timeBomb = MakeImage("TimeBomb", pill.transform, Color.white);
            timeBomb.sprite = Resources.Load<Sprite>("Deco/extra_crate_explosive");
            timeBomb.preserveAspect = true;
            timeBomb.raycastTarget = false;
            timeBomb.enabled = false;
            Place(timeBomb.rectTransform, new Vector2(1, 0.5f), new Vector2(16, 0), new Vector2(96, 96), new Vector2(0, 0.5f));

            // Sonuç paneli
            var dim = MakeImage("ResultPanel", safe, new Color(0, 0, 0, 0.6f));
            Stretch(dim.rectTransform);
            panel = dim.gameObject;

            var cardImage = MakeImage("Card", dim.transform, panelSprite != null ? Color.white : Palette.Hex("#E9ECF5"));
            SetSliced(cardImage, panelSprite);
            card = cardImage.rectTransform;
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(820, 620));

            // Karakterin üstünde durduğu küçük Kenney zemini: kartın üst kenarında çimen ve taş blokları.
            const float tile = 110f;
            string[] groundTiles = { "extra_stone_top", "extra_dirt_top", "extra_dirt_top", "extra_dirt_top", "extra_stone_top" };
            for (int i = 0; i < groundTiles.Length; i++)
            {
                var sprite = Resources.Load<Sprite>($"Deco/{groundTiles[i]}");
                if (sprite == null) continue;
                var groundTile = MakeImage(sprite.name, card, Color.white);
                groundTile.sprite = sprite;
                groundTile.raycastTarget = false;
                float x = (i - (groundTiles.Length - 1) * 0.5f) * tile;
                Place(groundTile.rectTransform, new Vector2(0.5f, 1), new Vector2(x, -50), new Vector2(tile, tile), new Vector2(0.5f, 0));
            }

            mascot = MakeImage("Mascot", card, Color.white);
            mascot.preserveAspect = true;
            mascot.raycastTarget = false;
            Place(mascot.rectTransform, new Vector2(0.5f, 1), MascotRest, new Vector2(230, 230), new Vector2(0.5f, 0));

            panelTitle = MakeLabel("Title", card, 92, TextAnchor.MiddleCenter);
            panelTitle.color = DarkTextColor;
            Place(panelTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(780, 140));

            panelSubtitle = MakeLabel("Subtitle", card, 56, TextAnchor.MiddleCenter);
            Place(panelSubtitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -245), new Vector2(780, 90));

            var button = MakeButton("Action", card, greenButton, ButtonColor, () => panelAction?.Invoke());
            Place(button, new Vector2(0.5f, 0), new Vector2(0, 140), new Vector2(580, 170));
            panelButtonLabel = MakeLabel("Label", button, 54, TextAnchor.MiddleCenter, shadow: true);
            Place(panelButtonLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(560, 150));

            panel.SetActive(false);
        }

        // ---- küçük uGUI yardımcıları ----

        static RectTransform MakeRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
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

        Text MakeLabel(string name, Transform parent, int size, TextAnchor align, bool shadow = false)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            t.transform.SetParent(parent, false);
            t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            if (shadow)
            {
                var sh = t.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0, 0, 0, 0.35f);
                sh.effectDistance = new Vector2(0, -4);
            }
            return t;
        }

        static Image MakeImage(string name, Transform parent, Color color)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.color = color;
            return img;
        }

        static RectTransform MakeButton(string name, Transform parent, Sprite sprite, Color fallback, Action onClick)
        {
            var img = MakeImage(name, parent, sprite != null ? Color.white : fallback);
            SetSliced(img, sprite);
            var btn = img.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(() =>
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
            img.pixelsPerUnitMultiplier = 0.8f;
        }
    }
}
