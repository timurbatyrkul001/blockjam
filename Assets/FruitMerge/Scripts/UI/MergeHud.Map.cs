using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FruitMerge
{
    // Bölüm haritası: aşağıdan yukarı kıvrılan yol, düğmeler, yıldızlar, kilitler.
    // Yerleşim level_design/level_select_mockup.png'ye göre; 30 bölüm için dikey kaydırılır.
    public partial class MergeHud
    {
        public event Action<int> LevelPressed;

        const float NodeSpacing = 230f, NodeBottom = 520f, NodeTop = 700f, NodeSize = 170f;
        const float TileHeight = 4680f;   // map_background.png: 1080x4680, iki kez üst üste dizilir

        float[] pathX;        // map_path.json: yolun orta noktası (0..1), alttan üste 10 px aralıkla
        float pathStep = 10f, spacing = NodeSpacing;

        GameObject mapPanel;
        ScrollRect mapScroll;
        RectTransform mapContent, currentRing;
        Text mapSubtitle, mapPlayLabel;
        Image[] nodeImages;
        Text[] nodeLabels;
        Image[] nodeLocks;
        Image[][] nodeStars;
        int mapCurrent = 1;
        Coroutine ringPulse;

        public bool MapVisible => mapPanel != null && mapPanel.activeSelf;

        public void ShowMap()
        {
            mapPanel.SetActive(true);
            mapPanel.transform.SetAsLastSibling();
            RefreshMap();
            Canvas.ForceUpdateCanvases();
            ScrollMapTo(mapCurrent);
            if (ringPulse != null) StopCoroutine(ringPulse);
            ringPulse = StartCoroutine(PulseRing());
        }

        public void HideMap()
        {
            if (ringPulse != null) StopCoroutine(ringPulse);
            ringPulse = null;
            mapPanel.SetActive(false);
        }

        void RefreshMap()
        {
            int count = LevelLibrary.Count, unlocked = LevelProgress.Unlocked;
            mapCurrent = Mathf.Min(unlocked, count);
            var open = FruitArt.Load("Art/map_node_open");
            var done = FruitArt.Load("Art/map_node_done");
            var locked = FruitArt.Load("Art/map_node_locked");
            var starFull = FruitArt.Load("Art/star_full");
            var starEmpty = FruitArt.Load("Art/star_empty");

            for (int i = 0; i < nodeImages.Length; i++)
            {
                int id = i + 1, stars = LevelProgress.Stars(id);
                bool isLocked = id > unlocked, isDone = stars > 0;
                var img = nodeImages[i];
                var sprite = isLocked ? locked : isDone ? done : open;
                img.sprite = sprite != null ? sprite : roundButton;
                img.type = Image.Type.Simple;
                img.color = sprite != null ? Color.white
                    : isLocked ? FruitArt.Hex("#D8D3BF") : isDone ? FruitArt.Hex("#4CAF50") : FruitArt.Hex("#8BC34A");
                nodeLabels[i].color = isLocked ? FruitArt.Hex("#A89F86") : FruitArt.CreamText;
                nodeLocks[i].gameObject.SetActive(isLocked && nodeLocks[i].sprite != null);
                var button = img.GetComponent<Button>();
                var colors = button.colors;
                colors.disabledColor = Color.white;    // kilitli düğme saydamlaşmasın; soluk görünümü sprite/renk verir
                button.colors = colors;
                button.interactable = !isLocked;
                for (int s = 0; s < 3; s++)
                {
                    var st = nodeStars[i][s];
                    st.gameObject.SetActive(isDone);
                    bool full = s < stars;
                    st.sprite = full ? (starFull != null ? starFull : FruitArt.Star()) : (starEmpty != null ? starEmpty : FruitArt.Star());
                    st.color = (full ? starFull : starEmpty) != null ? Color.white : full ? FruitArt.Gold : FruitArt.Hex("#CFC3A8");
                }
            }

            currentRing.SetParent(nodeImages[mapCurrent - 1].transform, false);
            currentRing.anchoredPosition = Vector2.zero;
            mapSubtitle.text = $"{Mathf.Min(LevelProgress.Completed, count)} / {count} BÖLÜM TAMAM  -  {LevelProgress.TotalStars} YILDIZ";
            mapPlayLabel.text = $"BÖLÜM {mapCurrent} - OYNA";


        }

        void ScrollMapTo(int id)
        {
            float viewH = ((RectTransform)mapScroll.viewport).rect.height;
            float contentH = mapContent.rect.height;
            float y = NodeY(id - 1);
            mapScroll.verticalNormalizedPosition = contentH <= viewH ? 0f : Mathf.Clamp01((y - viewH * 0.45f) / (contentH - viewH));
        }

        float NodeY(int i) => NodeBottom + i * spacing;
        float NodeX(int i) => PathX(NodeY(i));

        // Uzun görsel varsa düğmeler toprak yolun ortasına oturur; yoksa yumuşak zikzak.
        float PathX(float y)
        {
            if (pathX == null) return 230f * Mathf.Sin((y - NodeBottom) / NodeSpacing * 0.9f);
            float local = Mathf.Repeat(y, TileHeight) / pathStep;
            int i = Mathf.Clamp((int)local, 0, pathX.Length - 1), j = Mathf.Min(i + 1, pathX.Length - 1);
            return (Mathf.Lerp(pathX[i], pathX[j], local - i) - 0.5f) * 1080f;
        }

        void LoadPath()
        {
            var json = Resources.Load<TextAsset>("Art/map_path");
            if (json == null) return;
            var data = JsonUtility.FromJson<PathData>(json.text);
            if (data?.x == null || data.x.Length < 2) return;
            pathX = data.x;
            pathStep = data.step > 0 ? data.step : 10f;
        }

        [Serializable] class PathData { public float step; public float[] x; }

        IEnumerator PulseRing()
        {
            while (true)
            {
                float t = Time.unscaledTime;
                currentRing.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(t * 4f));
                currentRing.localEulerAngles = new Vector3(0, 0, t * -20f);
                yield return null;
            }
        }

        // ---- kurulum ----

        void BuildMap()
        {
            int count = Mathf.Max(1, LevelLibrary.Count);
            var panel = MakeImage("Map", transform, FruitArt.Hex("#CFEFD8"));
            Stretch(panel.rectTransform);
            mapPanel = panel.gameObject;
            var bgSprite = FruitArt.Load("Art/background");
            if (bgSprite != null) { panel.sprite = bgSprite; panel.color = Color.white; }

            // kaydırılan alan
            var scroll = MakeRect("Scroll", panel.transform);
            Stretch(scroll);
            mapScroll = scroll.gameObject.AddComponent<ScrollRect>();
            mapScroll.horizontal = false;
            mapScroll.movementType = ScrollRect.MovementType.Elastic;
            mapScroll.scrollSensitivity = 40f;
            var viewport = MakeRect("Viewport", scroll);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();   // boş yerden de sürüklenebilsin
            hit.color = Color.clear;
            mapScroll.viewport = viewport;

            mapContent = MakeRect("Content", viewport);
            mapContent.anchorMin = new Vector2(0, 0);
            mapContent.anchorMax = new Vector2(1, 0);
            mapContent.pivot = new Vector2(0.5f, 0);
            mapScroll.content = mapContent;

            var longBg = FruitArt.Load("Art/map_background");
            if (longBg != null)
            {
                LoadPath();
                const int tiles = 2;
                spacing = (TileHeight * tiles - NodeBottom - NodeTop) / Mathf.Max(1, count - 1);
                for (int t = 0; t < tiles; t++)
                {
                    var bg = MakeImage("MapBackground", mapContent, Color.white);
                    bg.sprite = longBg;
                    bg.raycastTarget = false;
                    Place(bg.rectTransform, new Vector2(0.5f, 0), new Vector2(0, t * TileHeight), new Vector2(1080, TileHeight), new Vector2(0.5f, 0));
                }
            }
            else
            {
                // uzun harita görseli gelene kadar: en altta bahçe şeridi
                var gardenSprite = FruitArt.Load("Art/garden");
                if (gardenSprite != null)
                {
                    var garden = MakeImage("Garden", mapContent, Color.white);
                    garden.sprite = gardenSprite;
                    garden.raycastTarget = false;
                    float h = 1080f * gardenSprite.rect.height / gardenSprite.rect.width;
                    Place(garden.rectTransform, new Vector2(0.5f, 0), new Vector2(0, -80), new Vector2(1080, h), new Vector2(0.5f, 0));
                }
            }

            mapContent.sizeDelta = new Vector2(0, NodeY(count - 1) + NodeTop);

            // yol noktaları
            var dotSprite = FruitArt.Load("Art/map_path_dot");
            for (int i = 0; i < count - 1; i++)
            {
                for (int k = 1; k <= 3; k++)
                {
                    float f = k / 4f;
                    var dot = MakeImage("Dot", mapContent, dotSprite != null ? Color.white : FruitArt.Hex("#C9A27A"));
                    dot.sprite = dotSprite != null ? dotSprite : FruitArt.Circle();
                    dot.raycastTarget = false;
                    float y = Mathf.Lerp(NodeY(i), NodeY(i + 1), f);
                    var pos = new Vector2(pathX != null ? PathX(y) : Mathf.Lerp(NodeX(i), NodeX(i + 1), f), y);
                    Place(dot.rectTransform, new Vector2(0.5f, 0), pos, new Vector2(26, 26));
                }
            }

            // bahçe tabelaları: her 10 bölümde bir
            for (int i = 10; i < count; i += 10)
            {
                var sign = MakePanel("Sign", mapContent, panelSprite);
                Place(sign.rectTransform, new Vector2(0.5f, 0), new Vector2(NodeX(i) > 0 ? -250 : 250, NodeY(i) - 100), new Vector2(300, 90));
                    var label = MakeLabel("Label", sign.transform, 32, TextAnchor.MiddleCenter, false, Weight.Bold);
                label.text = $"{i / 10 + 1}. BAHÇE";
                label.color = FruitArt.DarkText;
                Stretch(label.rectTransform);
            }

            // bölüm düğmeleri
            nodeImages = new Image[count];
            nodeLabels = new Text[count];
            nodeLocks = new Image[count];
            nodeStars = new Image[count][];
            var lockSprite = FruitArt.Load("Art/map_lock");
            for (int i = 0; i < count; i++)
            {
                int id = i + 1;
                var node = MakeButton($"Node{id}", mapContent, roundButton, FruitArt.Hex("#8BC34A"), () => LevelPressed?.Invoke(id));
                Place(node, new Vector2(0.5f, 0), new Vector2(NodeX(i), NodeY(i)), new Vector2(NodeSize, NodeSize));
                nodeImages[i] = node.GetComponent<Image>();
                nodeLabels[i] = MakeLabel("Number", node, 72, TextAnchor.MiddleCenter, shadow: true, Weight.Bold);
                nodeLabels[i].text = id.ToString();
                Place(nodeLabels[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(160, 90));

                var lk = MakeImage("Lock", node, Color.white);
                lk.sprite = lockSprite;   // Codex'in kilidi gelene kadar gizli
                lk.raycastTarget = false;
                Place(lk.rectTransform, new Vector2(1, 0), new Vector2(-14, 14), new Vector2(60, 60));
                nodeLocks[i] = lk;

                nodeStars[i] = new Image[3];
                for (int s = 0; s < 3; s++)
                {
                    var st = MakeImage($"Star{s}", node, Color.white);
                    st.raycastTarget = false;
                    st.preserveAspect = true;
                    Place(st.rectTransform, new Vector2(0.5f, 0), new Vector2((s - 1) * 48f, s == 1 ? -34f : -24f), new Vector2(50, 50));
                    nodeStars[i][s] = st;
                }
            }

            var ringSprite = FruitArt.Load("Art/map_current_ring");
            var ring = MakeImage("CurrentRing", mapContent, ringSprite != null ? Color.white : FruitArt.Gold);
            ring.sprite = ringSprite != null ? ringSprite : FruitArt.Ring();
            ring.raycastTarget = false;
            currentRing = ring.rectTransform;
            Place(currentRing, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(NodeSize + 50, NodeSize + 50));

            // sabit üst başlık ve alt butonlar (güvenli alanda)
            var safe = MakeRect("MapSafe", panel.transform);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            var header = MakePanel("Header", safe, panelSprite);
            Place(header.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(820, 170), new Vector2(0.5f, 1));
            var title = MakeLabel("Title", header.transform, 64, TextAnchor.MiddleCenter, false, Weight.Bold);
            title.text = "BAHÇEM";
            title.color = FruitArt.DarkText;
            Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -62), new Vector2(780, 70));
            mapSubtitle = MakeLabel("Subtitle", header.transform, 30, TextAnchor.MiddleCenter, false, Weight.SemiBold);
            mapSubtitle.color = FruitArt.Hex("#8A5A2B");
            Place(mapSubtitle.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 38), new Vector2(780, 36));

            var play = MakeButton("Play", safe, greenButton, FruitArt.Hex("#35C46A"), () => LevelPressed?.Invoke(mapCurrent));
            Place(play, new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(640, 160));
            mapPlayLabel = ButtonLabel(play, "BÖLÜM 1 - OYNA", 48);

            // Klasik mod artık ana menüden açılır; haritadan ana menüye dönülür
            var back = MakeButton("Menu", safe, panelSprite, FruitArt.Cream, () => MenuPressed?.Invoke());
            Place(back, new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(640, 100));
            ButtonLabel(back, "ANA MENÜ", 38, FruitArt.DarkText);

            mapPanel.SetActive(false);
        }
    }
}
