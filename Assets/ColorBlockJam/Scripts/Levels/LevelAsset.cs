using UnityEngine;

namespace ColorBlockJam
{
    // Tek bir level = tek bir dosya. Designer bunu Unity'de seçip Inspector'daki
    // ızgara editörüyle (Editor/LevelAssetEditor.cs) kod yazmadan düzenler.
    [CreateAssetMenu(menuName = "Color Block Jam/Level", fileName = "Level_")]
    public class LevelAsset : ScriptableObject
    {
        public LevelDef level = new();
    }
}
