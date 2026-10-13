using System;
using UnityEngine;

namespace PuzzleKit
{
    public enum SessionState { Idle, Playing, Won, Lost }

    // Oyundan bağımsız level akışı: başla → süre say → kazan / kaybet.
    // Sonraki puzzle oyunlarında da aynen kullanılabilir (GDD Bölüm 4).
    public class LevelSession
    {
        public SessionState State { get; private set; } = SessionState.Idle;
        public float TimeLimit { get; private set; }
        public float TimeLeft { get; private set; }
        public bool IsPlaying => State == SessionState.Playing;

        public event Action Won;
        public event Action Lost;

        public void Begin(float timeLimit)
        {
            TimeLimit = TimeLeft = timeLimit;
            State = SessionState.Playing;
        }

        public void Tick(float deltaTime)
        {
            if (!IsPlaying) return;
            TimeLeft = Mathf.Max(0f, TimeLeft - deltaTime);
            if (TimeLeft > 0f) return;
            State = SessionState.Lost;
            Lost?.Invoke();
        }

        public void Complete()
        {
            if (!IsPlaying) return;
            State = SessionState.Won;
            Won?.Invoke();
        }
    }
}
