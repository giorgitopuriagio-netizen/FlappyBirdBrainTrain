using System;
using System.Linq;
using System.Text.Json;
using FlappyBird.Core;

internal static class Program
{
    private static int passed;
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { IncludeFields = true };

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static string Json<T>(T value) { return JsonSerializer.Serialize(value, JsonOptions); }
    private static T RoundTrip<T>(T value) { return JsonSerializer.Deserialize<T>(Json(value), JsonOptions); }

    private static void Run(string name, Action test)
    {
        test();
        Console.WriteLine("PASS " + name);
        passed++;
    }

    private static EvolutionSettings SmallSettings()
    {
        return new EvolutionSettings { populationSize = 24, eliteCount = 2, tournamentSize = 4 };
    }

    private static void Main(string[] arguments)
    {
        Run("Neural bias and stateless feed-forward", () =>
        {
            var brain = BrainGenome.Create(new DeterministicRandom(10));
            Array.Clear(brain.genes, 0, brain.genes.Length);
            brain.genes[brain.genes.Length - 1] = Math.Log(3);
            var network = new NeuralNetwork(brain);
            Check(Math.Abs(network.Evaluate(new double[6]) - 0.75) < 1e-12, "Output bias is missing.");
            Check(network.Evaluate(new double[6]) == network.Evaluate(new double[6]), "Signals accumulated between calls.");
            brain.genes[brain.genes.Length - 1] = -20;
            Check(network.Evaluate(new double[6]) > 0.7, "Network shares mutable source genes.");
        });
        Run("Invalid genomes rejected", () =>
        {
            var brain = BrainGenome.Create(new DeterministicRandom(11));
            brain.genes[0] = double.NaN;
            bool threw = false;
            try { brain.Validate(); } catch (ArgumentException) { threw = true; }
            Check(threw, "NaN brain accepted.");
        });
        Run("Equal physics for human and bot actions", () =>
        {
            var world = new FlappyWorld(2, 100);
            for (int i = 0; i < 2000 && world.AliveCount > 0; i++)
            {
                bool flap = world.Birds[0].y < world.NextPipe().gapCenter - 8 && world.Birds[0].velocity < 0;
                world.Tick(new[] { flap, flap });
                Check(Json(world.Birds[0]) == Json(world.Birds[1]), "Two controllers get different physics.");
            }
        });
        Run("Flap resets velocity and gravity is fixed-step", () =>
        {
            var world = new FlappyWorld(1, 2);
            world.Tick(new[] { true });
            double velocity = GameRules.FlapVelocity - GameRules.Gravity * GameRules.Step;
            Check(world.Birds[0].velocity == velocity, "Wrong flap/gravity integration.");
            world.Tick(new[] { true });
            Check(world.Birds[0].velocity == velocity, "Flaps accumulate force.");
        });
        Run("Ground, ceiling and pipes kill only once", () =>
        {
            var world = new FlappyWorld(2, 3);
            while (world.AliveCount > 0) world.Tick(new[] { false, true });
            int ticks = world.Ticks;
            world.Tick(new[] { true, true });
            Check(world.AliveCount == 0 && world.Ticks == ticks, "A dead world keeps running.");
            var state = new FlappyWorld(1, 3).Capture();
            state.pipes[0].x = 65;
            state.pipes[1].x = 65 + GameRules.PipeSpacing;
            state.birds[0].y = GameRules.Ground + 20;
            world = new FlappyWorld(state);
            world.Tick(new[] { false });
            Check(!world.Birds[0].alive, "Pipe collision missed.");
        });
        Run("Independent score and immutable shared course", () =>
        {
            var snapshot = new FlappyWorld(2, 4).Capture();
            snapshot.pipes[0].x = 22;
            snapshot.pipes[0].gapCenter = 300;
            snapshot.pipes[1].x = 190;
            var world = new FlappyWorld(snapshot);
            string pipesBefore = Json(world.Capture().pipes);
            world.ReadSensors(0, new double[6]);
            world.ReadSensors(1, new double[6]);
            Check(Json(world.Capture().pipes) == pipesBefore, "Sensors removed shared obstacles.");
            for (int i = 0; i < 12; i++) world.Tick(new[] { false, false });
            Check(world.Birds[0].score == 1 && world.Birds[1].score == 1, "Pipe counted more than once or globally.");
        });
        Run("Velocity is observable", () =>
        {
            var snapshot = new FlappyWorld(2, 5).Capture();
            snapshot.birds[0].velocity = 100;
            snapshot.birds[1].velocity = -100;
            var world = new FlappyWorld(snapshot);
            var first = new double[6];
            var second = new double[6];
            world.ReadSensors(0, first);
            world.ReadSensors(1, second);
            Check(first[1] > 0 && second[1] < 0, "Upward and downward states look identical.");
        });
        Run("Selection, crossover, mutation and elitism are independent", () =>
        {
            var random = new DeterministicRandom(6);
            var settings = SmallSettings();
            var population = Enumerable.Range(0, settings.populationSize).Select(_ => BrainGenome.Create(random)).ToArray();
            var fitness = Enumerable.Range(0, settings.populationSize).Select(i => new BirdState { alive = false, survivalTicks = i + 1 }).ToArray();
            string parentData = Json(population);
            var children = new GeneticAlgorithm(settings, random).Evolve(population, fitness);
            Check(Json(children[0]) == Json(population[population.Length - 1]), "Best survivor was not retained.");
            Check(Json(population) == parentData, "Evolution mutated parents.");
            foreach (var child in children) child.Validate();
            children[0].genes[0] += 1;
            Check(Json(population) == parentData, "Elite shares parent arrays.");
            Check(!ReferenceEquals(children[0].genes, children[1].genes), "Children share arrays.");
        });
        Run("Two-member populations and tied fitness terminate", () =>
        {
            var random = new DeterministicRandom(7);
            var settings = new EvolutionSettings { populationSize = 2, eliteCount = 1, tournamentSize = 2 };
            var population = new[] { BrainGenome.Create(random), BrainGenome.Create(random) };
            var birds = new[] { new BirdState(), new BirdState() };
            for (int i = 0; i < 1000; i++) population = new GeneticAlgorithm(settings, random).Evolve(population, birds);
            Check(population.Length == 2, "Population size changed.");
        });
        Run("Checkpoint resumes world and future generations exactly", () =>
        {
            var original = new TrainingSession("resume-test", SmallSettings(), 12345);
            for (int i = 0; i < 137; i++) original.Tick();
            var resumed = new TrainingSession(RoundTrip(original.Capture()));
            int completed = 0;
            for (int i = 0; i < 8000; i++)
            {
                var first = original.Tick();
                var second = resumed.Tick();
                Check(Json(first) == Json(second), "Resumed champion differs.");
                if (first != null) completed++;
                if (i % 499 == 0) Check(Json(original.Capture()) == Json(resumed.Capture()), "Resumed population/world differs.");
            }
            Check(completed > 2, "Resume test did not cross enough generation boundaries.");
        });
        Run("Champion really is the last survivor and can replay", () =>
        {
            var session = new TrainingSession("champion-test", SmallSettings(), 987);
            for (int tick = 0; tick < 10000; tick++)
            {
                var before = session.Capture();
                var champion = session.Tick();
                if (champion == null) continue;
                var replay = new FlappyWorld(1, champion.courseSeed);
                var pilot = new NeuralPilot(champion.brain);
                while (replay.AliveCount > 0 && replay.Ticks <= champion.survivalTicks + 1)
                    replay.Tick(new[] { pilot.Decide(replay, 0) });
                Check(replay.Birds[0].survivalTicks == champion.survivalTicks, "Archived brain does not reproduce survival.");
                Check(replay.Birds[0].score == champion.score, "Archived brain does not reproduce score.");
                Check(champion.survivalTicks >= before.world.birds.Max(bird => bird.survivalTicks), "Champion died before another bird.");
                Check(session.Generation == 2, "Generation was not advanced exactly once.");
                return;
            }
            throw new Exception("No champion produced.");
        });
        Run("Evaluation modes own matching birds and actions", () =>
        {
            var genome = BrainGenome.Create(new DeterministicRandom(91));
            for (int i = 0; i < 40; i++)
            {
                var mode = i % 2 == 0 ? EvaluationMode.WatchBot : EvaluationMode.HumanVsBot;
                var round = new EvaluationSession(genome, 92, mode);
                Check(round.World.Birds.Length == (round.HasHuman ? 2 : 1), "Mode and population disagree.");
                Check((round.Human != null) == round.HasHuman, "Human presence does not match the mode.");
                round.RequestFlap();
                round.Tick();
                Check(round.World.Ticks == 1, "Fresh round did not advance exactly once.");
                round.Stop();
                round.RequestFlap();
                round.Tick();
                Check(round.World.Ticks == 1 && round.State == EvaluationState.Stopped, "Stopped round accepted input.");
            }
        });
        Run("Competition waits for the first flap and consumes it once", () =>
        {
            var round = new EvaluationSession(BrainGenome.Create(new DeterministicRandom(93)), 94, EvaluationMode.HumanVsBot);
            for (int i = 0; i < 100; i++) round.Tick();
            Check(round.World.Ticks == 0 && round.State == EvaluationState.WaitingForHuman, "Competition started before human input.");
            round.RequestFlap();
            round.Tick();
            double afterFlap = GameRules.FlapVelocity - GameRules.Gravity * GameRules.Step;
            Check(round.Human.velocity == afterFlap, "First press did not flap the human.");
            round.Tick();
            Check(round.Human.velocity == afterFlap - GameRules.Gravity * GameRules.Step, "One press produced repeated flaps.");
            round.Pause();
            int ticks = round.World.Ticks;
            round.Tick();
            Check(round.World.Ticks == ticks, "Paused round keeps advancing.");
            round.RequestFlap();
            round.Tick();
            Check(round.State == EvaluationState.Running && round.World.Ticks == ticks + 1, "Human input failed to resume.");
        });
        Run("Bot gets identical decisions in both evaluation modes", () =>
        {
            var genome = BrainGenome.Create(new DeterministicRandom(95));
            var solo = new EvaluationSession(genome, 96, EvaluationMode.WatchBot);
            var versus = new EvaluationSession(genome, 96, EvaluationMode.HumanVsBot);
            versus.RequestFlap();
            for (int i = 0; i < 2000 && solo.IsActive; i++)
            {
                solo.Tick();
                versus.Tick();
                Check(Json(solo.Bot) == Json(versus.Bot), "Bot physics/decisions changed when a human joined.");
            }
        });
        Console.WriteLine("Core checks passed: " + passed);

        if (arguments.Contains("--training-smoke"))
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var session = new TrainingSession("learning-smoke", new EvolutionSettings(), 314159);
            int completed = 0;
            int best = 0;
            while (completed < 60 && watch.Elapsed.TotalSeconds < 90)
            {
                var champion = session.Tick();
                if (champion == null) continue;
                completed++;
                best = Math.Max(best, champion.score);
                Console.WriteLine("Generation " + champion.generation + ": " + champion.score + " pipes, " + (champion.survivalTicks * GameRules.Step).ToString("F2") + " s");
                if (best >= 15) break;
            }
            Check(completed >= 1 && best >= 3, "The seeded learning smoke test failed to pass three pipes.");
            Console.WriteLine("Learning smoke: " + completed + " generations; best " + best + " pipes; " + watch.Elapsed.TotalSeconds.ToString("F1") + " s");
        }
    }
}
