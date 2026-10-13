using System.Collections.Generic;
using UnityEngine;

namespace ColorBlockJam
{
    public class SolveResult
    {
        public bool Solved;
        public bool LimitReached;   // arama sınırına takıldı: "çözülemez" değil, "bilinmiyor" demek
        public int Moves;           // en kısa çözümde oyuncunun yapacağı sürükleme sayısı (çıkışlar dahil)
        public int Explored;

        public string Summary =>
            Solved ? $"Çözülebilir: en az {Moves} hamle."
            : LimitReached ? $"Bilinmiyor: {Explored:N0} durum denendi, sınır aşıldı."
            : "Çözülemez: hiçbir hamle sırası bütün blokları çıkaramıyor.";
    }

    // Level'ı bilgisayara oynatır ve en az kaç hamlede bittiğini bulur.
    // Bir "hamle" = bir bloğu ulaşabildiği herhangi bir hücreye sürüklemek (oyuncunun yaptığı gibi).
    // Kapısına ulaşabilen blok hemen çıkarılır: blok çıkarmak sadece yer açar, hiçbir zaman zarar vermez.
    public static class LevelSolver
    {
        static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        public static SolveResult Solve(LevelData level, int maxStates = 200_000)
        {
            var board = new BoardModel(level);
            var reach = new List<Vector2Int>();

            var start = board.SaveState();
            int startMoves = RemoveExitable(board, start, reach);
            var depth = new Dictionary<string, int> { [Key(start)] = startMoves };
            var queue = new Queue<int[]>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var state = queue.Dequeue();
                int moves = depth[Key(state)];

                board.LoadState(state);
                if (board.RemainingBlocks == 0) return new SolveResult { Solved = true, Moves = moves, Explored = depth.Count };
                if (depth.Count > maxStates) return new SolveResult { LimitReached = true, Explored = depth.Count };

                foreach (var block in board.Blocks)
                {
                    if (block.Removed) continue;
                    board.LoadState(state);
                    Reachable(board, block, reach, out _);

                    foreach (var pos in reach)
                    {
                        if (pos == block.Position) continue;
                        var next = (int[])state.Clone();
                        next[block.Id] = BoardModel.Encode(pos);
                        int exits = RemoveExitable(board, next, reach: null);

                        var key = Key(next);
                        if (depth.ContainsKey(key)) continue;
                        depth[key] = moves + 1 + exits;
                        queue.Enqueue(next);
                    }
                    board.LoadState(state);
                }
            }
            return new SolveResult { Explored = depth.Count };
        }

        // Kapısına ulaşabilen blokları (zincirleme) state'ten çıkarır; çıkan blok sayısını döner.
        static int RemoveExitable(BoardModel board, int[] state, List<Vector2Int> reach)
        {
            reach ??= new List<Vector2Int>();
            int removed = 0;
            bool changed = true;
            while (changed)
            {
                changed = false;
                board.LoadState(state);
                foreach (var block in board.Blocks)
                {
                    if (block.Removed) continue;
                    Reachable(board, block, reach, out bool canExit);
                    if (!canExit) continue;
                    state[block.Id] = BoardModel.RemovedCode;
                    removed++;
                    changed = true;
                    break;
                }
            }
            return removed;
        }

        // Diğer bloklar yerinde dururken bu bloğun gidebileceği bütün pozisyonlar (flood fill).
        static void Reachable(BoardModel board, BlockState block, List<Vector2Int> result, out bool canExit)
        {
            canExit = false;
            result.Clear();
            var origin = block.Position;
            var seen = new HashSet<Vector2Int> { origin };
            result.Add(origin);

            for (int i = 0; i < result.Count; i++)
            {
                block.Position = result[i];
                foreach (var dir in Dirs)
                {
                    var r = board.Evaluate(block, dir, out _);
                    if (r == StepResult.Exited) canExit = true;
                    else if (r == StepResult.Moved && seen.Add(result[i] + dir)) result.Add(result[i] + dir);
                }
            }
            block.Position = origin;
        }

        static string Key(int[] state)
        {
            var chars = new char[state.Length];
            for (int i = 0; i < state.Length; i++) chars[i] = (char)state[i];
            return new string(chars);
        }
    }
}
