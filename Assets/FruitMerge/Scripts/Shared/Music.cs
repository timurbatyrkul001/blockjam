using UnityEngine;

namespace FruitMerge
{
    // Arka plan müziği: Resources/Audio/music varsa döngüde çalar. Dosya yoksa sessiz kalır.
    public static class Music
    {
        static AudioSource source;
        static bool enabled = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { source = null; enabled = true; }

        public static bool Enabled
        {
            get => enabled;
            set { enabled = value; Apply(); }
        }

        static void Apply()
        {
            if (source == null)
            {
                if (!enabled) return;
                var clip = Resources.Load<AudioClip>("Audio/music");
                if (clip == null) return;
                var go = new GameObject("Music");
                Object.DontDestroyOnLoad(go);
                source = go.AddComponent<AudioSource>();
                source.clip = clip;
                source.loop = true;
                source.volume = 0.18f;   // Codex: ~%15; efektlerin altında kalsın
            }
            if (enabled && !source.isPlaying) source.Play();
            else if (!enabled) source.Pause();
        }
    }
}
