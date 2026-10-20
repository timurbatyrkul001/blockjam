using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FruitMerge.EditorTools
{
    // Sahne kurulumu, menü ve iOS build. Sahne boştur; oyun FruitMergeBootstrap ile koddan kurulur.
    [InitializeOnLoad]
    static class FMEditor
    {
        public const string ScenePath = "Assets/Scenes/FruitMerge.unity";
        const string IconPath = "Assets/FruitMerge/Icon/app_icon_1024.png";
        const string SimRequest = "Temp/fm_build_sim.request";
        const string PortraitRequest = "Temp/fm_portrait.request";
        const string ResultFile = "Temp/fm_build.result";
        const string SimOutput = "Builds/iOS-Simulator";
        const string DeviceOutput = "Builds/iOS-Device";

        static double nextPoll;

        static FMEditor()
        {
            EditorApplication.delayCall += EnsureScene;
            EditorApplication.delayCall += FixGameViewDisplay;
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode) FixGameViewDisplay(); };
            EditorApplication.update += Poll;
        }

        // Sahne yoksa oluşturur, build listesine ekler ve açar.
        static void EnsureScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log($"[FruitMerge] Sahne oluşturuldu: {ScenePath}");
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Fruit Merge/Sahneyi aç", priority = 0)]
        static void OpenScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EnsureScene();
        }

        // Game penceresini iPhone dikey (1080x2340) yapar; Unity'nin iç API'si yansıma ile kullanılır.
        [MenuItem("Fruit Merge/Game görünümü: iPhone dikey", priority = 10)]
        static void PortraitGameView() => SetGameViewSize(1080, 2340);

        static void SetGameViewSize(int w, int h)
        {
            string label = $"Fruit Merge {w}x{h}";
            try
            {
                var asm = typeof(Editor).Assembly;
                var sizesType = asm.GetType("UnityEditor.GameViewSizes");
                var instance = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
                var groupType = (GameViewSizeGroupType)sizesType.GetProperty("currentGroupType").GetValue(instance);
                var group = sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { (int)groupType });

                int index = -1;
                var texts = (string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group, null);
                for (int i = 0; i < texts.Length; i++) if (texts[i].StartsWith(label)) index = i;
                if (index < 0)
                {
                    var sizeType = asm.GetType("UnityEditor.GameViewSize");
                    var kindType = asm.GetType("UnityEditor.GameViewSizeType");
                    var size = sizeType.GetConstructor(new[] { kindType, typeof(int), typeof(int), typeof(string) })
                        .Invoke(new[] { Enum.Parse(kindType, "FixedResolution"), w, h, (object)label });
                    group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
                }

                var gameViewType = asm.GetType("UnityEditor.GameView");
                var gameView = EditorWindow.GetWindow(gameViewType);
                gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Invoke(gameView, new object[] { index, null });
                FixGameViewDisplay();
                Debug.Log($"[FruitMerge] Game görünümü {w}x{h}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FruitMerge] Game görünümü ayarlanamadı: {e.Message}");
            }
        }

        // Oyunun tek kamerası Display 1'e çizer. Game penceresi Display 2'de kalırsa "No cameras rendering" görünür;
        // açık bütün Game pencerelerini Display 1'e döndürür (Unity'nin iç alanı yansıma ile).
        [MenuItem("Fruit Merge/Game görünümü: Display 1", priority = 11)]
        static void FixGameViewDisplay()
        {
            var gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null) return;
            foreach (var view in Resources.FindObjectsOfTypeAll(gameViewType))
            {
                for (var t = view.GetType(); t != null; t = t.BaseType)
                {
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                    var prop = t.GetProperty("targetDisplay", flags);
                    if (prop != null && prop.GetSetMethod(true) != null)
                    {
                        if ((int)prop.GetValue(view) != 0)
                        {
                            prop.GetSetMethod(true).Invoke(view, new object[] { 0 });
                            Debug.Log("[FruitMerge] Game görünümü Display 1'e alındı");
                        }
                        break;
                    }
                    var field = t.GetField("m_TargetDisplay", flags);
                    if (field != null)
                    {
                        if ((int)field.GetValue(view) != 0)
                        {
                            field.SetValue(view, 0);
                            ((EditorWindow)view).Repaint();
                            Debug.Log("[FruitMerge] Game görünümü Display 1'e alındı");
                        }
                        break;
                    }
                }
            }
        }

        [MenuItem("Fruit Merge/En iyi skoru sıfırla", priority = 20)]
        static void ResetBest() => PlayerPrefs.DeleteKey("fm.best");

        [MenuItem("Fruit Merge/Bölümler: hepsini aç", priority = 21)]
        static void UnlockAllLevels() => LevelProgress.UnlockAll();

        [MenuItem("Fruit Merge/Bölümler: ilerlemeyi sıfırla", priority = 22)]
        static void ResetLevels() => LevelProgress.Reset();

        [MenuItem("Fruit Merge/Build: iOS Simülatör", priority = 40)]
        static void BuildSimulator() => Build(simulator: true, SimOutput);

        [MenuItem("Fruit Merge/Build: iPhone (Xcode projesi)", priority = 41)]
        static void BuildDevice() => Build(simulator: false, DeviceOutput);

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1.0;
            if (File.Exists(PortraitRequest))
            {
                // içerik "750x1334" gibi bir boyutsa onu, boşsa 1080x2340'ı seçer (dar ekran testi için)
                var parts = File.ReadAllText(PortraitRequest).Trim().Split('x');
                File.Delete(PortraitRequest);
                if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h)) SetGameViewSize(w, h);
                else PortraitGameView();
            }
            if (!File.Exists(SimRequest)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;   // önce Play'den çık, sonraki turda build al
                return;
            }
            File.Delete(SimRequest);
            BuildSimulator();
        }

        static void Build(bool simulator, string output)
        {
            EnsureScene();
            PlayerSettings.companyName = "GG Tech";
            PlayerSettings.productName = "Fruit Merge";
            var target = UnityEditor.Build.NamedBuildTarget.iOS;
            PlayerSettings.SetApplicationIdentifier(target, "com.ggtech.fruitmerge");
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.iOS.sdkVersion = simulator ? iOSSdkVersion.SimulatorSDK : iOSSdkVersion.DeviceSDK;
            if (simulator) PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;

            // Tek 1024'lük ikon; Unity diğer boyutları kendisi küçültür.
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            var icons = new Texture2D[PlayerSettings.GetIconSizes(target, IconKind.Application).Length];
            for (int i = 0; i < icons.Length; i++) icons[i] = icon;
            PlayerSettings.SetIcons(target, icons, IconKind.Application);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None,
            });
            var s = report.summary;
            var text = $"{s.result} | {s.totalErrors} hata | {s.totalTime.TotalSeconds:0}s | {Path.GetFullPath(output)}";
            File.WriteAllText(ResultFile, text);
            Debug.Log($"[FruitMerge] iOS build: {text}");
        }
    }

    // Resources/Art, Resources/Fruits ve Resources/UI altındaki PNG'ler sprite olarak içe aktarılır.
    class FMImportRules : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            var p = assetPath.Replace('\\', '/');
            if (!p.StartsWith("Assets/FruitMerge/Resources/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = p.EndsWith("map_background.png") ? 8192 : p.EndsWith("background.png") ? 4096 : 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            // panel ve butonlar 9-slice: köşeler esnemesin
            if (p.EndsWith("Art/panel_wood.png")) importer.spriteBorder = new Vector4(48, 48, 48, 48);
            if (p.EndsWith("UI/button_green.png")) importer.spriteBorder = new Vector4(32, 40, 32, 32);
            if (p.EndsWith("UI/panel.png")) importer.spriteBorder = new Vector4(40, 48, 40, 40);
        }
    }
}
