using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ColorBlockJam
{
    // Tek bir bloğun 3D görüntüsü. Mantıksal pozisyonu BoardModel'de tutulur;
    // bu sınıf sadece o pozisyona yumuşakça kayar. Squash/stretch ve sarsıntı
    // "Visual" pivotunda uygulanır, böylece takip mantığıyla çakışmaz.
    public class BlockView : MonoBehaviour
    {
        const float Height = 0.42f;        // kabın çeper yüksekliği
        const float FloorHeight = 0.12f;
        const float RimWidth = 0.1f;
        const float FoodSize = 0.6f;
        const float CellSize = 0.88f;
        const float LiftAmount = 0.15f;
        const float FollowSharpness = 25f;
        const float IntroDelay = 0.2f;
        const float IntroStagger = 0.04f;

        public BlockState State { get; private set; }

        BoardView board;
        Transform visual;
        Vector3 visualBase;     // pivotun dinlenme pozisyonu (şeklin alt-merkezi)
        Vector3 target;
        Vector3 dragOffset;
        Vector3 shakeOffset;
        Vector3 dropOffset;     // intro/settle sırasındaki dikey ofset
        bool lifted;
        bool exiting;
        Coroutine scaleRoutine;
        Coroutine shakeRoutine;

        public void Init(BlockState state, BoardView boardView)
        {
            State = state;
            board = boardView;
            BuildMesh();
            target = board.CellToLocal(State.Position);
            transform.localPosition = target;
            PlayIntro();
        }

        public void SyncPosition()
        {
            if (board == null || State == null) return;
            target = board.CellToLocal(State.Position);
        }

        public void SetLifted(bool value)
        {
            if (exiting || lifted == value) return;
            lifted = value;
            RunScale(value ? PopRoutine() : SettleRoutine());
        }

        // Hücre-altı sürükleme ofseti (hücre birimi): x → dünya x, y → dünya z.
        public void SetDragOffset(Vector2 offset)
        {
            dragOffset = new Vector3(Mathf.Clamp(offset.x, -0.6f, 0.6f), 0f, Mathf.Clamp(offset.y, -0.6f, 0.6f));
        }

        // Yanlış kapı: kapıya doğru kısa çarpma + yatay titreme.
        public void Shake(Vector2Int dir)
        {
            if (exiting || !isActiveAndEnabled) return;
            if (shakeRoutine != null) StopCoroutine(shakeRoutine);
            shakeRoutine = StartCoroutine(ShakeRoutine(dir));
        }

        void Update()
        {
            if (exiting) return;
            var goal = target + dragOffset + Vector3.up * (lifted ? LiftAmount : 0f);
            transform.localPosition = Vector3.Lerp(transform.localPosition, goal, 1f - Mathf.Exp(-FollowSharpness * Time.deltaTime));
            ApplyVisualOffset();
        }

        void ApplyVisualOffset()
        {
            if (visual != null) visual.localPosition = visualBase + shakeOffset + dropOffset;
        }

        void RunScale(IEnumerator routine)
        {
            if (!isActiveAndEnabled) return;
            if (scaleRoutine != null) StopCoroutine(scaleRoutine);
            dropOffset = Vector3.zero;
            scaleRoutine = StartCoroutine(routine);
        }

        // --- Intro: sıfırdan büyüyerek ve hafifçe düşerek belirir ---
        void PlayIntro()
        {
            visual.localScale = Vector3.zero;
            dropOffset = Vector3.up * 0.6f;
            ApplyVisualOffset();
            if (scaleRoutine != null) StopCoroutine(scaleRoutine);
            scaleRoutine = StartCoroutine(IntroRoutine(IntroDelay + State.Id * IntroStagger));
        }

        IEnumerator IntroRoutine(float delay)
        {
            for (float t = 0; t < delay; t += Time.deltaTime) yield return null;

            const float duration = 0.32f;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                visual.localScale = Vector3.one * Ease.OutBack(k, 2.2f);
                dropOffset = Vector3.up * (0.6f * (1f - Ease.OutQuad(k)));
                yield return null;
            }
            visual.localScale = Vector3.one;
            dropOffset = Vector3.zero;
            scaleRoutine = null;
        }

        // --- Pickup: yukarı uzayıp sönümlenen pop ---
        IEnumerator PopRoutine()
        {
            const float duration = 0.28f, amp = 0.16f;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float p = Ease.Punch(t / duration) * amp;
                visual.localScale = new Vector3(1f - p * 0.5f, 1f + p, 1f - p * 0.5f);
                yield return null;
            }
            visual.localScale = Vector3.one;
            scaleRoutine = null;
        }

        // --- Release: yere inip ezilerek oturur ---
        IEnumerator SettleRoutine()
        {
            // Düşüş takip ile olur; kısa bir an bekleyip çarpma anında ezil.
            for (float t = 0; t < 0.05f; t += Time.deltaTime) yield return null;

            const float duration = 0.3f, amp = 0.14f;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                float p = Ease.Punch(k, 3f) * amp;
                visual.localScale = new Vector3(1f + p * 0.5f, 1f - p, 1f + p * 0.5f);
                dropOffset = Vector3.up * (Mathf.Max(0f, -Mathf.Sin(k * Mathf.PI * 3f)) * 0.04f * (1f - k));
                yield return null;
            }
            visual.localScale = Vector3.one;
            dropOffset = Vector3.zero;
            scaleRoutine = null;
        }

        IEnumerator ShakeRoutine(Vector2Int dir)
        {
            var d = new Vector3(dir.x, 0f, dir.y);
            var side = new Vector3(-dir.y, 0f, dir.x);
            if (side == Vector3.zero) side = Vector3.right;

            const float duration = 0.26f;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                float bump = Ease.Bump(k / 0.35f) * 0.14f;
                float wiggle = k > 0.25f ? Mathf.Sin((k - 0.25f) * Mathf.PI * 8f) * 0.06f * (1f - k) : 0f;
                shakeOffset = d * bump + side * wiggle;
                yield return null;
            }
            shakeOffset = Vector3.zero;
            shakeRoutine = null;
        }

        // Anticipation → kapıya doğru uzayarak kayma → konfeti → yok ol.
        public void PlayExit(Vector2Int dir, Action onDone)
        {
            if (exiting) return;
            exiting = true;
            foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
            StopAllCoroutines();
            scaleRoutine = shakeRoutine = null;
            shakeOffset = dropOffset = Vector3.zero;
            ApplyVisualOffset();
            StartCoroutine(ExitRoutine(dir, onDone));
        }

        IEnumerator ExitRoutine(Vector2Int dir, Action onDone)
        {
            bool alongX = dir.x != 0;
            var d = new Vector3(dir.x, 0f, dir.y);
            int travel = (alongX ? State.Size.x : State.Size.y) + 1;
            var startScale = visual.localScale;

            // 1) Anticipation: dir ekseninde kısa ezilme, hafif geri çekilme.
            const float antic = 0.07f;
            var from = transform.localPosition;
            var squash = AxisScale(alongX, 0.84f, 1.08f, 0.9f);
            for (float t = 0; t < antic; t += Time.deltaTime)
            {
                float k = Ease.OutQuad(t / antic);
                visual.localScale = Vector3.Scale(startScale, Vector3.Lerp(Vector3.one, squash, k));
                transform.localPosition = from - d * (0.06f * k);
                yield return null;
            }

            // 2) Kapıdan uzayarak kayıp küçül; yarı yolda konfeti.
            from = transform.localPosition;
            var to = target + d * travel + Vector3.down * 0.25f;
            var stretch = AxisScale(alongX, 1.3f, 0.85f, 0.95f);
            bool burst = false;
            const float slide = 0.3f;
            for (float t = 0; t < slide; t += Time.deltaTime)
            {
                float k = t / slide;
                transform.localPosition = Vector3.LerpUnclamped(from, to, Ease.InQuad(k));
                var s = Vector3.Lerp(squash, stretch, Ease.OutQuad(Mathf.Clamp01(k * 3f)));
                float shrink = Mathf.Lerp(1f, 0.15f, Ease.InQuad(k));
                visual.localScale = Vector3.Scale(startScale, s) * shrink;
                if (!burst && k >= 0.35f) { burst = true; SpawnConfetti(dir); }
                yield return null;
            }
            if (!burst) SpawnConfetti(dir);

            onDone?.Invoke();
            Destroy(gameObject);
        }

        static Vector3 AxisScale(bool alongX, float along, float across, float up) =>
            alongX ? new Vector3(along, up, across) : new Vector3(across, up, along);

        void SpawnConfetti(Vector2Int dir)
        {
            if (board == null || board.Model == null) return;
            var model = board.Model;

            // Bloğun merkezinden kapı halkasına (board kenarı + yarım hücre) izdüşüm.
            var center = target + new Vector3(visualBase.x, 0f, visualBase.z);
            float edgeX = model.Width * 0.5f + 0.5f, edgeZ = model.Height * 0.5f + 0.5f;
            if (dir.x != 0) center.x = dir.x * edgeX;
            if (dir.y != 0) center.z = dir.y * edgeZ;
            center.y = BoardView.TileTop + 0.35f;

            var world = board.transform.TransformPoint(center);
            var worldDir = board.transform.TransformDirection(new Vector3(dir.x, 0f, dir.y));
            Confetti.Burst(world, Palette.Of(State.Color), worldDir);
        }

        void BuildMesh()
        {
            var color = Palette.Of(State.Color);
            var cells = new HashSet<Vector2Int>(State.Shape);

            // Pivot şeklin alt-merkezinde: ölçek animasyonları bloğun ortasından ve zeminden işler.
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            visualBase = new Vector3((State.Size.x - 1) * 0.5f, BoardView.TileTop, (State.Size.y - 1) * 0.5f);
            visual.localPosition = visualBase;

            // Yemek teması: blok = renkli bento kabı. Hücre başına krem zemin, şeklin dış kenarlarında
            // parlak renkli çeper, her hücrenin içinde o rengin yemeği.
            var rim = color;
            var floor = Color.Lerp(Palette.TrayInner, color, 0.15f);   // krem zemin, kabın renginden hafif bir ton
            float inner = CellSize * 0.5f;
            var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

            foreach (var s in State.Shape)
            {
                var p = new Vector3(s.x - visualBase.x, 0f, s.y - visualBase.z);

                Shapes3D.Cube(visual, p + Vector3.up * (FloorHeight * 0.5f), new Vector3(CellSize, FloorHeight, CellSize),
                    floor, keepCollider: true, smoothness: 0.3f, name: "Floor");
                if (cells.Contains(s + Vector2Int.right))
                    Shapes3D.Cube(visual, p + new Vector3(0.5f, FloorHeight * 0.5f, 0), new Vector3(1f, FloorHeight, CellSize), floor, smoothness: 0.3f, name: "FloorBridge");
                if (cells.Contains(s + Vector2Int.up))
                    Shapes3D.Cube(visual, p + new Vector3(0, FloorHeight * 0.5f, 0.5f), new Vector3(CellSize, FloorHeight, 1f), floor, smoothness: 0.3f, name: "FloorBridge");
                if (cells.Contains(s + Vector2Int.right) && cells.Contains(s + Vector2Int.up) && cells.Contains(s + Vector2Int.one))
                    Shapes3D.Cube(visual, p + new Vector3(0.5f, FloorHeight * 0.5f, 0.5f), new Vector3(1f, FloorHeight, 1f), floor, smoothness: 0.3f, name: "FloorFill");

                // Çeper: komşusu bloğa ait olmayan her kenara bir duvar. L, T, artı şekillerinde de doğru çalışır.
                foreach (var d in dirs)
                {
                    if (cells.Contains(s + d)) continue;
                    bool alongX = d.y != 0;
                    var center = p + new Vector3(d.x * (inner - RimWidth * 0.5f), Height * 0.5f, d.y * (inner - RimWidth * 0.5f));
                    var size = alongX ? new Vector3(CellSize + 0.02f, Height, RimWidth) : new Vector3(RimWidth, Height, CellSize + 0.02f);
                    // komşu hücreye doğru uzat ki çeper hücreler arasında kesintisiz olsun
                    if (alongX && cells.Contains(s + Vector2Int.right)) { center.x += 0.5f - inner + 0.01f; size.x += 1f - CellSize; }
                    if (!alongX && cells.Contains(s + Vector2Int.up)) { center.z += 0.5f - inner + 0.01f; size.z += 1f - CellSize; }
                    Shapes3D.Cube(visual, center, size, rim, smoothness: 0.7f, name: "Rim");
                }

                // Hücrenin yemeği; dönüşü hücreye göre biraz farklı ki kap doğal görünsün.
                float yaw = (s.x * 73 + s.y * 151 + State.Id * 37) % 360;
                FoodModels.Spawn(State.Color, visual, p + Vector3.up * FloorHeight, FoodSize, yaw);

                // Ok işaretli bloklar: çeperin üstünde hareket eksenini gösteren beyaz çizgi.
                if (State.Axis != MoveAxis.Both)
                {
                    var stripe = State.Axis == MoveAxis.Horizontal ? new Vector3(0.6f, 0.02f, 0.12f) : new Vector3(0.12f, 0.02f, 0.6f);
                    Shapes3D.Cube(visual, p + Vector3.up * (Height + 0.12f), stripe, Color.white, smoothness: 0.3f, name: "Arrow");
                }
            }
        }
    }
}
