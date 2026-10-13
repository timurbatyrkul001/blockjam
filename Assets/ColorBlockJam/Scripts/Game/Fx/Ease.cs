using UnityEngine;

namespace ColorBlockJam
{
    // Basit easing fonksiyonları; t her zaman 0..1 aralığında beklenir.
    public static class Ease
    {
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float InQuad(float t) => t * t;
        public static float OutCubic(float t) { float u = 1f - t; return 1f - u * u * u; }

        public static float OutBack(float t, float overshoot = 1.70158f)
        {
            float u = t - 1f;
            return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
        }

        // 1'den başlayıp sönümlenerek 0'a giden salınım (squash/stretch "punch" için).
        public static float Punch(float t, float oscillations = 2.5f)
        {
            float u = 1f - t;
            return u * u * Mathf.Cos(t * Mathf.PI * oscillations);
        }

        // 0 → 1 → 0 tepe eğrisi.
        public static float Bump(float t) => Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
    }
}
