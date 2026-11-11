using System;
using System.Collections.Generic;

namespace FlappyBird.Core
{
    public static class GameRules
    {
        public const int Version = 1;
        public const double Step = 1.0 / 60;
        public const double Width = 288;
        public const double Height = 512;
        public const double Ground = 112;
        public const double BirdX = 72;
        public const double StartY = 300;
        public const double BirdRadius = 10;
        public const double Gravity = 900;
        public const double FlapVelocity = 270;
        public const double TerminalVelocity = -480;
        public const double PipeSpeed = 120;
        public const double PipeWidth = 52;
        public const double PipeGap = 100;
        public const double PipeSpacing = 168;
        public const double FirstPipeX = 340;
        public const double MinGapCenter = 202;
        public const double MaxGapCenter = 402;
    }

    [Serializable]
    public sealed class BirdState
    {
        public double y = GameRules.StartY;
        public double velocity;
        public bool alive = true;
        public int score;
        public int lastScoredPipe = -1;
        public int survivalTicks;
        public double deathGapDistance;

        public BirdState Clone() { return (BirdState)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class PipeState
    {
        public int id;
        public double x;
        public double gapCenter;
        public PipeState Clone() { return (PipeState)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class WorldSnapshot
    {
        public int ticks;
        public int nextPipeId;
        public uint randomState;
        public BirdState[] birds;
        public PipeState[] pipes;
    }

    // One deterministic fixed-step simulation is used for evolution, playback and human play.
    public sealed class FlappyWorld
    {
        private readonly DeterministicRandom random;
        private readonly List<PipeState> pipes = new List<PipeState>();
        private int nextPipeId;

        public BirdState[] Birds { get; private set; }
        public IReadOnlyList<PipeState> Pipes { get { return pipes; } }
        public int Ticks { get; private set; }
        public int AliveCount { get; private set; }

        public FlappyWorld(int birdCount, uint courseSeed)
        {
            if (birdCount < 1 || birdCount > 1000) throw new ArgumentOutOfRangeException(nameof(birdCount));
            random = new DeterministicRandom(courseSeed);
            Birds = new BirdState[birdCount];
            for (int i = 0; i < birdCount; i++) Birds[i] = new BirdState();
            AliveCount = birdCount;
            SpawnPipe(GameRules.FirstPipeX);
            SpawnPipe(GameRules.FirstPipeX + GameRules.PipeSpacing);
        }

        public FlappyWorld(WorldSnapshot snapshot)
        {
            if (snapshot == null || snapshot.ticks < 0 || snapshot.randomState == 0 || snapshot.birds == null ||
                snapshot.birds.Length < 1 || snapshot.birds.Length > 1000 || snapshot.pipes == null ||
                snapshot.pipes.Length < 1 || snapshot.pipes.Length > 10 || snapshot.nextPipeId < 1)
                throw new ArgumentException("Invalid world checkpoint.");
            random = new DeterministicRandom(snapshot.randomState);
            Ticks = snapshot.ticks;
            nextPipeId = snapshot.nextPipeId;
            Birds = new BirdState[snapshot.birds.Length];
            for (int i = 0; i < Birds.Length; i++)
            {
                var bird = snapshot.birds[i];
                if (bird == null || !Finite(bird.y) || !Finite(bird.velocity) || !Finite(bird.deathGapDistance) ||
                    bird.score < 0 || bird.survivalTicks < 0 || bird.survivalTicks > Ticks ||
                    (bird.alive && (bird.y <= GameRules.Ground || bird.y >= GameRules.Height)))
                    throw new ArgumentException("Invalid bird checkpoint.");
                Birds[i] = bird.Clone();
                if (bird.alive) AliveCount++;
            }
            int previousId = -1;
            double previousX = double.NegativeInfinity;
            foreach (var pipe in snapshot.pipes)
            {
                if (pipe == null || !Finite(pipe.x) || !Finite(pipe.gapCenter) || pipe.id <= previousId ||
                    pipe.id >= nextPipeId || pipe.x <= previousX || pipe.gapCenter < GameRules.MinGapCenter ||
                    pipe.gapCenter > GameRules.MaxGapCenter) throw new ArgumentException("Invalid pipe checkpoint.");
                pipes.Add(pipe.Clone());
                previousId = pipe.id;
                previousX = pipe.x;
            }
        }

        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }

        private void SpawnPipe(double x)
        {
            pipes.Add(new PipeState { id = nextPipeId++, x = x,
                gapCenter = GameRules.MinGapCenter + random.NextDouble() * (GameRules.MaxGapCenter - GameRules.MinGapCenter) });
        }

        public PipeState NextPipe()
        {
            foreach (var pipe in pipes)
                if (pipe.x + GameRules.PipeWidth >= GameRules.BirdX - GameRules.BirdRadius) return pipe;
            return pipes[pipes.Count - 1];
        }

        public void ReadSensors(int birdIndex, double[] inputs)
        {
            if (inputs == null || inputs.Length != 6) throw new ArgumentException("Six sensors are required.");
            var bird = Birds[birdIndex];
            var pipe = NextPipe();
            const double playable = GameRules.Height - GameRules.Ground;
            inputs[0] = (bird.y - GameRules.Ground) / playable * 2 - 1;
            inputs[1] = bird.velocity / Math.Abs(GameRules.TerminalVelocity);
            inputs[2] = (pipe.x - GameRules.BirdX) / GameRules.Width;
            inputs[3] = (pipe.gapCenter - bird.y) / playable;
            inputs[4] = (pipe.gapCenter + GameRules.PipeGap / 2 - bird.y) / playable;
            inputs[5] = (pipe.gapCenter - GameRules.PipeGap / 2 - bird.y) / playable;
        }

        public void Tick(bool[] flaps)
        {
            if (flaps == null || flaps.Length != Birds.Length) throw new ArgumentException("One action per bird is required.");
            if (AliveCount == 0) return;
            Ticks++;
            foreach (var pipe in pipes) pipe.x -= GameRules.PipeSpeed * GameRules.Step;
            while (pipes[0].x + GameRules.PipeWidth < -16) pipes.RemoveAt(0);
            if (pipes[pipes.Count - 1].x < GameRules.Width + GameRules.PipeSpacing)
                SpawnPipe(pipes[pipes.Count - 1].x + GameRules.PipeSpacing);

            for (int i = 0; i < Birds.Length; i++)
            {
                var bird = Birds[i];
                if (!bird.alive) continue;
                if (flaps[i]) bird.velocity = GameRules.FlapVelocity;
                bird.velocity = Math.Max(GameRules.TerminalVelocity, bird.velocity - GameRules.Gravity * GameRules.Step);
                bird.y += bird.velocity * GameRules.Step;
                bird.survivalTicks++;

                bool collided = bird.y - GameRules.BirdRadius <= GameRules.Ground ||
                                bird.y + GameRules.BirdRadius >= GameRules.Height;
                foreach (var pipe in pipes)
                {
                    double bottom = pipe.gapCenter - GameRules.PipeGap / 2;
                    double top = pipe.gapCenter + GameRules.PipeGap / 2;
                    collided |= TouchesRectangle(bird.y, pipe.x, GameRules.Ground, pipe.x + GameRules.PipeWidth, bottom) ||
                                TouchesRectangle(bird.y, pipe.x, top, pipe.x + GameRules.PipeWidth, GameRules.Height);
                }
                if (collided)
                {
                    bird.alive = false;
                    bird.deathGapDistance = Math.Abs(bird.y - NextPipe().gapCenter);
                    AliveCount--;
                    continue;
                }
                foreach (var pipe in pipes)
                {
                    if (pipe.id > bird.lastScoredPipe && pipe.x + GameRules.PipeWidth < GameRules.BirdX - GameRules.BirdRadius)
                    {
                        bird.score++;
                        bird.lastScoredPipe = pipe.id;
                    }
                }
            }
        }

        private static bool TouchesRectangle(double birdY, double left, double bottom, double right, double top)
        {
            double dx = GameRules.BirdX - Math.Max(left, Math.Min(right, GameRules.BirdX));
            double dy = birdY - Math.Max(bottom, Math.Min(top, birdY));
            return dx * dx + dy * dy <= GameRules.BirdRadius * GameRules.BirdRadius;
        }

        public WorldSnapshot Capture()
        {
            var snapshot = new WorldSnapshot { ticks = Ticks, nextPipeId = nextPipeId,
                randomState = random.State, birds = new BirdState[Birds.Length], pipes = new PipeState[pipes.Count] };
            for (int i = 0; i < Birds.Length; i++) snapshot.birds[i] = Birds[i].Clone();
            for (int i = 0; i < pipes.Count; i++) snapshot.pipes[i] = pipes[i].Clone();
            return snapshot;
        }
    }
}
