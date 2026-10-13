using System.Collections;
using System.Collections.Generic;
using ColorBlockJam;
using PuzzleKit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BentoBlast
{
    // BentoBlast sahnesinde Play'e basınca oyunu kurar (sahne boştur, her şey koddan gelir).
    public static class BentoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (SceneManager.GetActiveScene().name != "BentoBlast") return;
            if (Object.FindAnyObjectByType<BentoGame>() != null) return;
            new GameObject("BentoBlast").AddComponent<BentoGame>();
        }
    }

    // Bento Blast: 8×8 board, alttan 3 meyveli parça sürükle, dolan satır/sütunları patlat, siparişleri tamamla.
    public class BentoGame : MonoBehaviour
    {
        const string BestKey = "bb.best";
        const float TrayScale = 0.55f;
        const float TrayZ = -6.4f;
        const float LiftZ = 1.8f;           // sürüklenen parça parmağın bu kadar üstünde durur
        static readonly float[] SlotX = { -3.1f, 0f, 3.1f };
        static readonly Color ValidTint = new(0.62f, 0.86f, 0.6f);
        static readonly Color ClearTint = new(1f, 0.84f, 0.42f);

        static readonly BlockColor[] Fruits = (BlockColor[])System.Enum.GetValues(typeof(BlockColor));

        BentoBoard board;
        ScoreKeeper score;
        PieceGenerator generator;
        OrderBook orders;

        readonly Piece[] tray = new Piece[3];
        readonly GameObject[] trayViews = new GameObject[3];
        readonly Dictionary<Vector2Int, GameObject> cellViews = new();
        readonly Dictionary<Vector2Int, Renderer> tiles = new();
        MaterialPropertyBlock tint;   // Unity bunun alan tanımında oluşturulmasına izin vermez; Awake'te kurulur
        readonly List<Vector2Int> tinted = new();

        Transform boardRoot;
        Camera cam;
        BentoHud hud;
        bool gameOver;
        float lastAspect;
        readonly Coroutine[] slotAnimations = new Coroutine[3];
        readonly bool[] slotBusy = new bool[3];
        readonly Vector3[] idleFruitPositions = new Vector3[3];
        readonly Transform[] idleFruits = new Transform[3];
        Transform dragShadow, fxRoot;
        Material shadowMaterial;
        Vector3 cameraHome;
        Coroutine cameraShake;
        float lastTouch, nextIdleHop;
        bool resolving;
        int pendingOrderIcons;
        readonly HashSet<Vector2Int> previewCells = new();

        int dragIndex = -1;
        Vector2Int? dropOrigin;

        public static int Best => PlayerPrefs.GetInt(BestKey, 0);

        void Awake()
        {
            Application.targetFrameRate = 60;
            tint = new MaterialPropertyBlock();
            shadowMaterial = new Material(Palette.Mat(new Color(0, 0, 0, 0.18f)));
            shadowMaterial.SetFloat("_Surface", 1f);
            shadowMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            shadowMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            shadowMaterial.SetFloat("_ZWrite", 0f);
            shadowMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            shadowMaterial.renderQueue = (int)RenderQueue.Transparent;
            SetupCamera();
            SetupLighting();
            EnsureEventSystem();
            BuildTable();
            hud = BentoHud.Create(this);
        }

        void Start() => NewGame();

        public void NewGame()
        {
            StopAllCoroutines();
            cameraShake = null;
            resolving = false;
            pendingOrderIcons = 0;
            lastTouch = Time.time;
            nextIdleHop = lastTouch + 6f;
#if UNITY_EDITOR
            autoRoutine = null;
#endif
            hud.ResetAnimations();
            ResetIdle();
            for (int i = 0; i < 3; i++) { slotAnimations[i] = null; slotBusy[i] = false; }
            dragIndex = -1;
            dropOrigin = null;
            if (boardRoot != null) Destroy(boardRoot.gameObject);
            cellViews.Clear();
            tiles.Clear();
            tinted.Clear();
            for (int i = 0; i < 3; i++) { tray[i] = null; trayViews[i] = null; }

            var rng = new System.Random();
            board = new BentoBoard();
            score = new ScoreKeeper();
            generator = new PieceGenerator(rng, Fruits);
            orders = new OrderBook(rng, Fruits);
            gameOver = false;

            boardRoot = new GameObject("Board").transform;
            BuildBoard();
            fxRoot = new GameObject("BentoFx").transform;
            fxRoot.SetParent(boardRoot, false);
            dragShadow = new GameObject("DragShadow").transform;
            dragShadow.SetParent(boardRoot, false);
            for (int i = 2; i >= 0; i--)
            {
                var layer = Shapes3D.Cube(dragShadow, Vector3.up * (0.05f * i),
                    new Vector3(1f + i * 0.08f, 1f, 1f + i * 0.08f), Color.black,
                    keepCollider: false, name: "ShadowLayer");
                var renderer = layer.GetComponent<Renderer>();
                renderer.sharedMaterial = shadowMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                var shade = new Color(0, 0, 0, i == 0 ? 0.12f : i == 1 ? 0.055f : 0.025f);
                tint.SetColor("_BaseColor", shade); tint.SetColor("_Color", shade);
                renderer.SetPropertyBlock(tint);
            }
            dragShadow.gameObject.SetActive(false);
            RefillTray();
            FitCamera();

            hud.SetScore(0, Best);
            hud.SetOrder(orders.Current, animate: false);
            hud.HideGameOver();
        }

        void Update()
        {
            if (!Mathf.Approximately(cam.aspect, lastAspect)) FitCamera();
#if UNITY_EDITOR
            DevAutoPlay();
#endif
            if (PointerInput.Pressed) Touch();
            UpdateIdle();
            if (gameOver || resolving) return;

            if (PointerInput.Pressed && dragIndex < 0) TryGrab(PointerInput.Position);
            if (dragIndex < 0) return;
            if (PointerInput.Held) Drag(PointerInput.Position);
            else Release();
        }

        // ---- sürükle-bırak ----

        void TryGrab(Vector2 screenPos)
        {
            if (!Physics.Raycast(cam.ScreenPointToRay(screenPos), out var hit, 200f)) return;
            for (int i = 0; i < 3; i++)
            {
                if (trayViews[i] == null || !hit.transform.IsChildOf(trayViews[i].transform)) continue;
                Grab(i);
                return;
            }
        }

        void Touch()
        {
            lastTouch = Time.time;
            nextIdleHop = lastTouch + Random.Range(6f, 7f);
            ResetIdle();
        }

        void Grab(int index)
        {
            Touch();
            StopSlot(index);
            dragIndex = index;
            dropOrigin = null;
            slotBusy[index] = true;
            slotAnimations[index] = StartCoroutine(GrabScale(index));
            dragShadow.gameObject.SetActive(true);
            Sfx.Play("pickup", 0.7f);
            Haptics.Light();
        }

        void StopSlot(int index)
        {
            if (slotAnimations[index] != null) StopCoroutine(slotAnimations[index]);
            slotAnimations[index] = null;
            slotBusy[index] = false;
        }

        IEnumerator GrabScale(int index)
        {
            var view = trayViews[index];
            if (view == null) yield break;
            var from = view.transform.localScale;
            for (float time = 0; time < 0.12f && view != null; time += Time.deltaTime)
            {
                view.transform.localScale = Vector3.LerpUnclamped(from, Vector3.one, Ease.OutBack(time / 0.12f));
                yield return null;
            }
            if (view != null) view.transform.localScale = Vector3.one;
            slotAnimations[index] = null;
        }

        void Drag(Vector2 screenPos)
        {
            if (trayViews[dragIndex] == null || tray[dragIndex] == null) return;
            var view = trayViews[dragIndex].transform;
            var piece = tray[dragIndex];
            if (!TryPlanePoint(screenPos, out var point)) return;

            // parça parmağın üstünde, ortası parmakla hizalı
            var half = new Vector3((piece.Size.x - 1) * 0.5f, 0f, (piece.Size.y - 1) * 0.5f);
            var target = point + new Vector3(0f, 0.35f, LiftZ) - half;
            float k = 1f - Mathf.Exp(-30f * Time.deltaTime);
            view.position = Vector3.Lerp(view.position, target, k);
            dragShadow.position = new Vector3(view.position.x + half.x, 0.008f, view.position.z + half.z);
            dragShadow.localScale = new Vector3(piece.Size.x * BentoVisuals.CellSize, 0.008f, piece.Size.y * BentoVisuals.CellSize);

            var origin = BentoVisuals.WorldToCell(view.position);
            Vector2Int? next = board.CanPlace(piece, origin) ? origin : null;
            if (next != dropOrigin)
            {
                dropOrigin = next;
                ShowPreview(piece, next);
                if (next != null) Sfx.Play("step", 0.25f, minInterval: 0.04f);
            }
        }

        void Release()
        {
            int index = dragIndex;
            dragIndex = -1;
            StopSlot(index);
            if (dragShadow != null) dragShadow.gameObject.SetActive(false);
            ShowPreview(null, null);

            if (dropOrigin is Vector2Int origin) Place(index, origin);
            else
            {
                Sfx.Play("wrong", 0.5f);
                slotBusy[index] = true;
                slotAnimations[index] = StartCoroutine(ReturnToSlot(index));
            }
        }

        void Place(int index, Vector2Int origin)
        {
            if (tray[index] == null || resolving || !board.CanPlace(tray[index], origin)) return;
            Touch();
            StopSlot(index);
            var piece = tray[index];
            Destroy(trayViews[index]);
            tray[index] = null;
            trayViews[index] = null;

            var result = board.Place(piece, origin);
            foreach (var c in piece.Cells)
            {
                var cell = origin + c;
                var cellView = BentoVisuals.Tray(boardRoot, BentoVisuals.CellToWorld(cell), piece.Color, withCollider: false, name: $"Cell_{cell.x}_{cell.y}");
                cellViews[cell] = cellView;
                StartCoroutine(Land(cellView.transform));
            }

            score.Apply(result, piece.Cells.Length);
            var center = BentoVisuals.CellToWorld(origin) + new Vector3((piece.Size.x - 1) * 0.5f, 0.6f, (piece.Size.y - 1) * 0.5f);

            if (result.Lines > 0)
            {
                resolving = true;
                StartCoroutine(ResolveClear(result));
                string praise = result.Lines >= 3 ? "Harika!" : result.Lines == 2 ? "Super!" : "";
                hud.Floating($"+{result.Points}", cam.WorldToScreenPoint(center), praise, result.Combo);

                if (result.Combo >= 2) Sfx.Play("combo");
                if (result.Lines >= 3 || result.Combo >= 4)
                {
                    if (cameraShake != null) StopCoroutine(cameraShake);
                    cameraShake = StartCoroutine(ShakeCamera());
                    Haptics.Heavy();
                }
            }
            else Sfx.Play("drop", 0.6f);
            Haptics.Light();

            hud.SetScore(score.Score, Mathf.Max(Best, score.Score));

            if (tray[0] == null && tray[1] == null && tray[2] == null) RefillTray();
            if (!resolving) CheckGameOver();
        }

        void CheckGameOver()
        {
            for (int i = 0; i < 3; i++)
                if (tray[i] != null && board.CanPlaceAnywhere(tray[i])) return;

            gameOver = true;
            bool newBest = score.Score > Best;
            if (newBest) { PlayerPrefs.SetInt(BestKey, score.Score); PlayerPrefs.Save(); }
            StartCoroutine(GameOverRoutine(newBest));
        }

        IEnumerator GameOverRoutine(bool newBest)
        {
            yield return new WaitForSeconds(0.7f);
            Sfx.Play("lose");
            hud.ShowGameOver(score.Score, Best, newBest);
        }

        // ---- board görüntüsü ----

        void BuildBoard()
        {
            float size = BentoBoard.Size;
            Shapes3D.Cube(boardRoot, new Vector3(0, -0.26f, 0), new Vector3(size + 0.5f, 0.4f, size + 0.5f), Palette.BoardBase, name: "Base");
            for (int x = 0; x < BentoBoard.Size; x++)
            for (int y = 0; y < BentoBoard.Size; y++)
            {
                var c = new Vector2Int(x, y);
                var tile = Shapes3D.Cube(boardRoot, BentoVisuals.CellToWorld(c) + Vector3.down * 0.03f, new Vector3(0.95f, 0.06f, 0.95f),
                    (x + y) % 2 == 0 ? Palette.Tile : Palette.TileAlt, smoothness: 0.2f, name: "Tile");
                tiles[c] = tile.GetComponent<Renderer>();
            }

            // alttaki parça tepsisi
            Shapes3D.Cube(boardRoot, new Vector3(0, -0.2f, TrayZ), new Vector3(size + 1.5f, 0.3f, 3f), Palette.Border, name: "TrayShelf");
        }

        void BuildTable()
        {
            var table = new GameObject("Table").transform;
            const float plank = 1.6f;
            for (int i = -14; i <= 14; i++)
                Shapes3D.Cube(table, new Vector3(i * plank, -0.75f, 0), new Vector3(plank - 0.05f, 0.5f, 60f),
                    i % 2 == 0 ? Palette.TableA : Palette.TableB, smoothness: 0.15f, name: "TablePlank");
        }

        void RefillTray()
        {
            var pieces = generator.NextTray(board);
            for (int i = 0; i < 3; i++)
            {
                tray[i] = pieces[i];
                var view = BentoVisuals.PieceObject(boardRoot, pieces[i]);
                view.transform.position = SlotPosition(i, pieces[i]) + Vector3.right * 8f;
                view.transform.localScale = Vector3.one * TrayScale;
                trayViews[i] = view;
                slotBusy[i] = true;
                slotAnimations[i] = StartCoroutine(EnterSlot(i));
            }
        }

        static Vector3 SlotPosition(int slot, Piece piece) =>
            new Vector3(SlotX[slot], 0f, TrayZ) - new Vector3((piece.Size.x - 1) * 0.5f, 0f, (piece.Size.y - 1) * 0.5f) * TrayScale;

        // Bırakılacak hücreleri yeşil, patlayacak satır/sütunları altın renginde yakar.
        void ShowPreview(Piece piece, Vector2Int? origin)
        {
            foreach (var c in tinted) tiles[c].SetPropertyBlock(null);
            tinted.Clear();
            if (piece == null || origin is not Vector2Int o) return;

            var placed = previewCells;
            placed.Clear();
            foreach (var c in piece.Cells) placed.Add(o + c);

            for (int i = 0; i < BentoBoard.Size; i++)
            {
                bool row = true, col = true;
                for (int j = 0; j < BentoBoard.Size; j++)
                {
                    var r = new Vector2Int(j, i);
                    var cc = new Vector2Int(i, j);
                    row &= board.IsFilled(r) || placed.Contains(r);
                    col &= board.IsFilled(cc) || placed.Contains(cc);
                }
                for (int j = 0; j < BentoBoard.Size; j++)
                {
                    if (row) Tint(new Vector2Int(j, i), ClearTint);
                    if (col) Tint(new Vector2Int(i, j), ClearTint);
                }
            }
            foreach (var c in placed) if (!tinted.Contains(c)) Tint(c, ValidTint);
        }

        void Tint(Vector2Int c, Color color)
        {
            tint.SetColor("_BaseColor", color);
            tint.SetColor("_Color", color);
            tiles[c].SetPropertyBlock(tint);
            if (!tinted.Contains(c)) tinted.Add(c);
        }

        // ---- animasyonlar ----

        IEnumerator ResolveClear(PlaceResult result)
        {
            var order = orders.Current;
            int first = order.Have[0], second = order.Have[1];
            order.Count(result.Cleared);
            int left0 = order.Have[0] - first, left1 = order.Have[1] - second;
            int bonus = order.Done ? orders.CompleteIfDone() : 0;
            if (bonus > 0)
            {
                score.AddBonus(bonus);
                hud.SetScore(score.Score, Mathf.Max(Best, score.Score));
            }
            pendingOrderIcons = left0 + left1;
            float clearEnd = Time.time + 0.755f;
            StartCoroutine(ClearRoutine(result));
            yield return new WaitForSeconds(0.26f);
            foreach (var (cell, color) in result.Cleared)
            {
                int fruitIndex = color == order.Fruits[0] && left0 > 0 ? 0 :
                    color == order.Fruits[1] && left1 > 0 ? 1 : -1;
                if (fruitIndex < 0) continue;
                if (fruitIndex == 0) left0--; else left1--;
                hud.FlyToOrder(cam.WorldToScreenPoint(BentoVisuals.CellToWorld(cell) + Vector3.up * 0.4f),
                    fruitIndex, OrderIconArrived);
                yield return new WaitForSeconds(0.04f);
            }
            while (pendingOrderIcons > 0) yield return null;
            // Katkı yoksa da son kabın patlamasını bekle.
            while (Time.time < clearEnd) yield return null;
            if (bonus > 0)
            {
                yield return hud.CompleteOrder(bonus);
                hud.SetOrder(orders.Current, animate: true);
            }
            resolving = false;
            CheckGameOver();
        }

        void OrderIconArrived() => pendingOrderIcons = Mathf.Max(0, pendingOrderIcons - 1);

        IEnumerator ClearRoutine(PlaceResult result)
        {
            // Kesişim hücresi yalnızca ilk dalgada çıkarılır.
            foreach (int row in result.Rows)
            {
                StartCoroutine(Sweep(BentoVisuals.CellToWorld(new Vector2Int(0, row)), Vector3.right));
                for (int x = 0; x < BentoBoard.Size; x++) ScheduleBurst(result, new Vector2Int(x, row), x * 0.035f);
            }
            foreach (int column in result.Columns)
            {
                StartCoroutine(Sweep(BentoVisuals.CellToWorld(new Vector2Int(column, 0)), Vector3.forward));
                for (int y = 0; y < BentoBoard.Size; y++) ScheduleBurst(result, new Vector2Int(column, y), y * 0.035f);
            }
            yield break;
        }

        void ScheduleBurst(PlaceResult result, Vector2Int cell, float delay)
        {
            if (!cellViews.TryGetValue(cell, out var view)) return;
            cellViews.Remove(cell);
            foreach (var cleared in result.Cleared)
                if (cleared.cell == cell) { StartCoroutine(Burst(view, cleared.color, delay + 0.18f)); break; }
        }

        IEnumerator Burst(GameObject view, BlockColor fruitColor, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (view == null) yield break;
            var t = view.transform;
            for (float time = 0; time < 0.08f && t != null; time += Time.deltaTime)
            {
                t.localScale = Vector3.one * Mathf.Lerp(1f, 1.15f, Ease.OutQuad(time / 0.08f));
                yield return null;
            }
            if (t == null) yield break;
            var fruit = BentoVisuals.FruitOf(t);
            Confetti.Burst(t.position + Vector3.up * 0.4f, Palette.Of(fruitColor), Vector3.up, 5);
            Sfx.Play("clear");
            Haptics.Medium();
            if (fruit != null) { fruit.SetParent(fxRoot, true); StartCoroutine(FruitBurst(fruit)); }
            for (float time = 0; time < 0.18f && t != null; time += Time.deltaTime)
            {
                t.localScale = Vector3.one * (1.15f * (1f - Ease.InQuad(time / 0.18f)));
                yield return null;
            }
            if (view != null) Destroy(view);
        }

        IEnumerator FruitBurst(Transform fruit)
        {
            var position = fruit.position;
            var scale = fruit.localScale;
            var rotation = fruit.localRotation;
            for (float time = 0; time < 0.25f && fruit != null; time += Time.deltaTime)
            {
                float k = time / 0.25f;
                fruit.position = position + Vector3.up * (Ease.OutQuad(k) * 1.3f);
                fruit.localRotation = rotation * Quaternion.Euler(k * 220f, k * 180f, k * 90f);
                fruit.localScale = scale * (1f - Ease.InQuad(k));
                yield return null;
            }
            if (fruit != null) Destroy(fruit.gameObject);
        }

        IEnumerator Sweep(Vector3 start, Vector3 direction)
        {
            yield return new WaitForSeconds(0.18f);
            if (fxRoot == null) yield break;
            var strip = Shapes3D.Cube(fxRoot, start + Vector3.up * 0.36f,
                direction.x > 0 ? new Vector3(0.06f, 0.018f, 0.94f) : new Vector3(0.94f, 0.018f, 0.06f),
                Color.white, keepCollider: false, name: "ClearSweep");
            var from = strip.transform.position;
            for (float time = 0; time < 0.25f && strip != null; time += Time.deltaTime)
            {
                strip.transform.position = from + direction * (7f * time / 0.25f);
                yield return null;
            }
            if (strip != null) Destroy(strip);
        }

        IEnumerator Land(Transform t)
        {
            var fruit = BentoVisuals.FruitOf(t);
            if (fruit != null) StartCoroutine(FruitHop(fruit, 0.15f, 0.05f));
            for (float time = 0; time < 0.18f && t != null; time += Time.deltaTime)
            {
                float k = time / 0.18f;
                float y = k < 0.5f ? Mathf.Lerp(0.8f, 1.1f, Ease.OutQuad(k * 2f)) :
                    Mathf.Lerp(1.1f, 1f, Ease.OutQuad((k - 0.5f) * 2f));
                float side = 1f / Mathf.Sqrt(y);
                t.localScale = new Vector3(side, y, side);
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
        }

        IEnumerator FruitHop(Transform fruit, float height, float delay = 0f)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            if (fruit == null) yield break;
            var from = fruit.localPosition;
            var parent = fruit.parent;
            for (float time = 0; time < 0.28f && fruit != null && fruit.parent == parent; time += Time.deltaTime)
            {
                fruit.localPosition = from + Vector3.up * (Ease.Bump(time / 0.28f) * height);
                yield return null;
            }
            if (fruit != null && fruit.parent == parent) fruit.localPosition = from;
        }

        IEnumerator ReturnToSlot(int index)
        {
            var view = trayViews[index];
            var piece = tray[index];
            if (view == null || piece == null) yield break;
            var from = view.transform.position;
            var to = SlotPosition(index, piece);
            var fromScale = view.transform.localScale;
            for (float time = 0; time < 0.28f && view != null; time += Time.deltaTime)
            {
                float k = time / 0.28f;
                view.transform.position = Vector3.Lerp(from, to, Ease.OutCubic(k)) + Vector3.up * (Ease.Bump(k) * 1.2f);
                view.transform.localScale = Vector3.Lerp(fromScale, Vector3.one * TrayScale, Ease.OutQuad(k));
                yield return null;
            }
            if (view != null)
            {
                view.transform.position = to;
                yield return SlotPunch(view.transform);
            }
            slotBusy[index] = false;
            slotAnimations[index] = null;
        }

        IEnumerator EnterSlot(int index)
        {
            yield return new WaitForSeconds(index * 0.07f);
            var view = trayViews[index];
            if (view == null) yield break;
            var to = SlotPosition(index, tray[index]);
            var from = to + Vector3.right * 8f;
            for (float time = 0; time < 0.3f && view != null; time += Time.deltaTime)
            {
                float k = time / 0.3f;
                view.transform.position = Vector3.LerpUnclamped(from, to, Ease.OutBack(k, 1.2f));
                yield return null;
            }
            if (view != null)
            {
                view.transform.position = to;
                yield return SlotPunch(view.transform);
            }
            slotBusy[index] = false;
            slotAnimations[index] = null;
        }

        static IEnumerator SlotPunch(Transform t)
        {
            if (t == null) yield break;
            var position = t.localPosition;
            for (float time = 0; time < 0.14f && t != null; time += Time.deltaTime)
            {
                float bump = Ease.Bump(time / 0.14f);
                t.localScale = Vector3.one * (TrayScale * (1f + bump * 0.06f));
                t.localPosition = position + Vector3.up * (bump * 0.08f);
                yield return null;
            }
            if (t != null) { t.localScale = Vector3.one * TrayScale; t.localPosition = position; }
        }

        void ResetIdle()
        {
            for (int i = 0; i < 3; i++)
            {
                if (trayViews[i] != null && !slotBusy[i] && i != dragIndex)
                    trayViews[i].transform.localScale = Vector3.one * TrayScale;
                if (idleFruits[i] != null) idleFruits[i].localPosition = idleFruitPositions[i];
                idleFruits[i] = null;
            }
            idleHopSlot = -1;
        }

        void UpdateIdle()
        {
            if (gameOver || resolving || dragIndex >= 0 || Time.time - lastTouch < 4f) return;
            for (int i = 0; i < 3; i++)
            {
                if (trayViews[i] == null || slotBusy[i]) continue;
                float idleTime = Time.time - lastTouch - 4f;
                float phase = idleTime * (Mathf.PI * 2f / 1.6f) + i * 2.1f;
                float amplitude = 0.04f * Mathf.Clamp01(idleTime / 0.2f);
                trayViews[i].transform.localScale = Vector3.one * (TrayScale * (1f + Mathf.Sin(phase) * amplitude));
            }
            UpdateIdleHop();
        }

        float idleHopStart;
        int idleHopSlot = -1;

        void UpdateIdleHop()
        {
            if (idleHopSlot >= 0)
            {
                int i = idleHopSlot;
                if (idleFruits[i] == null) idleHopSlot = -1;
                else
                {
                    float k = (Time.time - idleHopStart) / 0.28f;
                    idleFruits[i].localPosition = idleFruitPositions[i] + Vector3.up * (Ease.Bump(k) * (0.12f / Mathf.Max(0.01f, idleFruits[i].parent.lossyScale.y)));
                    if (k >= 1f) { idleFruits[i].localPosition = idleFruitPositions[i]; idleFruits[i] = null; idleHopSlot = -1; }
                }
            }
            if (Time.time < nextIdleHop) return;
            nextIdleHop = Time.time + Random.Range(2f, 3f);
            int slot = Random.Range(0, 3);
            if (trayViews[slot] == null || slotBusy[slot]) return;
            var root = trayViews[slot].transform;
            if (root.childCount == 0) return;
            var fruit = BentoVisuals.FruitOf(root.GetChild(Random.Range(0, root.childCount)));
            if (fruit == null) return;
            idleFruits[slot] = fruit;
            idleFruitPositions[slot] = fruit.localPosition;
            idleHopSlot = slot;
            idleHopStart = Time.time;
        }

        IEnumerator ShakeCamera()
        {
            for (float time = 0; time < 0.25f && cam != null; time += Time.deltaTime)
            {
                float strength = 0.12f * (1f - time / 0.25f);
                cam.transform.position = cameraHome + new Vector3(Mathf.Sin(time * 173f), Mathf.Cos(time * 137f), 0) * strength;
                yield return null;
            }
            if (cam != null) cam.transform.position = cameraHome;
            cameraShake = null;
        }

        void OnDestroy()
        {
            if (cam != null) cam.transform.position = cameraHome;
            if (shadowMaterial != null) Destroy(shadowMaterial);
            if (hud != null) { hud.ResetAnimations(); Destroy(hud.gameObject); }
        }

        // ---- kamera, ışık, input altyapısı ----

        bool TryPlanePoint(Vector2 screenPos, out Vector3 point)
        {
            var ray = cam.ScreenPointToRay(screenPos);
            var plane = new Plane(Vector3.up, Vector3.up * 0.35f);
            if (plane.Raycast(ray, out float d)) { point = ray.GetPoint(d); return true; }
            point = default;
            return false;
        }

        void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            if (FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Background;
            cam.fieldOfView = 40f;
        }

        // Board + alttaki tepsi ekrana sığsın (telefon dikey).
        void FitCamera()
        {
            lastAspect = cam.aspect;
            const float pitch = 60f;
            const float minZ = -8.2f, maxZ = 5f, width = 10.6f;
            float vHalf = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float hHalf = Mathf.Atan(Mathf.Tan(vHalf) * cam.aspect);
            float depth = maxZ - minZ;

            float distForWidth = width * 0.5f / Mathf.Tan(hHalf);
            float distForHeight = (depth * 0.5f * Mathf.Sin(pitch * Mathf.Deg2Rad) + 0.6f) / Mathf.Tan(vHalf);
            float dist = Mathf.Max(distForWidth, distForHeight) * 1.08f + 1f;

            var rot = Quaternion.Euler(pitch, 0f, 0f);
            var focus = new Vector3(0f, 0f, (minZ + maxZ) * 0.5f + depth * 0.04f);
            cameraHome = focus - rot * Vector3.forward * dist;
            cam.transform.SetPositionAndRotation(cameraHome, rot);
        }

        void SetupLighting()
        {
            Light sun = null;
            foreach (var l in FindObjectsByType<Light>())
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null) sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            sun.color = new Color(1f, 0.95f, 0.88f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.5f;

            var ambient = new Color(0.62f, 0.58f, 0.55f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(ambient);
            RenderSettings.ambientProbe = sh;
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

#if UNITY_EDITOR
        // Geliştirme: Temp/bb_auto.request dosyasındaki sayı kadar hamleyi bilgisayar oynar
        // (en çok satır patlatan yere koyar). Görselleri parmak olmadan test etmek için.
        float nextAutoCheck;
        Coroutine autoRoutine;

        void DevAutoPlay()
        {
            const string request = "Temp/bb_auto.request";
            if (Time.unscaledTime < nextAutoCheck) return;
            nextAutoCheck = Time.unscaledTime + 0.4f;
            if (!System.IO.File.Exists(request)) return;
            if (autoRoutine != null || dragIndex >= 0 || resolving) return;
            string command = System.IO.File.ReadAllText(request).Trim();
            System.IO.File.Delete(request);
            if (command == "fx") { NewGame(); autoRoutine = StartCoroutine(DevFxShowcase()); return; }
            int.TryParse(command, out int moves);
            autoRoutine = StartCoroutine(AutoMoves(Mathf.Max(1, moves)));
        }

        IEnumerator AutoMoves(int moves)
        {
            for (int m = 0; m < moves && !gameOver; m++)
            {
                int bestIndex = -1, bestLines = -1;
                Vector2Int bestOrigin = default;
                for (int i = 0; i < 3; i++)
                {
                    if (tray[i] == null) continue;
                    for (int x = 0; x < BentoBoard.Size; x++)
                    for (int y = 0; y < BentoBoard.Size; y++)
                    {
                        var o = new Vector2Int(x, y);
                        if (!board.CanPlace(tray[i], o)) continue;
                        int lines = LinesIf(tray[i], o);
                        if (lines > bestLines) { bestLines = lines; bestIndex = i; bestOrigin = o; }
                    }
                }
                if (bestIndex < 0) break;
                while (resolving) yield return null;
                Place(bestIndex, bestOrigin);
                yield return new WaitForSeconds(0.6f);
                while (resolving) yield return null;
            }
            autoRoutine = null;
        }

        IEnumerator DevFxShowcase()
        {
            // Editör kanıtı: normal animasyon yollarını belirli bir sırayla çalıştırır.
            System.IO.Directory.CreateDirectory("Temp/BentoFxShots");
            yield return new WaitForSeconds(0.16f);
            CaptureFx("05_refill");
            yield return new WaitForSeconds(0.6f);
            Grab(0);
            yield return new WaitForSeconds(0.06f);
            Drag(cam.WorldToScreenPoint(new Vector3(0, 0.35f, -2f)));
            CaptureFx("04_grab");
            dropOrigin = null;
            Release();
            yield return new WaitForSeconds(0.14f);
            CaptureFx("04_return");
            yield return new WaitForSeconds(4.5f);
            CaptureFx("06_idle");
            yield return new WaitForSeconds(2f);
            ResetIdle();
            board.Clear();
            foreach (var pair in cellViews) if (pair.Value != null) Destroy(pair.Value);
            cellViews.Clear();
            var red = BlockColor.Red;
            orders.Current.Fruits[0] = red;
            orders.Current.Fruits[1] = BlockColor.Blue;
            orders.Current.Need[0] = 4; orders.Current.Need[1] = 3;
            orders.Current.Have[0] = orders.Current.Have[1] = 0;
            hud.SetOrder(orders.Current, false);
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 7; x++)
            {
                var cell = new Vector2Int(x, y);
                var color = y == 1 ? BlockColor.Blue : red;
                board.Place(new Piece(new[] { Vector2Int.zero }, color), cell);
                cellViews[cell] = BentoVisuals.Tray(boardRoot, BentoVisuals.CellToWorld(cell), color, false);
            }
            for (int i = 0; i < 3; i++)
            {
                var prior = new PlaceResult(); prior.Rows.Add(0); score.Apply(prior, 0);
            }
            StopSlot(0);
            if (trayViews[0] != null) Destroy(trayViews[0]);
            tray[0] = new Piece(new[] { Vector2Int.zero, Vector2Int.up, Vector2Int.up * 2 }, red);
            trayViews[0] = BentoVisuals.PieceObject(boardRoot, tray[0]);
            Place(0, new Vector2Int(7, 0));
            yield return new WaitForSeconds(0.10f);
            CaptureFx("03_combo_shake");
            CaptureFx("04_land");
            yield return new WaitForSeconds(0.25f);
            CaptureFx("01_clear_wave");
            yield return new WaitForSeconds(0.10f);
            CaptureFx("02_fly_order");
            while (pendingOrderIcons > 0) yield return null;
            yield return new WaitForSeconds(0.12f);
            CaptureFx("02_order_complete");
            while (resolving) yield return null;
            Debug.Log("[BentoFx] Showcase complete: six animations, restart remains available.");
            autoRoutine = null;
        }

        static void CaptureFx(string name) => ScreenCapture.CaptureScreenshot($"Temp/BentoFxShots/{name}.png");

        int LinesIf(Piece piece, Vector2Int o)
        {
            var placed = previewCells;
            placed.Clear();
            foreach (var c in piece.Cells) placed.Add(o + c);
            int lines = 0;
            for (int i = 0; i < BentoBoard.Size; i++)
            {
                bool row = true, col = true;
                for (int j = 0; j < BentoBoard.Size; j++)
                {
                    row &= board.IsFilled(new Vector2Int(j, i)) || placed.Contains(new Vector2Int(j, i));
                    col &= board.IsFilled(new Vector2Int(i, j)) || placed.Contains(new Vector2Int(i, j));
                }
                lines += (row ? 1 : 0) + (col ? 1 : 0);
            }
            return lines;
        }
#endif
    }
}
