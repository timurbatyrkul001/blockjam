#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace FruitMerge
{
    // Sadece editörde, geliştirme için (oyuna girmez):
    //   Temp/fm_shot.request → Game ekranını Temp/fm_shot.png olarak kaydeder
    //   Temp/fm_auto.request → içindeki saniye kadar bilgisayar oynar (çift buldukça birleştirir)
    //   Temp/fm_level.request → içindeki bölümü açar ("map": harita, "lose": kaybetme kartı, "classic": Klasik,
    //     "setup 0:0,3:0 3": ağacı kur (yuva:meyve, sepet hakkı), "tap 3": yuvaya dokun (-1 boş yer), "drag 3 0", "finish")
    //   Temp/fm_click.request → içinde adı yazan görünür butona tıklar (ör. "Classic", "Pause", "Sound")
    //   Temp/fm_state.request → oyunun durumunu Temp/fm_state.txt'ye yazar
    public static class DevTools
    {
        const string ShotRequest = "Temp/fm_shot.request";
        const string ShotOutput = "Temp/fm_shot.png";
        const string AutoRequest = "Temp/fm_auto.request";
        const string LevelRequest = "Temp/fm_level.request";
        const string ClickRequest = "Temp/fm_click.request";
        const string StateRequest = "Temp/fm_state.request";

        static float nextCheck, autoUntil, nextAuto;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { nextCheck = autoUntil = nextAuto = 0f; }

        public static void Poll(FruitMergeGame game)
        {
            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + 0.5f;
                if (File.Exists(ShotRequest))
                {
                    File.Delete(ShotRequest);
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath(ShotOutput));
                }
                if (File.Exists(LevelRequest))
                {
                    string text = File.ReadAllText(LevelRequest).Trim();
                    File.Delete(LevelRequest);
                    if (int.TryParse(text, out int id)) game.StartLevel(id);
                    else if (text == "lose") game.DevShowLose();
                    else if (text == "classic") game.NewGame();
                    else if (text.StartsWith("setup ")) { var a = text.Substring(6).Split(' '); game.DevSetup(a[0], a.Length > 1 ? int.Parse(a[1]) : 3); }
                    else if (text.StartsWith("tap ")) game.DevTap(int.Parse(text.Substring(4)));
                    else if (text.StartsWith("drag ")) { var a = text.Substring(5).Split(' '); game.DevDrag(int.Parse(a[0]), int.Parse(a[1])); }
                    else if (text == "finish") game.DevFinish();
                    else game.ShowMap();
                }
                if (File.Exists(ClickRequest))
                {
                    string name = File.ReadAllText(ClickRequest).Trim();
                    File.Delete(ClickRequest);
                    Click(name);
                }
                if (File.Exists(StateRequest))
                {
                    File.Delete(StateRequest);
                    File.WriteAllText("Temp/fm_state.txt", game.DevState());
                }
                if (File.Exists(AutoRequest))
                {
                    float.TryParse(File.ReadAllText(AutoRequest).Trim(), out float seconds);
                    File.Delete(AutoRequest);
                    autoUntil = Time.unscaledTime + (seconds > 0f ? seconds : 10f);
                }
            }

            if (Time.unscaledTime < autoUntil && Time.unscaledTime >= nextAuto)
            {
                nextAuto = Time.unscaledTime + 0.45f;
                game.AutoMove();
            }
        }

        // Ekranda görünen, adı eşleşen en üstteki butona tıklar (gerçek onClick; aynı adlıların sonuncusu en üstte).
        static void Click(string name)
        {
            Button target = null;
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (b.name == name && b.isActiveAndEnabled && b.interactable && (target == null || IsAbove(b, target))) target = b;
            if (target == null) { Debug.Log($"[Dev] buton yok: {name}"); return; }
            Debug.Log($"[Dev] tıklandı: {PathOf(target.transform)}");
            target.onClick.Invoke();
        }

        static bool IsAbove(Component a, Component b)
        {
            // hiyerarşide sonra çizilen üsttedir
            var pa = a.transform; var pb = b.transform;
            return string.CompareOrdinal(Order(pa), Order(pb)) > 0;
        }

        static string Order(Transform t) => t.parent == null ? t.GetSiblingIndex().ToString("D4") : Order(t.parent) + "/" + t.GetSiblingIndex().ToString("D4");
        static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    }
}
#endif
