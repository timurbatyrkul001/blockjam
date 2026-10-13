using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace ColorBlockJam.EditorTools
{
    // Oyun sahnesi (Assets/Scenes/Game.unity) boştur; kamera, ışık, board ve UI'ı GameBootstrap kodla kurar.
    // Bu script sahneyi build listesine koyar ve açık sahnenin dosyası diskte yoksa Game.unity'yi açar.
    [InitializeOnLoad]
    static class CBJSceneSetup
    {
        const string GameScene = "Assets/Scenes/Game.unity";

        static CBJSceneSetup() => EditorApplication.delayCall += Setup;

        static void Setup()
        {
            if (!File.Exists(GameScene) || EditorApplication.isPlayingOrWillChangePlaymode) return;

            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0 || scenes[0].path != GameScene)
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(GameScene, true) };

            var active = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path) || !File.Exists(active.path))
                EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
        }
    }
}
