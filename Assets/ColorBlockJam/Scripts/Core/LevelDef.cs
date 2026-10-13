using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorBlockJam
{
    [Serializable]
    public class BlockDef
    {
        public string id = "a";
        public BlockColor color;
        public MoveAxis axis;
    }

    public class LevelFormatException : Exception
    {
        public LevelFormatException(string message) : base(message) { }
    }

    // Designer'ın düzenlediği ham level tanımı (LevelAsset içinde saklanır).
    // Izgara metin olarak tutulur:
    //   '.' boş, '#' duvar, harf = blok (aynı harf = aynı blok)
    // İlk satır board'un EN ÜST satırıdır.
    [Serializable]
    public class LevelDef
    {
        public const char Empty = '.';
        public const char Wall = '#';

        public float timeLimit = 60f;
        public int coinReward = 10;
        public string layout = "";
        public List<BlockDef> blocks = new();
        public List<GateData> gates = new();

        // ---- koddan level yazmak için kısayollar (DefaultLevels kullanır) ----

        public static LevelDef Create(params string[] rows) => new() { layout = string.Join("\n", rows) };

        public LevelDef Time(float seconds) { timeLimit = seconds; return this; }

        public LevelDef Coins(int amount) { coinReward = amount; return this; }

        public LevelDef Block(char id, BlockColor color, MoveAxis axis = MoveAxis.Both)
        {
            blocks.Add(new BlockDef { id = id.ToString(), color = color, axis = axis });
            return this;
        }

        public LevelDef Gate(Side side, int start, int length, BlockColor color)
        {
            gates.Add(new GateData { side = side, start = start, length = length, color = color });
            return this;
        }

        // ---- okuma ----

        public string[] Rows()
        {
            var rows = new List<string>();
            foreach (var line in (layout ?? "").Split('\n'))
            {
                var row = line.Trim();
                if (row.Length > 0) rows.Add(row);
            }
            return rows.ToArray();
        }

        public BlockDef FindBlock(char id)
        {
            foreach (var b in blocks)
                if (!string.IsNullOrEmpty(b.id) && b.id[0] == id) return b;
            return null;
        }

        // Level'daki tüm hataları insan diliyle listeler; boş liste = level geçerli.
        public List<string> Validate()
        {
            var problems = new List<string>();
            var rows = Rows();
            if (rows.Length == 0) { problems.Add("Izgara boş."); return problems; }

            int width = rows[0].Length, height = rows.Length;
            var cellsById = new Dictionary<char, List<Vector2Int>>();

            for (int r = 0; r < height; r++)
            {
                if (rows[r].Length != width)
                {
                    problems.Add($"{r + 1}. satırın uzunluğu {rows[r].Length}, diğerleri {width}.");
                    continue;
                }
                for (int x = 0; x < width; x++)
                {
                    char ch = rows[r][x];
                    if (ch == Empty || ch == Wall) continue;
                    if (!cellsById.TryGetValue(ch, out var list)) cellsById[ch] = list = new List<Vector2Int>();
                    list.Add(new Vector2Int(x, height - 1 - r));
                }
            }
            if (problems.Count > 0) return problems;

            if (cellsById.Count == 0) problems.Add("Board'da hiç blok yok.");

            foreach (var (id, cells) in cellsById)
            {
                var def = FindBlock(id);
                if (def == null) { problems.Add($"'{id}' bloğunun rengi tanımlanmamış."); continue; }
                if (!IsConnected(cells)) problems.Add($"'{id}' bloğunun hücreleri birbirine bitişik değil.");

                bool hasGate = false;
                foreach (var g in gates) hasGate |= g.color == def.color;
                if (!hasGate) problems.Add($"'{id}' bloğu {def.color}, ama {def.color} renkte kapı yok.");
            }

            for (int i = 0; i < gates.Count; i++)
            {
                var g = gates[i];
                int edge = g.side is Side.Left or Side.Right ? height : width;
                if (g.length < 1) problems.Add($"{i + 1}. kapının uzunluğu en az 1 olmalı.");
                else if (g.start < 0 || g.End >= edge) problems.Add($"{i + 1}. kapı ({g.side}, {g.color}) board kenarından taşıyor.");

                for (int j = 0; j < i; j++)
                {
                    var o = gates[j];
                    if (o.side == g.side && g.start <= o.End && o.start <= g.End)
                        problems.Add($"{j + 1}. ve {i + 1}. kapı aynı kenarda üst üste biniyor.");
                }
            }
            return problems;
        }

        public LevelData Build()
        {
            var problems = Validate();
            if (problems.Count > 0) throw new LevelFormatException(string.Join("\n", problems));

            var rows = Rows();
            var level = new LevelData { width = rows[0].Length, height = rows.Length, timeLimit = timeLimit };
            var cellsById = new Dictionary<char, List<Vector2Int>>();

            for (int r = 0; r < level.height; r++)
            {
                int y = level.height - 1 - r;
                for (int x = 0; x < level.width; x++)
                {
                    char ch = rows[r][x];
                    if (ch == Empty) continue;
                    if (ch == Wall) { level.walls.Add(new Vector2Int(x, y)); continue; }
                    if (!cellsById.TryGetValue(ch, out var list)) cellsById[ch] = list = new List<Vector2Int>();
                    list.Add(new Vector2Int(x, y));
                }
            }

            foreach (var (id, cells) in cellsById)
            {
                var def = FindBlock(id);
                var min = new Vector2Int(int.MaxValue, int.MaxValue);
                foreach (var c in cells) min = Vector2Int.Min(min, c);

                var shape = new Vector2Int[cells.Count];
                for (int i = 0; i < cells.Count; i++) shape[i] = cells[i] - min;

                level.blocks.Add(new BlockData { position = min, shape = shape, color = def.color, axis = def.axis });
            }

            foreach (var g in gates)
                level.gates.Add(new GateData { side = g.side, start = g.start, length = g.length, color = g.color });
            return level;
        }

        static bool IsConnected(List<Vector2Int> cells)
        {
            var set = new HashSet<Vector2Int>(cells);
            var seen = new HashSet<Vector2Int> { cells[0] };
            var stack = new Stack<Vector2Int>();
            stack.Push(cells[0]);
            var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                foreach (var d in dirs)
                {
                    var n = c + d;
                    if (set.Contains(n) && seen.Add(n)) stack.Push(n);
                }
            }
            return seen.Count == cells.Count;
        }
    }
}
