using PuzzleKit;
using UnityEngine;

namespace ColorBlockJam
{
    // Parmağın/farenin altındaki bloğu tutar ve onu hücre hücre parmağa doğru ilerletir.
    // Her adım BoardModel.TryStep ile kontrol edilir, bu yüzden blok asla duvarın/bloğun içinden geçemez.
    // Hücreler arasında ise blok parmağı yarım hücreye kadar görsel olarak takip eder (akıcı his).
    public class DragController : MonoBehaviour
    {
        const int MaxStepsPerFrame = 32;
        const float MaxVisualOffset = 0.45f;
        const float WrongGateCooldown = 0.5f;

        GameManager game;
        Camera cam;
        BlockView dragging;
        Vector3 grabWorld;
        Vector2Int grabCell;
        float lastWrongGate = -10f;

        public void Init(GameManager gameManager, Camera camera)
        {
            game = gameManager;
            cam = camera;
        }

        public void Cancel()
        {
            if (dragging != null)
            {
                dragging.SetDragOffset(Vector2.zero);
                dragging.SetLifted(false);
            }
            dragging = null;
        }

        void Update()
        {
            if (!game.IsPlaying) { Cancel(); return; }

            if (PointerInput.Pressed) TryGrab(PointerInput.Position);

            if (dragging == null) return;
            if (!PointerInput.Held)
            {
                Sfx.Play("drop", 0.5f);
                Cancel();
                return;
            }

            Drag(PointerInput.Position);
        }

        void TryGrab(Vector2 screenPos)
        {
            var ray = cam.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit, 200f)) return;

            var view = hit.collider.GetComponentInParent<BlockView>();
            if (view == null || view.State.Removed) return;
            if (!TryGetPlanePoint(screenPos, out grabWorld)) return;

            dragging = view;
            grabCell = view.State.Position;
            view.SetLifted(true);
            Sfx.Play("pickup", 0.7f);
        }

        void Drag(Vector2 screenPos)
        {
            if (!TryGetPlanePoint(screenPos, out var world)) return;

            var delta = world - grabWorld;
            var target = new Vector2(grabCell.x + delta.x, grabCell.y + delta.z);
            var desired = new Vector2Int(Mathf.RoundToInt(target.x), Mathf.RoundToInt(target.y));
            var state = dragging.State;

            for (int i = 0; i < MaxStepsPerFrame; i++)
            {
                var remaining = desired - state.Position;
                if (remaining == Vector2Int.zero) break;

                // Önce mesafesi büyük olan eksende dene, olmazsa diğerinde — köşelerden "kayarak" dönebilsin.
                var stepX = new Vector2Int(System.Math.Sign(remaining.x), 0);
                var stepY = new Vector2Int(0, System.Math.Sign(remaining.y));
                bool xFirst = Mathf.Abs(remaining.x) >= Mathf.Abs(remaining.y);

                var first = xFirst ? stepX : stepY;
                var second = xFirst ? stepY : stepX;
                var result = Step(first, out var gate);
                var dir = first;
                if ((result is StepResult.Blocked or StepResult.WrongGate) && second != Vector2Int.zero)
                {
                    var firstResult = result;
                    var firstGate = gate;
                    result = Step(second, out gate);
                    dir = second;
                    if (result == StepResult.Blocked && firstResult == StepResult.WrongGate)
                    {
                        result = firstResult;
                        gate = firstGate;
                        dir = first;
                    }
                }

                if (result == StepResult.WrongGate) { WrongGate(dir, gate); break; }
                if (result == StepResult.Blocked) break;
                if (result == StepResult.Exited)
                {
                    var exited = dragging;
                    dragging = null;
                    exited.SetDragOffset(Vector2.zero);
                    game.View.PulseGate(gate);
                    Sfx.Play("exit");
                    exited.PlayExit(dir, null);
                    game.OnBlockExiting();
                    return;
                }
                dragging.SyncPosition();
                Sfx.Play("step", 0.3f, pitchJitter: 0.12f, minInterval: 0.045f);
            }

            dragging.SetDragOffset(VisualOffset(target - (Vector2)state.Position));
        }

        StepResult Step(Vector2Int dir, out GateData gate)
        {
            gate = null;
            if (dir == Vector2Int.zero) return StepResult.Blocked;
            return game.Board.TryStep(dragging.State, dir, out gate);
        }

        // Parmak hücrenin içinde nereye doğru çekiyorsa blok o yöne yarım hücreye kadar eğilir —
        // ama sadece o yönde gerçekten yer varsa. Böylece duvara dayanınca titremez.
        Vector2 VisualOffset(Vector2 pull)
        {
            var state = dragging.State;
            float x = Mathf.Clamp(pull.x, -MaxVisualOffset, MaxVisualOffset);
            float y = Mathf.Clamp(pull.y, -MaxVisualOffset, MaxVisualOffset);
            if (x != 0f && !game.Board.CanStep(state, new Vector2Int(x > 0 ? 1 : -1, 0))) x = 0f;
            if (y != 0f && !game.Board.CanStep(state, new Vector2Int(0, y > 0 ? 1 : -1))) y = 0f;
            return new Vector2(x, y);
        }

        void WrongGate(Vector2Int dir, GateData gate)
        {
            if (Time.time - lastWrongGate < WrongGateCooldown) return;
            lastWrongGate = Time.time;
            dragging.Shake(dir);
            Sfx.Play("wrong", 0.8f);
            game.View.FlashGate(gate);
        }

        // Ekrandaki noktayı blokların üst yüzeyi hizasındaki yatay düzleme yansıtır.
        bool TryGetPlanePoint(Vector2 screenPos, out Vector3 point)
        {
            var plane = new Plane(Vector3.up, new Vector3(0f, 0.4f, 0f));
            var ray = cam.ScreenPointToRay(screenPos);
            if (plane.Raycast(ray, out float dist)) { point = ray.GetPoint(dist); return true; }
            point = default;
            return false;
        }
    }
}
