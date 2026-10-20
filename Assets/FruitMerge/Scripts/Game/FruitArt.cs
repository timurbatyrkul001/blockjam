using System;
using System.Collections.Generic;
using UnityEngine;

namespace FruitMerge
{
    // Görseller Resources'tan yüklenir; yoksa yer tutucu (renkli daire) kullanılır.
    // Codex'in çıktıları: Resources/Fruits/fruit_{seviye}_{ad}.png, Resources/Art/{tree, background, basket, ...}.png
    public enum Weight { Medium, SemiBold, Bold }

    public static class FruitArt
    {
        // Fredoka (OFL, Resources/Fonts): Türkçe Ğ/İ/Ş bileşik glif olarak eklendi (scratchpad/font/turkish.py).
        static readonly Font[] fonts = new Font[3];

        public static Font Font(Weight weight)
        {
            int i = (int)weight;
            if (fonts[i] != null) return fonts[i];
            fonts[i] = Resources.Load<Font>($"Fonts/Fredoka-{weight}")
                       ?? Resources.Load<Font>("Fonts/KenneyFuture")
                       ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return fonts[i];
        }

        public static readonly Color CreamText = Hex("#FFF7DC");
        public static readonly Color Outline = Hex("#63351E");
        public static readonly Color[] Colors =
        {
            Hex("#E5394B"), Hex("#FF6F91"), Hex("#8E5BD6"), Hex("#FFB627"),
            Hex("#E84A3A"), Hex("#FFB38A"), Hex("#A8D86B"), Hex("#3FAE5A"),
        };

        public static readonly Color Cream = Hex("#FFF6E3");
        public static readonly Color Wood = Hex("#7A4E2D");
        public static readonly Color DarkText = Hex("#5A3A22");
        public static readonly Color Gold = Hex("#FFC43D");

        static readonly Dictionary<string, Sprite> cache = new();
        static Sprite circle, star, ring;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { cache.Clear(); circle = star = ring = null; System.Array.Clear(fonts, 0, fonts.Length); }

        // Seviyeye göre ekrandaki boy (canvas birimi): karpuz en büyük.
        public static float SizeFor(int tier) => 223f + tier * 4f;   // rahat oynanış için ~%20 büyük

        public static Sprite Fruit(int tier) => Load($"Fruits/fruit_{tier}_{FruitChain.Names[tier]}");

        public static Sprite Load(string path)
        {
            if (cache.TryGetValue(path, out var s)) return s;
            s = Resources.Load<Sprite>(path);
            cache[path] = s;
            return s;
        }

        // Yumuşak kenarlı beyaz daire; yer tutucu meyve ve ağaç tacı için.
        public static Sprite Circle()
        {
            if (circle != null) return circle;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            float r = size * 0.5f - 1f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                byte a = (byte)(Mathf.Clamp01(r - d + 0.5f) * 255);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return circle;
        }

        // Beş köşeli yıldız (Codex'in star_full.png'si gelene kadar).
        public static Sprite Star()
        {
            if (star != null) return star;
            star = Procedural(128, (x, y) =>
            {
                // düz kenarlı yıldız: tepe (dış yarıçap) ile vadi (iç yarıçap) arasındaki doğruya göre
                const float outer = 60f, inner = 27f, half = Mathf.PI / 5f;
                float dx = x - 64f, dy = y - 60f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Abs(Mathf.Repeat(Mathf.Atan2(dx, dy) + half, 2f * half) - half);   // 0 tepe, half vadi
                float edge = outer * inner * Mathf.Sin(half) / (outer * Mathf.Sin(a) + inner * Mathf.Sin(half - a));
                return Mathf.Clamp01(edge - r + 0.75f);
            });
            return star;
        }

        // Halka (seçili bölüm çerçevesi).
        public static Sprite Ring()
        {
            if (ring != null) return ring;
            ring = Procedural(128, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(64f, 64f));
                return Mathf.Clamp01(63f - d + 0.5f) * Mathf.Clamp01(d - 52f + 0.5f);
            });
            return ring;
        }

        static Sprite Procedural(int size, System.Func<float, float, float> alpha)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = new Color32(255, 255, 255, (byte)(alpha(x + 0.5f, y + 0.5f) * 255));
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // Ağaçtaki 12 yuva: ağaç resminin sol-üst köşesine göre 0..1 oranları.
        // Codex'in tree_slots.json'u varsa o kullanılır.
        public static Vector2[] Slots()
        {
            var json = Resources.Load<TextAsset>("Art/tree_slots");
            if (json != null)
            {
                try
                {
                    var file = JsonUtility.FromJson<SlotFile>(json.text);
                    if (file?.slots != null && file.slots.Length >= 8)
                    {
                        var list = new Vector2[file.slots.Length];
                        for (int i = 0; i < list.Length; i++) list[i] = new Vector2(file.slots[i].x, file.slots[i].y);
                        return list;
                    }
                }
                catch (Exception e) { Debug.LogWarning($"[FruitMerge] tree_slots.json okunamadı: {e.Message}"); }
            }
            return new[]
            {
                new Vector2(0.30f, 0.12f), new Vector2(0.52f, 0.08f), new Vector2(0.74f, 0.13f),
                new Vector2(0.15f, 0.27f), new Vector2(0.39f, 0.25f), new Vector2(0.62f, 0.26f), new Vector2(0.86f, 0.28f),
                new Vector2(0.24f, 0.42f), new Vector2(0.49f, 0.40f), new Vector2(0.75f, 0.43f),
                new Vector2(0.12f, 0.56f), new Vector2(0.88f, 0.56f),
            };
        }

        [Serializable] class SlotFile { public SlotPos[] slots; }
        [Serializable] class SlotPos { public float x, y; }

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
