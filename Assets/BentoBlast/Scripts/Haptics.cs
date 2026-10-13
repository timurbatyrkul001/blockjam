using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace BentoBlast
{
    // iPhone titreşimi (Plugins/iOS/BentoHaptics.mm). Editörde ve diğer platformlarda hiçbir şey yapmaz.
    public static class Haptics
    {
        const string Key = "bb.haptics";

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(Key, 1) == 1;
            set => PlayerPrefs.SetInt(Key, value ? 1 : 0);
        }

        public static void Light() => Play(0);     // parça tutma / bırakma
        public static void Medium() => Play(1);    // satır patlama
        public static void Heavy() => Play(2);     // 3+ satır ya da büyük kombo
        public static void Success() => Play(3);   // talep tamamlandı

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void _bbHaptic(int style);
        static void Play(int style) { if (Enabled) _bbHaptic(style); }
#else
        static void Play(int style) { }
#endif
    }
}
