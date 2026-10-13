using System.Collections.Generic;
using UnityEngine;

namespace ColorBlockJam
{
    public enum StepResult { Moved, Blocked, Exited, WrongGate }

    public class BlockState
    {
        public readonly int Id;
        public readonly Vector2Int[] Shape;
        public readonly BlockColor Color;
        public readonly MoveAxis Axis;
        public readonly Vector2Int Size;   // şeklin kapladığı kutunun boyutu

        public Vector2Int Position;
        public bool Removed;

        public BlockState(int id, BlockData data)
        {
            Id = id;
            Shape = data.shape;
            Color = data.color;
            Axis = data.axis;
            Position = data.position;

            var max = Vector2Int.zero;
            foreach (var s in Shape) max = Vector2Int.Max(max, s);
            Size = max + Vector2Int.one;
        }

        public bool Allows(Vector2Int dir) => Axis switch
        {
            MoveAxis.Horizontal => dir.y == 0,
            MoveAxis.Vertical => dir.x == 0,
            _ => true,
        };
    }

    // Oyunun saf mantığı: hangi hücre dolu, blok bir adım gidebilir mi, kapıdan çıkar mı.
    // Görselden bağımsızdır; view'lar ve LevelSolver sadece bu modelin sonucunu kullanır.
    public class BoardModel
    {
        public readonly int Width;
        public readonly int Height;
        public readonly List<BlockState> Blocks = new();
        public readonly List<GateData> Gates;
        public int RemainingBlocks { get; private set; }

        readonly bool[,] walls;
        readonly int[,] occupancy;   // hücredeki blok id'si, boşsa -1

        public BoardModel(LevelData level)
        {
            Width = level.width;
            Height = level.height;
            Gates = level.gates;
            walls = new bool[Width, Height];
            occupancy = new int[Width, Height];
            ClearOccupancy();

            foreach (var w in level.walls) walls[w.x, w.y] = true;

            foreach (var data in level.blocks)
            {
                var block = new BlockState(Blocks.Count, data);
                Blocks.Add(block);
                Stamp(block, block.Id);
            }
            RemainingBlocks = Blocks.Count;
        }

        public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < Width && c.y < Height;

        public bool IsWall(Vector2Int c) => InBounds(c) && walls[c.x, c.y];

        public BlockState BlockAt(Vector2Int c)
        {
            if (!InBounds(c)) return null;
            int id = occupancy[c.x, c.y];
            return id < 0 ? null : Blocks[id];
        }

        public bool CanStep(BlockState block, Vector2Int dir)
        {
            var r = Evaluate(block, dir, out _);
            return r == StepResult.Moved || r == StepResult.Exited;
        }

        // Bloğu dir yönünde tam bir hücre kaydırmayı dener.
        // Board dışına çıkacaksa, sadece aynı renkteki ve bloğun genişliğine yeten kapıdan çıkabilir.
        public StepResult TryStep(BlockState block, Vector2Int dir, out GateData gate)
        {
            var result = Evaluate(block, dir, out gate);
            if (result == StepResult.Moved)
            {
                Stamp(block, -1);
                block.Position += dir;
                Stamp(block, block.Id);
            }
            else if (result == StepResult.Exited)
            {
                Stamp(block, -1);
                block.Removed = true;
                RemainingBlocks--;
            }
            return result;
        }

        // TryStep'in sonucunu board'u değiştirmeden hesaplar.
        // WrongGate: blok bir kapıya dayandı ama rengi ya da genişliği uymuyor (feedback için gate döner).
        public StepResult Evaluate(BlockState block, Vector2Int dir, out GateData gate)
        {
            gate = null;
            if (block.Removed || dir == Vector2Int.zero || !block.Allows(dir)) return StepResult.Blocked;

            var next = block.Position + dir;
            bool leavesBoard = false;

            foreach (var s in block.Shape)
            {
                var c = next + s;
                if (!InBounds(c)) { leavesBoard = true; continue; }
                if (walls[c.x, c.y]) return StepResult.Blocked;
                int other = occupancy[c.x, c.y];
                if (other != -1 && other != block.Id) return StepResult.Blocked;
            }

            if (!leavesBoard) return StepResult.Moved;

            gate = FindGate(block, dir, requireMatch: true);
            if (gate != null) return StepResult.Exited;

            gate = FindGate(block, dir, requireMatch: false);
            return gate != null ? StepResult.WrongGate : StepResult.Blocked;
        }

        // ---- LevelSolver için: tüm blok pozisyonlarını kaydet / geri yükle ----

        public const int RemovedCode = char.MaxValue;

        public int[] SaveState()
        {
            var state = new int[Blocks.Count];
            for (int i = 0; i < Blocks.Count; i++)
                state[i] = Blocks[i].Removed ? RemovedCode : Encode(Blocks[i].Position);
            return state;
        }

        public void LoadState(int[] state)
        {
            ClearOccupancy();
            RemainingBlocks = 0;
            for (int i = 0; i < Blocks.Count; i++)
            {
                var b = Blocks[i];
                b.Removed = state[i] == RemovedCode;
                if (b.Removed) continue;
                b.Position = new Vector2Int(state[i] >> 8, state[i] & 0xFF);
                Stamp(b, b.Id);
                RemainingBlocks++;
            }
        }

        public static int Encode(Vector2Int p) => (p.x << 8) | p.y;

        // requireMatch: true → renk aynı ve blok kapıya tamamen sığmalı.
        //               false → kenarda bloğa değen herhangi bir kapı.
        GateData FindGate(BlockState block, Vector2Int dir, bool requireMatch)
        {
            Side side = dir.x < 0 ? Side.Left : dir.x > 0 ? Side.Right : dir.y < 0 ? Side.Bottom : Side.Top;
            bool vertical = side is Side.Left or Side.Right;
            int min = vertical ? block.Position.y : block.Position.x;
            int max = min + (vertical ? block.Size.y : block.Size.x) - 1;

            foreach (var g in Gates)
            {
                if (g.side != side) continue;
                bool fits = g.color == block.Color && g.start <= min && max <= g.End;
                bool touches = g.start <= max && min <= g.End;
                if (requireMatch ? fits : touches) return g;
            }
            return null;
        }

        void ClearOccupancy()
        {
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    occupancy[x, y] = -1;
        }

        void Stamp(BlockState block, int value)
        {
            foreach (var s in block.Shape)
            {
                var c = block.Position + s;
                occupancy[c.x, c.y] = value;
            }
        }
    }
}
