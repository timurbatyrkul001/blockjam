#if UNITY_EDITOR
using System.IO;
using UnityEngine;

namespace ColorBlockJam
{
    // Sadece editörde, geliştirme için: Play'deki oyunu dışarıdan görmek.
    //   Temp/cbj_shot.request → Game ekranını Temp/cbj_shot.png olarak kaydeder
    //   Temp/cbj_win.request  → kazanma ekranını önizler (ilerleme/coin değişmez)
    //   Temp/cbj_hide.request → paneli kapatır
    //   Temp/cbj_level.request → içindeki numaralı level'ı açar (ilerleme kaydedilmez)
    public class DevScreenshot : MonoBehaviour
    {
        const string Request = "Temp/cbj_shot.request";
        const string WinPreview = "Temp/cbj_win.request";
        const string HidePreview = "Temp/cbj_hide.request";
        const string LevelRequest = "Temp/cbj_level.request";
        const string Output = "Temp/cbj_shot.png";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (FindAnyObjectByType<DevScreenshot>() != null) return;
            var go = new GameObject("DevScreenshot") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<DevScreenshot>();
        }

        float nextCheck;

        void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 0.5f;

            var hud = FindAnyObjectByType<Hud>();
            if (File.Exists(WinPreview))
            {
                File.Delete(WinPreview);
                if (hud != null) hud.ShowResult(true, "+20 coin", hud.HidePanel);
            }
            if (File.Exists(HidePreview))
            {
                File.Delete(HidePreview);
                if (hud != null) hud.HidePanel();
            }
            if (File.Exists(LevelRequest))
            {
                var text = File.ReadAllText(LevelRequest).Trim();
                File.Delete(LevelRequest);
                var game = FindAnyObjectByType<GameManager>();
                if (game != null && int.TryParse(text, out int number)) game.LoadLevel(number - 1);
            }
            if (!File.Exists(Request)) return;
            File.Delete(Request);
            ScreenCapture.CaptureScreenshot(Output);
        }
    }
}
#endif
