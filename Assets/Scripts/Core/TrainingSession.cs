using System;

namespace FlappyBird.Core
{
    [Serializable]
    public sealed class GenerationChampion
    {
        public int formatVersion = 1;
        public int rulesVersion = GameRules.Version;
        public string runId;
        public int generation;
        public int birdIndex;
        public int score;
        public int survivalTicks;
        public int tiedSurvivors;
        public bool generationCompleted;
        public uint courseSeed;
        public BrainGenome brain;
    }

    [Serializable]
    public sealed class TrainingSnapshot
    {
        public int formatVersion = 1;
        public int rulesVersion = GameRules.Version;
        public string runId;
        public string createdUtc;
        public int generation;
        public int bestScore;
        public uint geneticRandomState;
        public uint courseSeed;
        public EvolutionSettings settings;
        public BrainGenome[] population;
        public WorldSnapshot world;
    }

    public sealed class NeuralPilot
    {
        private readonly NeuralNetwork network;
        private readonly double[] inputs = new double[6];

        public NeuralPilot(BrainGenome genome) { network = new NeuralNetwork(genome); }

        public bool Decide(FlappyWorld world, int birdIndex)
        {
            world.ReadSensors(birdIndex, inputs);
            return network.Evaluate(inputs) > 0.5;
        }
    }

    public sealed class TrainingSession
    {
        private readonly EvolutionSettings settings;
        private readonly DeterministicRandom random;
        private readonly GeneticAlgorithm evolution;
        private BrainGenome[] population;
        private NeuralPilot[] pilots;
        private bool[] actions;
        private uint courseSeed;

        public string RunId { get; private set; }
        public string CreatedUtc { get; private set; }
        public int Generation { get; private set; }
        public int BestScore { get; private set; }
        public FlappyWorld World { get; private set; }
        public int PopulationSize { get { return population.Length; } }

        public TrainingSession(string runId, EvolutionSettings settings, uint seed)
        {
            settings.Validate();
            RunId = runId;
            CreatedUtc = DateTime.UtcNow.ToString("O");
            this.settings = settings.Clone();
            random = new DeterministicRandom(seed);
            evolution = new GeneticAlgorithm(this.settings, random);
            population = new BrainGenome[settings.populationSize];
            for (int i = 0; i < population.Length; i++) population[i] = BrainGenome.Create(random);
            Generation = 1;
            StartGeneration();
        }

        public TrainingSession(TrainingSnapshot snapshot)
        {
            if (snapshot == null || snapshot.formatVersion != 1 || snapshot.rulesVersion != GameRules.Version ||
                string.IsNullOrEmpty(snapshot.runId) || snapshot.generation < 1 || snapshot.bestScore < 0 ||
                snapshot.settings == null || snapshot.population == null || snapshot.geneticRandomState == 0 ||
                snapshot.courseSeed == 0) throw new ArgumentException("Incompatible training checkpoint.");
            snapshot.settings.Validate();
            if (snapshot.population.Length != snapshot.settings.populationSize)
                throw new ArgumentException("Incomplete checkpoint population.");
            settings = snapshot.settings.Clone();
            random = new DeterministicRandom(snapshot.geneticRandomState);
            evolution = new GeneticAlgorithm(settings, random);
            population = new BrainGenome[snapshot.population.Length];
            for (int i = 0; i < population.Length; i++)
            {
                if (snapshot.population[i] == null) throw new ArgumentException("Missing brain.");
                snapshot.population[i].Validate();
                if (i > 0)
                {
                    var topology = snapshot.population[0].layers;
                    var candidate = snapshot.population[i].layers;
                    if (candidate.Length != topology.Length) throw new ArgumentException("Population topologies do not match.");
                    for (int layer = 0; layer < topology.Length; layer++)
                        if (candidate[layer] != topology[layer]) throw new ArgumentException("Population topologies do not match.");
                }
                population[i] = snapshot.population[i].Clone();
            }
            World = new FlappyWorld(snapshot.world);
            if (World.Birds.Length != population.Length || World.AliveCount == 0)
                throw new ArgumentException("Checkpoint world and population do not match.");
            RunId = snapshot.runId;
            CreatedUtc = snapshot.createdUtc;
            Generation = snapshot.generation;
            BestScore = snapshot.bestScore;
            courseSeed = snapshot.courseSeed;
            CreatePilots();
        }

        private void CreatePilots()
        {
            pilots = new NeuralPilot[population.Length];
            actions = new bool[population.Length];
            for (int i = 0; i < pilots.Length; i++) pilots[i] = new NeuralPilot(population[i]);
        }

        private void StartGeneration()
        {
            courseSeed = random.NextUInt();
            World = new FlappyWorld(population.Length, courseSeed);
            CreatePilots();
        }

        // Null means the current generation is still running. No timer stops a survivor.
        public GenerationChampion Tick()
        {
            for (int i = 0; i < pilots.Length; i++)
                actions[i] = World.Birds[i].alive && pilots[i].Decide(World, i);
            World.Tick(actions);
            foreach (var bird in World.Birds) BestScore = Math.Max(BestScore, bird.score);
            if (World.AliveCount > 0) return null;

            int winnerIndex = GeneticAlgorithm.Rank(World.Birds)[0];
            var winner = World.Birds[winnerIndex];
            int ties = 0;
            foreach (var bird in World.Birds) if (bird.survivalTicks == winner.survivalTicks) ties++;
            var champion = new GenerationChampion { runId = RunId, generation = Generation,
                birdIndex = winnerIndex, score = winner.score, survivalTicks = winner.survivalTicks,
                tiedSurvivors = ties, generationCompleted = true, courseSeed = courseSeed, brain = population[winnerIndex].Clone() };
            population = evolution.Evolve(population, World.Birds);
            Generation++;
            StartGeneration();
            return champion;
        }

        public GenerationChampion CaptureLivingSurvivor()
        {
            if (World.AliveCount != 1) return null;
            for (int i = 0; i < World.Birds.Length; i++)
            {
                var bird = World.Birds[i];
                if (!bird.alive) continue;
                return new GenerationChampion { runId = RunId, generation = Generation, birdIndex = i,
                    score = bird.score, survivalTicks = bird.survivalTicks, tiedSurvivors = 1,
                    generationCompleted = false, courseSeed = courseSeed, brain = population[i].Clone() };
            }
            return null;
        }

        public TrainingSnapshot Capture()
        {
            var copy = new BrainGenome[population.Length];
            for (int i = 0; i < copy.Length; i++) copy[i] = population[i].Clone();
            return new TrainingSnapshot { runId = RunId, createdUtc = CreatedUtc, generation = Generation,
                bestScore = BestScore, settings = settings.Clone(), population = copy,
                geneticRandomState = random.State, courseSeed = courseSeed, world = World.Capture() };
        }
    }
}
