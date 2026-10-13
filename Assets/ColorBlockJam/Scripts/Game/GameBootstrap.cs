using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorBlockJam
{
    // Play'e basınca oyunu kendiliğinden kurar. Aynı projedeki diğer oyunların sahnelerinde çalışmaz.
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (SceneManager.GetActiveScene().name == "BentoBlast") return;
            if (Object.FindAnyObjectByType<GameManager>() != null) return;
            new GameObject("ColorBlockJam").AddComponent<GameManager>();
        }
    }
}
