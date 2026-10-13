using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ColorBlockJam.EditorTools
{
    // iOS build'leri. Menüden ya da (geliştirme sırasında) Temp/cbj_build_sim.request dosyasıyla tetiklenir;
    // sonuç Temp/cbj_build.result dosyasına yazılır.
    [InitializeOnLoad]
    static class CBJBuild
    {
        const string GameScene = "Assets/Scenes/Game.unity";
        const string BentoScene = "Assets/Scenes/BentoBlast.unity";
        const string SimRequest = "Temp/cbj_build_sim.request";
        const string BentoSimRequest = "Temp/bb_build_sim.request";
        const string ResultFile = "Temp/cbj_build.result";
        public const string SimOutput = "Builds/iOS-Simulator";
        public const string DeviceOutput = "Builds/iOS-Device";
        public const string BentoSimOutput = "Builds/iOS-Simulator-Bento";

        // Aynı projeden iki ayrı uygulama: sahne, isim ve bundle ID farklı.
        class App
        {
            public readonly string Scene, Name, BundleId, Icon;
            public App(string scene, string name, string bundleId, string icon = null)
            {
                Scene = scene; Name = name; BundleId = bundleId; Icon = icon;
            }
        }
        static readonly App ColorBlock = new(GameScene, "Color Block Puzzle", "com.ggtech.colorblockpuzzle");
        static readonly App Bento = new(BentoScene, "Bento Blast", "com.ggtech.bentoblast", "Assets/BentoBlast/Icon/app_icon_1024.png");

        static double nextPoll;

        static CBJBuild() => EditorApplication.update += Poll;

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 1.0;
            string request = File.Exists(SimRequest) ? SimRequest : File.Exists(BentoSimRequest) ? BentoSimRequest : null;
            if (request == null) return;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;   // önce Play'den çık, sonraki turda build al
                return;
            }
            File.Delete(request);
            if (request == SimRequest) BuildSimulator();
            else BuildBentoSimulator();
        }

        [MenuItem("Color Block Jam/Build: iOS Simülatör", priority = 40)]
        static void BuildSimulator() => Build(ColorBlock, simulator: true, SimOutput);

        [MenuItem("Color Block Jam/Build: iPhone (Xcode projesi)", priority = 41)]
        static void BuildDevice() => Build(ColorBlock, simulator: false, DeviceOutput);

        [MenuItem("Bento Blast/Build: iOS Simülatör", priority = 40)]
        static void BuildBentoSimulator() => Build(Bento, simulator: true, BentoSimOutput);

        static void Build(App app, bool simulator, string output)
        {
            ApplyPlayerSettings(app, simulator);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { app.Scene },
                locationPathName = output,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = BuildOptions.None,
            });

            var s = report.summary;
            var text = $"{s.result} | {s.totalErrors} hata | {s.totalTime.TotalSeconds:0}s | {Path.GetFullPath(output)}";
            File.WriteAllText(ResultFile, text);
            Debug.Log($"[ColorBlockJam] iOS build: {text}");
        }

        static void ApplyPlayerSettings(App app, bool simulator)
        {
            PlayerSettings.companyName = "GG Tech";
            PlayerSettings.productName = app.Name;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, app.BundleId);
            PlayerSettings.bundleVersion = "0.1.0";

            // Tek 1024'lük ikon; Unity diğer boyutları kendisi küçültür. İkonu olmayan uygulamada boş kalır.
            var target = UnityEditor.Build.NamedBuildTarget.iOS;
            var icon = app.Icon != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(app.Icon) : null;
            var icons = new Texture2D[PlayerSettings.GetIconSizes(target, IconKind.Application).Length];
            for (int i = 0; i < icons.Length; i++) icons[i] = icon;
            PlayerSettings.SetIcons(target, icons, IconKind.Application);

            // Oyun sadece dikey oynanır.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            PlayerSettings.iOS.sdkVersion = simulator ? iOSSdkVersion.SimulatorSDK : iOSSdkVersion.DeviceSDK;
            if (simulator) PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
        }
    }
}
