using System.Collections.Generic;
using UnityEngine;

namespace PuzzleKit
{
    // Ses efektleri: Sfx.Play("exit"). Dosyalar Resources/Audio altından adıyla yüklenir.
    // Her çalışta perdesi biraz oynar ki aynı ses tekrar edince robotik duyulmasın.
    public static class Sfx
    {
        const int Voices = 8;

        static readonly Dictionary<string, AudioClip> clips = new();
        static readonly Dictionary<string, float> lastPlayed = new();
        static AudioSource[] sources;
        static int next;

        public static bool Muted { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            clips.Clear();
            lastPlayed.Clear();
            sources = null;
        }

        // minInterval: aynı ses bu kadar saniye içinde tekrar çalmaz (ör. hızlı sürüklemede adım sesi).
        public static void Play(string name, float volume = 1f, float pitchJitter = 0.06f, float minInterval = 0f)
        {
            if (Muted) return;
            if (minInterval > 0f && lastPlayed.TryGetValue(name, out var last) && Time.unscaledTime - last < minInterval) return;

            var clip = Load(name);
            if (clip == null) return;
            lastPlayed[name] = Time.unscaledTime;

            EnsureSources();
            var source = sources[next];
            next = (next + 1) % Voices;
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.PlayOneShot(clip, volume);
        }

        static AudioClip Load(string name)
        {
            if (clips.TryGetValue(name, out var clip)) return clip;
            clip = Resources.Load<AudioClip>($"Audio/{name}");
            if (clip == null) Debug.LogWarning($"[Sfx] Resources/Audio/{name} bulunamadı");
            clips[name] = clip;
            return clip;
        }

        static void EnsureSources()
        {
            if (sources != null && sources[0] != null) return;
            var go = new GameObject("Sfx");
            Object.DontDestroyOnLoad(go);
            sources = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                sources[i] = go.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
            }
        }
    }
}
