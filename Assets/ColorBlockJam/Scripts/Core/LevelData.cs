using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorBlockJam
{
    // Board kenarları. Kapılar bu kenarlardan birinin üzerinde durur.
    public enum Side { Left, Right, Bottom, Top }

    public enum BlockColor { Red, Blue, Green, Yellow, Purple, Orange, Pink, Cyan }

    // Ok işaretli bloklar sadece tek eksende hareket edebilir.
    public enum MoveAxis { Both, Horizontal, Vertical }

    [Serializable]
    public class GateData
    {
        public Side side;
        public int start;   // Left/Right kenarında y, Bottom/Top kenarında x
        public int length;  // kaç hücre genişliğinde
        public BlockColor color;

        public int End => start + length - 1;
    }

    [Serializable]
    public class BlockData
    {
        public Vector2Int position;  // şeklin sol-alt köşesinin hücresi
        public Vector2Int[] shape;   // position'a göre hücre ofsetleri
        public BlockColor color;
        public MoveAxis axis;
    }

    [Serializable]
    public class LevelData
    {
        public int width;
        public int height;
        public float timeLimit;
        public List<Vector2Int> walls = new();
        public List<GateData> gates = new();
        public List<BlockData> blocks = new();
    }
}
