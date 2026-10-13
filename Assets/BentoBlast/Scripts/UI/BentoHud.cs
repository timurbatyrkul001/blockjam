using System;
using System.Collections;
using System.Collections.Generic;
using ColorBlockJam;
using PuzzleKit;
using UnityEngine;
using UnityEngine.UI;

namespace BentoBlast
{
    // Skor, en iyi skor, sipariş kartı, uçan puan yazıları ve oyun sonu paneli. Koddan kurulur.
    // Kenney Future fontunda ı/ş/ğ yok; metinler bu harfler olmadan seçildi.
    public class BentoHud : MonoBehaviour
    {
        static readonly Color PillColor = new(0f, 0f, 0f, 0.35f);
        static readonly Color DarkText = Palette.Hex("#2B3556");
        static readonly Color GoldText = Palette.Hex("#FFC43D");
        static readonly Color ComboText = Palette.Hex("#FF8A3D");

        Font font;
        Text scoreText, bestText;
        RectTransform orderCard;
        readonly Image[] orderSwatches = new Image[2];
        readonly Text[] orderCounts = new Text[2];
        RectTransform canvasRect;
        GameObject gameOverPanel;
        RectTransform gameOverCard;
        Text gameOverScore, gameOverBest;
        Image mascot;
        Sprite happyMascot, sadMascot;
        Action onRestart;
        RectTransform transientRoot;
        Image orderImage;
        readonly int[] displayedHave = new int[2];
        readonly int[] displayedNeed = new int[2];
        readonly Coroutine[] swatchPunch = new Coroutine[2];
        Coroutine orderPunch;

        public Vector2 OrderScreenPosition => RectTransformUtility.WorldToScreenPoint(null, orderCard.position);

        public static BentoHud Create(BentoGame game)
        {
            var go = new GameObject("BentoHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            var hud = go.AddComponent<BentoHud>();
            hud.Build(game);
            return hud;
        }

        public void SetScore(int score, int best)
        {
            scoreText.text = score.ToString();
            bestText.text = best.ToString();
        }

        public void SetOrder(Order order, bool animate)
        {
            for (int i = 0; i < 2; i++)
            {
                orderSwatches[i].color = Palette.Of(order.Fruits[i]);
                displayedHave[i] = order.Have[i];
                displayedNeed[i] = order.Need[i];
                RefreshOrderCount(i);
            }
            if (orderPunch != null) StopCoroutine(orderPunch);
            orderCard.localScale = Vector3.one;
            if (animate) orderPunch = StartCoroutine(Punch(orderCard, 0.35f));
        }

        // Ekranda yukarı süzülüp kaybolan puan yazısı; kombo varsa altında "COMBO x3".
        public void Floating(string text, Vector2 screenPos, string praise, int combo)
        {
            var group = MakeRect("Floating", transientRoot, Vector2.zero, Vector2.zero);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out var local);
            group.anchorMin = group.anchorMax = new Vector2(0.5f, 0.5f);
            group.anchoredPosition = local;
            group.sizeDelta = new Vector2(600, 260);

            var main = MakeLabel("Points", group, 84, TextAnchor.MiddleCenter, shadow: true);
            main.text = text;
            Place(main.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(600, 100));

            var sub = MakeLabel("Combo", group, Mathf.Min(110, 56 + 8 * combo), TextAnchor.MiddleCenter, shadow: true);
            sub.color = combo >= 6 ? Palette.Hex("#E040FB") : combo >= 4 ? ComboText : GoldText;
            sub.text = combo >= 2 ? $"{praise} Combo x{combo}".Trim() : praise;
            Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(600, 80));

            StartCoroutine(FloatAway(group, sub.rectTransform));
        }

        void RefreshOrderCount(int index)
        {
            orderCounts[index].text = $"{displayedHave[index]}/{displayedNeed[index]}";
            orderCounts[index].color = displayedHave[index] >= displayedNeed[index] ? GoldText : Color.white;
        }

        public void FlyToOrder(Vector2 screenPos, int fruitIndex, Action onArrive)
        {
            if (fruitIndex < 0 || fruitIndex >= orderSwatches.Length) { onArrive?.Invoke(); return; }
            var target = orderSwatches[fruitIndex];
            var icon = MakeImage("FlyingFruit", transientRoot, target.color);
            icon.sprite = target.sprite;
            icon.preserveAspect = target.preserveAspect;
            icon.raycastTarget = false;
            Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 56));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out var local);
            icon.rectTransform.anchoredPosition = local;
            StartCoroutine(Fly(icon, fruitIndex, onArrive));
        }

        IEnumerator Fly(Image icon, int fruitIndex, Action onArrive)
        {
            var rt = icon.rectTransform;
            var from = rt.anchoredPosition;
            var target = orderSwatches[fruitIndex];
            for (float time = 0; time < 0.5f && rt != null && target != null; time += Time.unscaledDeltaTime)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(null,
                    target.rectTransform.TransformPoint(target.rectTransform.rect.center));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var to);
                var control = (from + to) * 0.5f + Vector2.up * 180f;
                float k = Ease.OutQuad(time / 0.5f), u = 1f - k;
                rt.anchoredPosition = u * u * from + 2f * u * k * control + k * k * to;
                rt.localScale = Vector3.one * (1f + 0.12f * Ease.Bump(k));
                yield return null;
            }
            if (rt == null) yield break;
            displayedHave[fruitIndex] = Mathf.Min(displayedNeed[fruitIndex], displayedHave[fruitIndex] + 1);
            RefreshOrderCount(fruitIndex);
            if (swatchPunch[fruitIndex] != null) StopCoroutine(swatchPunch[fruitIndex]);
            swatchPunch[fruitIndex] = StartCoroutine(Punch(target.rectTransform, 0.18f));
            Sfx.Play("fly", 0.5f, 0.15f);
            Destroy(icon.gameObject);
            onArrive?.Invoke();
        }

        public IEnumerator CompleteOrder(int bonus)
        {
            Sfx.Play("order");
            Haptics.Success();
            Floating($"Talep +{bonus}", OrderScreenPosition, "", 0);
            for (float time = 0; time < 0.48f && orderImage != null; time += Time.unscaledDeltaTime)
            {
                float pulse = Mathf.Pow(Mathf.Sin(time / 0.48f * Mathf.PI * 2f), 2f);
                orderImage.color = Color.Lerp(PillColor, new Color(1, 1, 1, 0.9f), pulse * 0.85f);
                yield return null;
            }
            if (orderImage != null) orderImage.color = PillColor;
        }

        public void ResetAnimations()
        {
            StopAllCoroutines();
            orderPunch = null;
            for (int i = 0; i < 2; i++)
            {
                swatchPunch[i] = null;
                if (orderSwatches[i] != null) orderSwatches[i].rectTransform.localScale = Vector3.one;
            }
            if (orderCard != null) orderCard.localScale = Vector3.one;
            if (gameOverCard != null) gameOverCard.localScale = Vector3.one;
            if (orderImage != null) orderImage.color = PillColor;
            if (transientRoot != null)
                for (int i = transientRoot.childCount - 1; i >= 0; i--) Destroy(transientRoot.GetChild(i).gameObject);
        }

        public void ShowGameOver(int score, int best, bool newBest)
        {
            gameOverScore.text = $"Skor {score}";
            gameOverBest.text = newBest ? "Yeni rekor!" : $"En iyi {best}";
            gameOverBest.color = newBest ? Palette.Hex("#E08A00") : Palette.Hex("#6B7699");
            mascot.sprite = newBest ? happyMascot : sadMascot;
            mascot.enabled = mascot.sprite != null;
            gameOverPanel.SetActive(true);
            StartCoroutine(PopIn(gameOverCard));
        }

        public void HideGameOver() => gameOverPanel.SetActive(false);

        void Build(BentoGame game)
        {
            canvasRect = (RectTransform)transform;
            transientRoot = MakeRect("TransientFx", transform, Vector2.zero, Vector2.one);
            Stretch(transientRoot);
            onRestart = game.NewGame;
            font = Resources.Load<Font>("Fonts/KenneyFuture") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var roundButton = Resources.Load<Sprite>("UI/button_round_blue");
            var repeatIcon = Resources.Load<Sprite>("UI/icon_repeat");
            var greenButton = Resources.Load<Sprite>("UI/button_green");
            var panelSprite = Resources.Load<Sprite>("UI/panel");
            happyMascot = Resources.Load<Sprite>("Mascots/extra_character_a");
            sadMascot = Resources.Load<Sprite>("Mascots/extra_character_c");

            var safe = MakeRect("SafeArea", transform, Vector2.zero, Vector2.one);
            safe.gameObject.AddComponent<ColorBlockJam.SafeArea>();

            // Üst bar: en iyi skor (sol), skor (orta), yeniden başlat (sağ)
            var top = MakeRect("TopBar", safe, new Vector2(0, 1), new Vector2(1, 1));
            top.sizeDelta = new Vector2(0, 200);
            top.anchoredPosition = new Vector2(0, -110);

            var bestLabel = MakeLabel("BestLabel", top, 30, TextAnchor.MiddleLeft);
            bestLabel.text = "En iyi";
            bestLabel.color = GoldText;
            Place(bestLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(50, 28), new Vector2(260, 50), new Vector2(0, 0.5f));
            bestText = MakeLabel("Best", top, 46, TextAnchor.MiddleLeft, shadow: true);
            Place(bestText.rectTransform, new Vector2(0, 0.5f), new Vector2(50, -22), new Vector2(260, 60), new Vector2(0, 0.5f));

            scoreText = MakeLabel("Score", top, 110, TextAnchor.MiddleCenter, shadow: true);
            Place(scoreText.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 150));

            var restart = MakeButton("Restart", top, roundButton, PillColor, () => onRestart());
            Place(restart, new Vector2(1, 0.5f), new Vector2(-50, 0), new Vector2(130, 130), new Vector2(1, 0.5f));
            var icon = MakeImage("Icon", restart, Color.white);
            icon.sprite = repeatIcon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(64, 64));

            // Sipariş kartı: "TALEP  [renk] 2/6   [renk] 0/4"
            var card = MakeImage("Order", safe, PillColor);
            orderImage = card;
            orderCard = card.rectTransform;
            Place(orderCard, new Vector2(0.5f, 1), new Vector2(0, -250), new Vector2(640, 100));
            var label = MakeLabel("Label", orderCard, 36, TextAnchor.MiddleLeft);
            label.text = "Talep";
            label.color = GoldText;
            Place(label.rectTransform, new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(160, 80), new Vector2(0, 0.5f));
            for (int i = 0; i < 2; i++)
            {
                float x = 200 + i * 220;
                orderSwatches[i] = MakeImage($"Fruit{i}", orderCard, Color.white);
                SetSliced(orderSwatches[i], panelSprite);
                Place(orderSwatches[i].rectTransform, new Vector2(0, 0.5f), new Vector2(x, 0), new Vector2(64, 64), new Vector2(0, 0.5f));
                orderCounts[i] = MakeLabel($"Count{i}", orderCard, 44, TextAnchor.MiddleLeft);
                Place(orderCounts[i].rectTransform, new Vector2(0, 0.5f), new Vector2(x + 80, 0), new Vector2(130, 80), new Vector2(0, 0.5f));
            }

            // Oyun sonu paneli
            var dim = MakeImage("GameOver", safe, new Color(0, 0, 0, 0.6f));
            Stretch(dim.rectTransform);
            gameOverPanel = dim.gameObject;
            var cardImage = MakeImage("Card", dim.transform, panelSprite != null ? Color.white : Palette.Hex("#E9ECF5"));
            SetSliced(cardImage, panelSprite);
            gameOverCard = cardImage.rectTransform;
            Place(gameOverCard, new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(820, 660));

            mascot = MakeImage("Mascot", gameOverCard, Color.white);
            mascot.preserveAspect = true;
            mascot.raycastTarget = false;
            Place(mascot.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(230, 230), new Vector2(0.5f, 0));

            var title = MakeLabel("Title", gameOverCard, 84, TextAnchor.MiddleCenter);
            title.text = "Oyun bitti";
            title.color = DarkText;
            Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(780, 120));
            gameOverScore = MakeLabel("Score", gameOverCard, 64, TextAnchor.MiddleCenter);
            gameOverScore.color = DarkText;
            Place(gameOverScore.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -230), new Vector2(780, 90));
            gameOverBest = MakeLabel("Best", gameOverCard, 48, TextAnchor.MiddleCenter);
            Place(gameOverBest.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -320), new Vector2(780, 80));

            var again = MakeButton("Again", gameOverCard, greenButton, Palette.Hex("#35C46A"), () => onRestart());
            Place(again, new Vector2(0.5f, 0), new Vector2(0, 130), new Vector2(580, 170));
            var againLabel = MakeLabel("Label", again, 54, TextAnchor.MiddleCenter, shadow: true);
            againLabel.text = "Tekrar oyna";
            Place(againLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(560, 150));

            gameOverPanel.SetActive(false);
            transientRoot.SetAsLastSibling();
        }

        // ---- animasyonlar ----

        IEnumerator FloatAway(RectTransform rt, RectTransform combo)
        {
            var start = rt.anchoredPosition;
            var texts = rt.GetComponentsInChildren<Text>();
            const float duration = 0.9f;
            for (float t = 0; t < duration && rt != null; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                rt.anchoredPosition = start + Vector2.up * (Ease.OutCubic(k) * 160f);
                rt.localScale = Vector3.one * (t < 0.15f ? Ease.OutBack(t / 0.15f, 2.5f) : 1f);
                if (combo != null) combo.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 24f) * 5f * (1f - k));
                foreach (var tx in texts) { if (tx == null) continue; var c = tx.color; c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f; tx.color = c; }
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

        static IEnumerator Punch(RectTransform rt, float duration)
        {
            for (float t = 0; t < duration && rt != null; t += Time.unscaledDeltaTime)
            {
                rt.localScale = Vector3.one * (1f + 0.15f * Ease.Punch(t / duration));
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
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
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
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
            img.pixelsPerUnitMultiplier = 0.8f;
        }
    }
}
