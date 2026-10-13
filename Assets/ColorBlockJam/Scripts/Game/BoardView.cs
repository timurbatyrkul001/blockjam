using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ColorBlockJam
{
    // BoardModel'i 3D sahneye döker: zemin, kenar duvarları, kapılar, iç duvarlar ve bloklar.
    public class BoardView : MonoBehaviour
    {
        public const float TileTop = 0.05f;
        const float BorderHeight = 0.7f;
        const float GateHeight = 0.2f;
        const float IntroDrop = 0.45f;
        const float IntroStagger = 0.018f;
        const float IntroDuration = 0.22f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly Color WrongTint = new(1f, 0.15f, 0.15f);

        public BoardModel Model { get; private set; }
        readonly Dictionary<int, BlockView> blockViews = new();

        // Kapı → küpleri; flash/pulse animasyonları için.
        class GateFx
        {
            public readonly List<Transform> cubes = new();
            public readonly List<Renderer> renderers = new();
            public readonly List<Vector3> basePositions = new();
            public Color color;
            public Coroutine running;
        }
        readonly Dictionary<GateData, GateFx> gateFx = new();

        // Intro'da yükselen zemin parçaları.
        readonly List<Transform> introPieces = new();
        readonly List<Vector3> introBase = new();
        readonly List<float> introDelay = new();

        MaterialPropertyBlock mpb;

        public static BoardView Create(BoardModel model)
        {
            var view = new GameObject("Board").AddComponent<BoardView>();
            view.Build(model);
            return view;
        }

        public BlockView ViewOf(BlockState block) => blockViews[block.Id];

        // Hücrenin merkezi; board (0,0,0) etrafında ortalanır.
        public Vector3 CellToLocal(Vector2Int c) =>
            new(c.x + 0.5f - Model.Width * 0.5f, 0f, c.y + 0.5f - Model.Height * 0.5f);

        // Yanlış renk: kırmızı nabız + hafif ölçek darbesi.
        public void FlashGate(GateData gate)
        {
            PlayGate(gate, wrong: true);
            var fx = FindGate(gate);
            if (fx != null && isActiveAndEnabled) StartCoroutine(ExclamationRoutine(fx));
        }

        static Sprite exclamationSprite;

        // Yanlış kapı: kapının üstünde Kenney "!" kutusu zıplayıp yükselerek kaybolur.
        IEnumerator ExclamationRoutine(GateFx fx)
        {
            if (exclamationSprite == null) exclamationSprite = Resources.Load<Sprite>("Deco/extra_box_exclamation");
            if (exclamationSprite == null || fx.basePositions.Count == 0) yield break;

            var center = Vector3.zero;
            foreach (var p in fx.basePositions) center += p;
            center /= fx.basePositions.Count;

            var go = new GameObject("WrongGate!");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = exclamationSprite;
            float baseScale = 0.8f / exclamationSprite.bounds.size.x;
            var cam = Camera.main;

            const float duration = 0.7f;
            for (float t = 0; t < duration && go != null; t += Time.deltaTime)
            {
                float k = t / duration;
                float pop = Ease.OutBack(Mathf.Clamp01(t / 0.15f), 2.5f);
                go.transform.localPosition = center + Vector3.up * (0.7f + k * 0.6f);
                go.transform.localScale = Vector3.one * baseScale * pop;
                if (cam != null) go.transform.rotation = cam.transform.rotation;
                sr.color = new Color(1f, 1f, 1f, k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f);
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        // Doğru çıkış: kapı zıplar ve kendi renginde parlar.
        public void PulseGate(GateData gate) => PlayGate(gate, wrong: false);

        void PlayGate(GateData gate, bool wrong)
        {
            if (this == null || !isActiveAndEnabled) return;
            var fx = FindGate(gate);
            if (fx == null) return;
            if (fx.running != null) { StopCoroutine(fx.running); RestoreGate(fx); }
            fx.running = StartCoroutine(wrong ? FlashRoutine(fx) : PulseRoutine(fx));
        }

        GateFx FindGate(GateData gate)
        {
            if (gate == null) return null;
            if (gateFx.TryGetValue(gate, out var fx)) return fx;
            // Referans farklıysa alanlarla eşleştir.
            foreach (var kv in gateFx)
                if (kv.Key.side == gate.side && kv.Key.start == gate.start && kv.Key.length == gate.length) return kv.Value;
            return null;
        }

        IEnumerator FlashRoutine(GateFx fx)
        {
            const float duration = 0.3f;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                // İki kısa kırmızı nabız.
                float tint = Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2f)) * (1f - k * 0.4f);
                float punch = Ease.Punch(k, 3f) * 0.14f;
                ApplyGate(fx, Color.Lerp(fx.color, WrongTint, tint), new Vector3(1f + punch, 1f + punch * 1.5f, 1f + punch), 0f);
                yield return null;
            }
            RestoreGate(fx);
            fx.running = null;
        }

        IEnumerator PulseRoutine(GateFx fx)
        {
            const float duration = 0.38f;
            var bright = Color.Lerp(fx.color, Color.white, 0.55f);
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                float glow = Ease.Bump(Mathf.Clamp01(k * 1.6f));
                float bounce = Ease.Punch(k, 2.5f);
                ApplyGate(fx, Color.Lerp(fx.color, bright, glow),
                    new Vector3(1f + bounce * 0.08f, 1f + bounce * 1.1f, 1f + bounce * 0.08f), Mathf.Max(0f, bounce) * 0.06f);
                yield return null;
            }
            RestoreGate(fx);
            fx.running = null;
        }

        void ApplyGate(GateFx fx, Color color, Vector3 scale, float lift)
        {
            mpb ??= new MaterialPropertyBlock();
            mpb.SetColor(BaseColorId, color);
            mpb.SetColor(ColorId, color);
            var baseScale = new Vector3(1f, GateHeight, 1f);
            for (int i = 0; i < fx.cubes.Count; i++)
            {
                var cube = fx.cubes[i];
                if (cube == null) continue;
                fx.renderers[i].SetPropertyBlock(mpb);
                cube.localScale = Vector3.Scale(baseScale, scale);
                // Alt yüz zeminde kalsın: merkez yüksekliği ölçekle birlikte artar.
                var p = fx.basePositions[i];
                p.y = GateHeight * 0.5f * scale.y + lift;
                cube.localPosition = p;
            }
        }

        void RestoreGate(GateFx fx)
        {
            var baseScale = new Vector3(1f, GateHeight, 1f);
            for (int i = 0; i < fx.cubes.Count; i++)
            {
                var cube = fx.cubes[i];
                if (cube == null) continue;
                fx.renderers[i].SetPropertyBlock(null);
                cube.localScale = baseScale;
                cube.localPosition = fx.basePositions[i];
            }
        }

        void Build(BoardModel model)
        {
            Model = model;
            int w = model.Width, h = model.Height;

            Shapes3D.Cube(transform, new Vector3(0, -0.25f, 0), new Vector3(w + 2, 0.5f, h + 2), Palette.BoardBase, name: "Base");

            // Ahşap masa: board'un altında, ekranı dolduran geniş tahtalar.
            const float plank = 1.6f;
            for (int i = -14; i <= 14; i++)
                Shapes3D.Cube(transform, new Vector3(i * plank, -0.75f, 0), new Vector3(plank - 0.05f, 0.5f, 60f),
                    i % 2 == 0 ? Palette.TableA : Palette.TableB, smoothness: 0.15f, name: "TablePlank");

            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                var c = new Vector2Int(x, y);
                var p = CellToLocal(c);
                GameObject piece;
                if (model.IsWall(c))
                    piece = Shapes3D.Cube(transform, p + Vector3.up * 0.35f, new Vector3(0.96f, 0.7f, 0.96f), Palette.Wall, name: "Wall");
                else
                {
                    var tileColor = (x + y) % 2 == 0 ? Palette.Tile : Palette.TileAlt;
                    piece = Shapes3D.Cube(transform, p + Vector3.up * (TileTop - 0.025f), new Vector3(0.96f, 0.05f, 0.96f), tileColor, smoothness: 0.2f, name: "Tile");
                }
                AddIntroPiece(piece.transform, (x + (h - 1 - y)) * IntroStagger);
            }

            BuildBorder();

            foreach (var block in model.Blocks)
            {
                var bv = new GameObject($"Block_{block.Id}_{block.Color}").AddComponent<BlockView>();
                bv.transform.SetParent(transform, false);
                bv.Init(block, this);
                blockViews[block.Id] = bv;
            }

            StartCoroutine(IntroRoutine());
        }

        void AddIntroPiece(Transform t, float delay)
        {
            introPieces.Add(t);
            introBase.Add(t.localPosition);
            introDelay.Add(delay);
            t.localPosition += Vector3.down * IntroDrop;
        }

        // Zemin karoları köşeden çapraz dalga halinde yükselir.
        IEnumerator IntroRoutine()
        {
            float maxDelay = 0f;
            foreach (var d in introDelay) maxDelay = Mathf.Max(maxDelay, d);
            float total = maxDelay + IntroDuration;

            for (float t = 0; t < total; t += Time.deltaTime)
            {
                for (int i = 0; i < introPieces.Count; i++)
                {
                    var piece = introPieces[i];
                    if (piece == null) continue;
                    float k = Mathf.Clamp01((t - introDelay[i]) / IntroDuration);
                    piece.localPosition = introBase[i] + Vector3.down * (IntroDrop * (1f - Ease.OutBack(k, 1.4f)));
                }
                yield return null;
            }
            for (int i = 0; i < introPieces.Count; i++)
                if (introPieces[i] != null) introPieces[i].localPosition = introBase[i];
        }

        // Board'un etrafındaki halka: kapı olan hücreler alçak ve renkli, diğerleri yüksek duvar.
        void BuildBorder()
        {
            int w = Model.Width, h = Model.Height;
            for (int x = -1; x <= w; x++)
            for (int y = -1; y <= h; y++)
            {
                bool onRing = x == -1 || y == -1 || x == w || y == h;
                if (!onRing) continue;

                var c = new Vector2Int(x, y);
                var p = CellToLocal(c);
                var gate = GateAt(c);
                if (gate != null)
                {
                    var color = Palette.Of(gate.color);
                    var cube = Shapes3D.Cube(transform, p + Vector3.up * (GateHeight * 0.5f), new Vector3(1f, GateHeight, 1f),
                        color, smoothness: 0.7f, name: $"Gate_{gate.color}");
                    if (!gateFx.TryGetValue(gate, out var fx))
                        gateFx[gate] = fx = new GateFx { color = color };
                    fx.cubes.Add(cube.transform);
                    fx.renderers.Add(cube.GetComponent<Renderer>());
                    fx.basePositions.Add(cube.transform.localPosition);
                    AddGateArrow(p, gate.side);
                }
                else
                    Shapes3D.Cube(transform, p + Vector3.up * (BorderHeight * 0.5f - 0.1f), new Vector3(1f, BorderHeight, 1f),
                        Palette.Border, name: "Border");
            }
        }

        static Sprite arrowSprite;

        // Kapının üstünde, çıkış yönünü gösteren düz yatan beyaz ok (Kenney UI Pack).
        void AddGateArrow(Vector3 cellPos, Side side)
        {
            if (arrowSprite == null) arrowSprite = Resources.Load<Sprite>("UI/gate_arrow");
            if (arrowSprite == null) return;

            float yaw = side switch { Side.Top => 0f, Side.Right => 90f, Side.Bottom => 180f, _ => -90f };
            var go = new GameObject("GateArrow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = cellPos + Vector3.up * (GateHeight + 0.02f);
            go.transform.localRotation = Quaternion.Euler(90f, yaw, 0f);
            go.transform.localScale = Vector3.one * (0.5f / arrowSprite.bounds.size.x);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = arrowSprite;
            sr.color = new Color(1f, 1f, 1f, 0.9f);
        }

        GateData GateAt(Vector2Int ring)
        {
            foreach (var g in Model.Gates)
            {
                bool hit = g.side switch
                {
                    Side.Left => ring.x == -1 && ring.y >= g.start && ring.y <= g.End,
                    Side.Right => ring.x == Model.Width && ring.y >= g.start && ring.y <= g.End,
                    Side.Bottom => ring.y == -1 && ring.x >= g.start && ring.x <= g.End,
                    _ => ring.y == Model.Height && ring.x >= g.start && ring.x <= g.End,
                };
                if (hit) return g;
            }
            return null;
        }
    }
}
