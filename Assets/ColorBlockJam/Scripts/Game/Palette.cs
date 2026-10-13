using System.Collections.Generic;
using UnityEngine;

namespace ColorBlockJam
{
    public static class Palette
    {
        // Yemek teması: board ahşap bir masanın üstünde, gri-mavi bir tepside durur.
        public static readonly Color Background = Hex("#7A4E2D");
        public static readonly Color TableA = Hex("#B9824F");
        public static readonly Color TableB = Hex("#AD7745");
        public static readonly Color BoardBase = Hex("#3B4252");
        public static readonly Color Tile = Hex("#7D8699");
        public static readonly Color TileAlt = Hex("#757E91");
        public static readonly Color Border = Hex("#4A5265");
        public static readonly Color Wall = Hex("#5D6679");
        public static readonly Color TrayInner = Hex("#F3E7D3");   // bento kabının iç zemini (krem)

        public static Color Of(BlockColor c) => c switch
        {
            BlockColor.Red => Hex("#F2445B"),
            BlockColor.Blue => Hex("#3D7BF5"),
            BlockColor.Green => Hex("#35C46A"),
            BlockColor.Yellow => Hex("#FFC43D"),
            BlockColor.Purple => Hex("#9B5CF6"),
            BlockColor.Orange => Hex("#FF8A3D"),
            BlockColor.Pink => Hex("#FF6FB5"),
            _ => Hex("#2EC9D9"),
        };

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        // Her renk+parlaklık için tek materyal; yüzlerce küp aynı materyali paylaşır.
        static readonly Dictionary<(Color, float), Material> cache = new();
        static Material baseMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cache.Clear();
            baseMaterial = null;
        }

        public static Material Mat(Color color, float smoothness = 0.5f)
        {
            if (cache.TryGetValue((color, smoothness), out var m) && m != null) return m;

            m = new Material(BaseMaterial) { color = color };
            m.SetFloat("_Smoothness", smoothness);  // URP Lit
            m.SetFloat("_Glossiness", smoothness);  // Built-in Standard
            cache[(color, smoothness)] = m;
            return m;
        }

        // Editor/CBJMaterialSetup.cs bu materyali Resources'a otomatik oluşturur;
        // böylece shader iOS build'inde de pakete dahil olur.
        static Material BaseMaterial
        {
            get
            {
                if (baseMaterial != null) return baseMaterial;
                baseMaterial = Resources.Load<Material>("CBJ_Base");
                if (baseMaterial == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    baseMaterial = new Material(shader);
                }
                return baseMaterial;
            }
        }
    }
}
