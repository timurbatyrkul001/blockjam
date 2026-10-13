using UnityEditor;
using UnityEditor.SceneManagement;

namespace BentoBlast.EditorTools
{
    // Aynı projedeki iki oyun arasında geçiş: menüden sahneyi açar (kaydedilmemiş değişiklik varsa sorar).
    static class BentoEditorMenu
    {
        [MenuItem("Bento Blast/Bento Blast sahnesini aç", priority = 0)]
        static void OpenBento() => Open("Assets/Scenes/BentoBlast.unity");

        [MenuItem("Bento Blast/Color Block sahnesini aç", priority = 1)]
        static void OpenColorBlock() => Open("Assets/Scenes/Game.unity");

        [MenuItem("Bento Blast/En iyi skoru sıfırla", priority = 20)]
        static void ResetBest() => UnityEngine.PlayerPrefs.DeleteKey("bb.best");

        static void Open(string path)
        {
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
    }
}
