using System.Collections.Generic;
using UnityEngine;

namespace ColorBlockJam
{
    // Oyundaki level sırası. Listede sürükleyerek sırayı değiştirmek level sırasını değiştirir.
    // Resources klasöründe durur ki oyun build'de de bulabilsin.
    [CreateAssetMenu(menuName = "Color Block Jam/Level Catalog", fileName = "LevelCatalog")]
    public class LevelCatalog : ScriptableObject
    {
        public const string ResourcePath = "LevelCatalog";

        public List<LevelAsset> levels = new();
    }
}
