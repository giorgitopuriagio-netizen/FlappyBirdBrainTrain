using System;

namespace FlappyBird.Core
{
    public enum EvaluationMode { WatchBot, HumanVsBot }
    public enum EvaluationState { WaitingForHuman, Running, Paused, Completed, Stopped }

    // Mode, bird count, pilots and actions belong to one round and never change independently.
    public sealed class EvaluationSession
    {
        private readonly NeuralPilot pilot;
        private readonly bool[] actions;
        private bool humanFlap;

        public EvaluationMode Mode { get; private set; }
        public EvaluationState State { get; private set; }
        public FlappyWorld World { get; private set; }
        public bool HasHuman { get { return Mode == EvaluationMode.HumanVsBot; } }
        public bool IsActive { get { return State == EvaluationState.Running || State == EvaluationState.WaitingForHuman || State == EvaluationState.Paused; } }
        public BirdState Bot { get { return World.Birds[0]; } }
        public BirdState Human { get { return HasHuman ? World.Birds[1] : null; } }

        public EvaluationSession(BrainGenome brain, uint courseSeed, EvaluationMode mode)
        {
            if (brain == null) throw new ArgumentNullException(nameof(brain));
            if (mode != EvaluationMode.WatchBot && mode != EvaluationMode.HumanVsBot)
                throw new ArgumentOutOfRangeException(nameof(mode));
            pilot = new NeuralPilot(brain);
            Mode = mode;
            World = new FlappyWorld(HasHuman ? 2 : 1, courseSeed);
            actions = new bool[World.Birds.Length];
            State = HasHuman ? EvaluationState.WaitingForHuman : EvaluationState.Running;
        }

        public void RequestFlap()
        {
            if (!IsActive || !HasHuman) return;
            // A paused round can resume even if the human has already crashed.
            if (State == EvaluationState.WaitingForHuman || State == EvaluationState.Paused)
                State = EvaluationState.Running;
            humanFlap |= Human.alive;
        }

        public void Tick()
        {
            if (State != EvaluationState.Running) return;
            actions[0] = Bot.alive && pilot.Decide(World, 0);
            if (HasHuman) actions[1] = humanFlap;
            humanFlap = false;
            World.Tick(actions);
            if (World.AliveCount == 0) State = EvaluationState.Completed;
        }

        public void Pause()
        {
            if (State != EvaluationState.Running) return;
            State = EvaluationState.Paused;
            humanFlap = false;
        }

        public void Resume() { if (State == EvaluationState.Paused) State = EvaluationState.Running; }

        public void Stop()
        {
            if (!IsActive) return;
            State = EvaluationState.Stopped;
            humanFlap = false;
        }
    }
}
