using System.Collections;
using PuzzleKit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace ColorBlockJam
{
    // Bu oyuna özel yapıştırıcı: level'ı kurar, LevelSession'ın (ortak akış) olaylarına göre
    // ekranları gösterir, coin ve ilerlemeyi Progress'e (ortak kayıt) yazar.
    // Sahnede hiçbir şey kurmana gerek yok — GameBootstrap bunu Play'e basınca otomatik oluşturur.
    public class GameManager : MonoBehaviour
    {
        public BoardModel Board { get; private set; }
        public BoardView View { get; private set; }
        public bool IsPlaying => session.IsPlaying;

        readonly LevelSession session = new();
        LevelDef current;
        int levelIndex;
        float lastAspect;
        Camera cam;
        DragController drag;
        Hud hud;

        void Awake()
        {
            Application.targetFrameRate = 60;
            SetupCamera();
            SetupLighting();
            EnsureEventSystem();
            hud = Hud.Create(this);
            drag = gameObject.AddComponent<DragController>();
            drag.Init(this, cam);

            session.Won += OnWon;
            session.Lost += OnLost;
            Progress.CoinsChanged += hud.SetCoins;
        }

        void OnDestroy() => Progress.CoinsChanged -= hud.SetCoins;

        void Start()
        {
            int testLevel = LevelDatabase.ConsumeTestLevel();
            LoadLevel(testLevel >= 0 ? testLevel : Progress.Level);
        }

        public void LoadLevel(int index) => LoadLevel(index, attempts: 0);

        void LoadLevel(int index, int attempts)
        {
            levelIndex = Mathf.Max(0, index);
            drag.Cancel();
            StopAllCoroutines();
            if (View != null) Destroy(View.gameObject);

            current = LevelDatabase.Get(levelIndex);
            LevelData data;
            try { data = current.Build(); }
            catch (LevelFormatException e)
            {
                Debug.LogError($"[ColorBlockJam] Level {levelIndex + 1} hatalı, atlanıyor:\n{e.Message}");
                if (attempts < LevelDatabase.Count) LoadLevel(levelIndex + 1, attempts + 1);
                return;
            }

            Board = new BoardModel(data);
            FitCamera();   // görsel kurulum hata verse bile kamera board'a baksın
            View = BoardView.Create(Board);

            session.Begin(current.timeLimit);
            hud.SetLevel(levelIndex + 1);
            hud.SetTime(session.TimeLeft);
            hud.SetCoins(Progress.Coins);
            hud.HidePanel();
        }

        public void Restart() => LoadLevel(levelIndex);

        void Update()
        {
            if (!Mathf.Approximately(cam.aspect, lastAspect)) FitCamera();
            if (!session.IsPlaying) return;
            session.Tick(Time.deltaTime);
            hud.SetTime(session.TimeLeft);
        }

        // DragController, bir blok kapıdan çıkmaya başladığı anda çağırır.
        // Son blok çıktıysa süre hemen durur; ekran animasyon bitince açılır.
        public void OnBlockExiting()
        {
            if (Board.RemainingBlocks == 0) session.Complete();
        }

        void OnWon()
        {
            Progress.Level = levelIndex + 1;
            Progress.AddCoins(current.coinReward);
            StartCoroutine(ShowAfter(0.6f, () =>
            {
                Sfx.Play("win");
                hud.ShowResult(true, $"+{current.coinReward} coin", () => LoadLevel(levelIndex + 1));
            }));
        }

        void OnLost()
        {
            Sfx.Play("lose");
            hud.ShowResult(false, $"Level {levelIndex + 1}", Restart);
        }

        IEnumerator ShowAfter(float seconds, System.Action action)
        {
            yield return new WaitForSeconds(seconds);
            action();
        }

        void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            // Sesin duyulması için sahnede bir "kulak" gerekir; boş sahnede kamerayla birlikte gelmez.
            if (FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Background;
            cam.fieldOfView = 40f;
        }

        // Board'u ekrana sığdırır; telefon dik tutulunca genişlik belirleyici olur.
        void FitCamera()
        {
            if (Board == null) return;
            lastAspect = cam.aspect;

            const float pitch = 58f;
            float boardW = Board.Width + 2f, boardH = Board.Height + 2f;
            float vHalf = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float hHalf = Mathf.Atan(Mathf.Tan(vHalf) * cam.aspect);

            float distForWidth = boardW * 0.5f / Mathf.Tan(hHalf);
            float distForHeight = (boardH * 0.5f * Mathf.Sin(pitch * Mathf.Deg2Rad) + 0.5f) / Mathf.Tan(vHalf);
            float dist = Mathf.Max(distForWidth, distForHeight) * 1.1f + 1f;

            var rot = Quaternion.Euler(pitch, 0f, 0f);
            var focus = new Vector3(0f, 0f, boardH * 0.05f);  // board'u biraz aşağı it, üstte HUD'a yer kalsın
            cam.transform.SetPositionAndRotation(focus - rot * Vector3.forward * dist, rot);
        }

        void SetupLighting()
        {
            Light sun = null;
            foreach (var l in FindObjectsByType<Light>())
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null) sun = new GameObject("Sun").AddComponent<Light>();

            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            sun.color = new Color(1f, 0.95f, 0.88f);   // sıcak mutfak ışığı
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;

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
    }
}
