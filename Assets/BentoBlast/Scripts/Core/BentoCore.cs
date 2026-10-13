using System;
using System.Collections.Generic;
using ColorBlockJam;
using UnityEngine;

namespace BentoBlast
{
    // Tek parça: hücre listesi + meyve rengi. Hücreler sol-alt köşe (0,0) olacak şekilde normalize edilir.
    public class Piece
    {
        public readonly Vector2Int[] Cells;
        public readonly BlockColor Color;
        public readonly Vector2Int Size;

        public Piece(Vector2Int[] cells, BlockColor color)
        {
            var min = new Vector2Int(int.MaxValue, int.MaxValue);
            var max = new Vector2Int(int.MinValue, int.MinValue);
            foreach (var c in cells) { min = Vector2Int.Min(min, c); max = Vector2Int.Max(max, c); }
            Cells = new Vector2Int[cells.Length];
            for (int i = 0; i < cells.Length; i++) Cells[i] = cells[i] - min;
            Size = max - min + Vector2Int.one;
            Color = color;
        }
    }

    // Block Blast'teki parça ailesi: çubuklar, kareler, L'ler, T, S/Z. Döndürme yok; her yön ayrı parça.
    public static class PieceLibrary
    {
        public static readonly (string name, int weight, Vector2Int[] cells)[] Shapes =
        {
            ("1", 6, P("X")),
            ("I2h", 8, P("XX")), ("I2v", 8, P("X", "X")),
            ("I3h", 8, P("XXX")), ("I3v", 8, P("X", "X", "X")),
            ("I4h", 5, P("XXXX")), ("I4v", 5, P("X", "X", "X", "X")),
            ("I5h", 3, P("XXXXX")), ("I5v", 3, P("X", "X", "X", "X", "X")),
            ("O2", 8, P("XX", "XX")),
            ("O3", 2, P("XXX", "XXX", "XXX")),
            ("R23", 4, P("XX", "XX", "XX")), ("R32", 4, P("XXX", "XXX")),
            ("V3a", 6, P("X.", "XX")), ("V3b", 6, P(".X", "XX")), ("V3c", 6, P("XX", "X.")), ("V3d", 6, P("XX", ".X")),
            ("L4a", 4, P("X.", "X.", "XX")), ("L4b", 4, P(".X", ".X", "XX")), ("L4c", 4, P("XX", "X.", "X.")), ("L4d", 4, P("XX", ".X", ".X")),
            ("L4e", 4, P("XXX", "X..")), ("L4f", 4, P("XXX", "..X")), ("L4g", 4, P("X..", "XXX")), ("L4h", 4, P("..X", "XXX")),
            ("T4a", 4, P("XXX", ".X.")), ("T4b", 4, P(".X.", "XXX")), ("T4c", 4, P("X.", "XX", "X.")), ("T4d", 4, P(".X", "XX", ".X")),
            ("S4a", 3, P(".XX", "XX.")), ("S4b", 3, P("XX.", ".XX")), ("S4c", 3, P("X.", "XX", ".X")), ("S4d", 3, P(".X", "XX", "X.")),
            ("V5a", 2, P("X..", "X..", "XXX")), ("V5b", 2, P("..X", "..X", "XXX")), ("V5c", 2, P("XXX", "X..", "X..")), ("V5d", 2, P("XXX", "..X", "..X")),
        };

        // ASCII'den hücre listesi; ilk satır en üst.
        static Vector2Int[] P(params string[] rows)
        {
            var cells = new List<Vector2Int>();
            for (int r = 0; r < rows.Length; r++)
                for (int x = 0; x < rows[r].Length; x++)
                    if (rows[r][x] == 'X') cells.Add(new Vector2Int(x, rows.Length - 1 - r));
            return cells.ToArray();
        }
    }

    public class PlaceResult
    {
        public readonly List<int> Rows = new();
        public readonly List<int> Columns = new();
        public readonly List<(Vector2Int cell, BlockColor color)> Cleared = new();
        public int Points;
        public int Combo;   // bu yerleştirmeden sonraki kombo seviyesi (0 = kombo yok)
        public int Lines => Rows.Count + Columns.Count;
    }

    // 8×8 board. Hücre -1 = boş, aksi halde içindeki meyvenin rengi.
    public class BentoBoard
    {
        public const int Size = 8;
        readonly int[,] cells = new int[Size, Size];

        public BentoBoard() => Clear();

        public void Clear()
        {
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    cells[x, y] = -1;
        }

        public bool IsFilled(Vector2Int c) => cells[c.x, c.y] >= 0;
        public BlockColor ColorAt(Vector2Int c) => (BlockColor)cells[c.x, c.y];

        public int FilledCount
        {
            get
            {
                int n = 0;
                foreach (var v in cells) if (v >= 0) n++;
                return n;
            }
        }

        public bool CanPlace(Piece piece, Vector2Int origin)
        {
            foreach (var s in piece.Cells)
            {
                var c = origin + s;
                if (c.x < 0 || c.y < 0 || c.x >= Size || c.y >= Size || cells[c.x, c.y] >= 0) return false;
            }
            return true;
        }

        public bool CanPlaceAnywhere(Piece piece)
        {
            for (int x = 0; x <= Size - piece.Size.x; x++)
                for (int y = 0; y <= Size - piece.Size.y; y++)
                    if (CanPlace(piece, new Vector2Int(x, y))) return true;
            return false;
        }

        // Parçayı koyar, dolan satır/sütunları temizler. Puanı ScoreKeeper hesaplar.
        public PlaceResult Place(Piece piece, Vector2Int origin)
        {
            if (!CanPlace(piece, origin)) throw new InvalidOperationException("Parça buraya sığmıyor");
            foreach (var s in piece.Cells)
            {
                var c = origin + s;
                cells[c.x, c.y] = (int)piece.Color;
            }

            var result = new PlaceResult();
            for (int y = 0; y < Size; y++) if (LineFull(y, horizontal: true)) result.Rows.Add(y);
            for (int x = 0; x < Size; x++) if (LineFull(x, horizontal: false)) result.Columns.Add(x);

            // Satır ve sütunun kesiştiği hücre iki kere sayılmasın diye önce topla, sonra sil.
            var toClear = new HashSet<Vector2Int>();
            foreach (var y in result.Rows) for (int x = 0; x < Size; x++) toClear.Add(new Vector2Int(x, y));
            foreach (var x in result.Columns) for (int y = 0; y < Size; y++) toClear.Add(new Vector2Int(x, y));
            foreach (var c in toClear)
            {
                result.Cleared.Add((c, (BlockColor)cells[c.x, c.y]));
                cells[c.x, c.y] = -1;
            }
            return result;
        }

        bool LineFull(int index, bool horizontal)
        {
            for (int i = 0; i < Size; i++)
                if ((horizontal ? cells[i, index] : cells[index, i]) < 0) return false;
            return true;
        }
    }

    // Puan: her konulan hücre 1 puan. Satır/sütun patlatınca 80 × satır² × kombo.
    // Kombo, art arda patlatmalarla artar; 3 yerleştirme boyunca hiç patlatma olmazsa sıfırlanır.
    public class ScoreKeeper
    {
        public const int ComboGrace = 3;

        public int Score { get; private set; }
        public int Combo { get; private set; }
        int movesSinceClear;

        public void Apply(PlaceResult r, int placedCells)
        {
            int points = placedCells;
            if (r.Lines > 0)
            {
                Combo++;
                movesSinceClear = 0;
                points += 80 * r.Lines * r.Lines * Combo;
            }
            else if (++movesSinceClear >= ComboGrace)
            {
                Combo = 0;
            }
            r.Points = points;
            r.Combo = Combo;
            Score += points;
        }

        public void AddBonus(int points) => Score += points;
    }

    // Sipariş: "6 çilek + 4 kivi". Patlatılan hücrelerdeki meyveler siparişe sayılır.
    public class Order
    {
        public readonly BlockColor[] Fruits;
        public readonly int[] Need;
        public readonly int[] Have;
        public readonly int Bonus;

        public Order(BlockColor[] fruits, int[] need, int bonus)
        {
            Fruits = fruits;
            Need = need;
            Have = new int[fruits.Length];
            Bonus = bonus;
        }

        public bool Done
        {
            get
            {
                for (int i = 0; i < Fruits.Length; i++) if (Have[i] < Need[i]) return false;
                return true;
            }
        }

        // Patlayan meyveleri sayar; siparişe katkısı olduysa true döner.
        public bool Count(IEnumerable<(Vector2Int cell, BlockColor color)> cleared)
        {
            bool changed = false;
            foreach (var (_, color) in cleared)
                for (int i = 0; i < Fruits.Length; i++)
                    if (Fruits[i] == color && Have[i] < Need[i]) { Have[i]++; changed = true; }
            return changed;
        }
    }

    public class OrderBook
    {
        readonly System.Random rng;
        readonly BlockColor[] palette;
        public Order Current { get; private set; }
        public int Completed { get; private set; }

        public OrderBook(System.Random rng, BlockColor[] palette)
        {
            this.rng = rng;
            this.palette = palette;
            Current = Make();
        }

        // Sipariş tamamlandıysa bonusu döner ve yenisini açar; değilse 0.
        public int CompleteIfDone()
        {
            if (!Current.Done) return 0;
            int bonus = Current.Bonus;
            Completed++;
            Current = Make();
            return bonus;
        }

        Order Make()
        {
            int a = rng.Next(palette.Length);
            int b = (a + 1 + rng.Next(palette.Length - 1)) % palette.Length;
            int need = Math.Min(4 + Completed * 2, 12);
            return new Order(new[] { palette[a], palette[b] }, new[] { need, Math.Max(3, need - 2) }, 150 + Completed * 50);
        }
    }

    // Her turda 3 parça seçer. Kural: en az bir parça mutlaka sığsın (sığacak hiçbir şekil yoksa oyun biter).
    // Board doldukça küçük parçaların gelme ihtimali artar ki oyun haksız hissettirmesin.
    public class PieceGenerator
    {
        readonly System.Random rng;
        readonly BlockColor[] palette;

        public PieceGenerator(System.Random rng, BlockColor[] palette)
        {
            this.rng = rng;
            this.palette = palette;
        }

        public Piece[] NextTray(BentoBoard board)
        {
            float fill = board.FilledCount / (float)(BentoBoard.Size * BentoBoard.Size);
            var tray = new Piece[3];
            for (int i = 0; i < 3; i++) tray[i] = Random(fill);

            bool anyFits = false;
            foreach (var p in tray) anyFits |= board.CanPlaceAnywhere(p);
            if (!anyFits)
            {
                var fitting = new List<Piece>();
                foreach (var s in PieceLibrary.Shapes)
                {
                    var p = new Piece(s.cells, RandomColor());
                    if (board.CanPlaceAnywhere(p)) fitting.Add(p);
                }
                if (fitting.Count > 0) tray[rng.Next(3)] = fitting[rng.Next(fitting.Count)];
            }
            return tray;
        }

        Piece Random(float fill)
        {
            int total = 0;
            foreach (var s in PieceLibrary.Shapes) total += Weight(s.weight, s.cells.Length, fill);
            int roll = rng.Next(total);
            foreach (var s in PieceLibrary.Shapes)
            {
                roll -= Weight(s.weight, s.cells.Length, fill);
                if (roll < 0) return new Piece(s.cells, RandomColor());
            }
            return new Piece(PieceLibrary.Shapes[0].cells, RandomColor());
        }

        // Board %50'den fazla doluysa büyük parçaların ağırlığı azalır.
        static int Weight(int baseWeight, int cellCount, float fill)
        {
            if (fill < 0.5f || cellCount <= 3) return baseWeight * 2;
            return Math.Max(1, baseWeight * (cellCount >= 6 ? 1 : 2) / 2);
        }

        BlockColor RandomColor() => palette[rng.Next(palette.Length)];
    }
}
