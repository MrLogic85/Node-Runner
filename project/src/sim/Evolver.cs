using Godot;
using NodeRunner.ML;
using NodeRunner.ML.Ga;

namespace NodeRunner.Sim;

/// <summary>
/// Runs the generation cycle in fixed parallel slots. Each slot owns one
/// creature and one <see cref="TrialController"/>; completed slots receive
/// the next pending genome until the generation is evaluated. The resulting
/// fitness scores are passed to <see cref="GeneticAlgorithm"/>. See
/// docs/TRAINING_LOOP.md.
/// </summary>
public partial class Evolver : Node
{
    private GeneticAlgorithm? _ga;
    private Random? _rng;
    private Creature.Creature? _primaryCreature;
    private readonly List<Creature.Creature> _creatures = [];
    private readonly List<TrialController> _trialControllers = [];
    private int[] _layerSizes = [];
    private double[][] _genomes = [];
    private double[] _fitness = [];
    private ParallelEvaluationSchedule? _schedule;

    public int Generation { get; private set; }

    public double BestFitness { get; private set; } = double.NegativeInfinity;

    public double MeanFitness { get; private set; }

    public double[]? BestGenome { get; private set; }

    public int[] LayerSizes => _layerSizes.ToArray();

    /// <summary>Number of candidates in the active generation.</summary>
    public int PopulationSize => _genomes.Length;

    /// <summary>Lowest one-based candidate number currently being evaluated, or zero when stopped.</summary>
    public int CurrentCandidate => (_schedule?.LowestActiveCandidate ?? -1) + 1;

    /// <summary>Whether any candidate trial is currently active.</summary>
    public bool IsTrialActive => _primaryCreature is not null && _trialControllers.Any(controller => controller.IsRunning);

    /// <summary>Fitness values completed in the current generation.</summary>
    public double[] CompletedFitness => _fitness.ToArray();

    /// <summary>Number of candidates whose trials have completed in the current generation.</summary>
    public int CompletedCandidateCount => _schedule?.CompletedCount ?? 0;

    /// <summary>Raised after every genome in a generation has been evaluated and the next generation has been produced.</summary>
    public event Action? GenerationCompleted;

    /// <summary>Raised when a generation's best fitness exceeds every previous generation's best.</summary>
    public event Action? NewBestFound;

    /// <summary>Raised when active candidates, completed candidates, or the generation changes.</summary>
    public event Action? TrainingProgressChanged;

    /// <summary>
    /// Halts the current generation cycle, removes parallel slot creatures,
    /// forgets the primary creature, and notifies progress subscribers of
    /// the inactive state. Safe to call when nothing is running. Callers
    /// must call <see cref="Start"/> again to resume.
    /// </summary>
    public void Stop()
    {
        ReleaseSlots();
        _primaryCreature = null;
        TrainingProgressChanged?.Invoke();
    }

    /// <summary>
    /// Begins evolving brains for the given creature. <paramref name="layerSizes"/>
    /// must match the creature's sensor/motor counts (its input/output layers).
    /// Supplying <paramref name="creatureFactory"/> enables fixed parallel
    /// slots up to <paramref name="maxParallelSlots"/>; without it, evaluation
    /// remains sequential for compatibility.
    /// </summary>
    public void Start(
        Creature.Creature creature,
        int populationSize,
        int[] layerSizes,
        GeneticAlgorithm ga,
        Random rng,
        double[]? resumeGenome = null,
        int resumeGeneration = 0,
        int trialDurationTicks = 600,
        Func<Creature.Creature>? creatureFactory = null,
        int maxParallelSlots = Creature.Creature.MaximumCollisionSlots)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(layerSizes);
        ArgumentNullException.ThrowIfNull(ga);
        ArgumentNullException.ThrowIfNull(rng);
        if (populationSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(populationSize), "Population size must be at least 1.");
        }

        if (resumeGeneration < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(resumeGeneration));
        }

        if (trialDurationTicks < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(trialDurationTicks));
        }

        if (maxParallelSlots is < 1 or > Creature.Creature.MaximumCollisionSlots)
        {
            throw new ArgumentOutOfRangeException(nameof(maxParallelSlots));
        }

        if (resumeGenome is not null && resumeGenome.Length != NeuralNetwork.GenomeLength(layerSizes))
        {
            throw new ArgumentException("Resume genome must match the network layer sizes.", nameof(resumeGenome));
        }

        ReleaseSlots();
        _primaryCreature = creature;
        _layerSizes = layerSizes.ToArray();
        _ga = ga;
        _rng = rng;
        Generation = resumeGeneration;
        BestFitness = double.NegativeInfinity;
        MeanFitness = 0;
        BestGenome = resumeGenome?.ToArray();

        _genomes = CreateRandomPopulation(populationSize);
        if (resumeGenome is not null)
        {
            _genomes[0] = resumeGenome.ToArray();
        }
        _fitness = new double[populationSize];

        var slotCount = creatureFactory is null
            ? 1
            : Math.Min(populationSize, maxParallelSlots);
        _schedule = new ParallelEvaluationSchedule(populationSize, slotCount);
        ConfigureSlots(creature, slotCount, trialDurationTicks, creatureFactory);
        StartAvailableSlots();
    }

    private double[][] CreateRandomPopulation(int populationSize)
    {
        var genomes = new double[populationSize][];
        for (var i = 0; i < populationSize; i++)
        {
            genomes[i] = new NeuralNetwork(_layerSizes, Activation.Tanh, _rng!).FlattenGenome();
        }

        return genomes;
    }

    private void ConfigureSlots(
        Creature.Creature primaryCreature,
        int slotCount,
        int trialDurationTicks,
        Func<Creature.Creature>? creatureFactory)
    {
        for (var slot = 0; slot < slotCount; slot++)
        {
            var creature = slot == 0
                ? primaryCreature
                : CreateParallelCreature(primaryCreature, creatureFactory!, slot);
            creature.ConfigureCollisionSlot(slot + 1);
            _creatures.Add(creature);

            var controller = new TrialController
            {
                Name = $"TrialController{slot + 1}",
                TrialDurationTicks = trialDurationTicks,
            };
            var capturedSlot = slot;
            controller.TrialCompleted += fitness => OnTrialCompleted(capturedSlot, fitness);
            AddChild(controller);
            _trialControllers.Add(controller);
        }
    }

    private Creature.Creature CreateParallelCreature(
        Creature.Creature primaryCreature,
        Func<Creature.Creature> creatureFactory,
        int slot)
    {
        var creature = creatureFactory();
        creature.Name = $"ParallelCreature{slot + 1}";
        creature.Definition = primaryCreature.Definition;
        creature.BrainShape = primaryCreature.BrainShape;
        creature.Theme = primaryCreature.Theme;
        creature.Position = primaryCreature.Position;
        creature.ProcessMode = ProcessModeEnum.Pausable;
        creature.Visible = false;
        AddChild(creature);
        return creature;
    }

    private void StartAvailableSlots()
    {
        for (var slot = 0; slot < _creatures.Count; slot++)
        {
            if (_schedule!.ActiveCandidate(slot) < 0 && _schedule.TryAssignNext(slot, out var genomeIndex))
            {
                StartCandidate(slot, genomeIndex);
            }
        }

        TrainingProgressChanged?.Invoke();
    }

    private void StartCandidate(int slot, int genomeIndex)
    {
        var brain = NeuralNetwork.FromGenome(_layerSizes, _genomes[genomeIndex], Activation.Tanh);
        _creatures[slot].SetBrain(brain, seed: (Generation * _genomes.Length) + genomeIndex);
        _trialControllers[slot].StartTrial(_creatures[slot]);
    }

    private void OnTrialCompleted(int slot, float fitness)
    {
        var genomeIndex = _schedule!.ActiveCandidate(slot);
        if (genomeIndex < 0)
        {
            return;
        }

        _fitness[genomeIndex] = fitness;
        _schedule.Complete(slot);

        if (_schedule.IsComplete)
        {
            FinishGeneration();
            return;
        }

        if (_schedule.TryAssignNext(slot, out var nextGenomeIndex))
        {
            StartCandidate(slot, nextGenomeIndex);
        }

        TrainingProgressChanged?.Invoke();
    }

    private void FinishGeneration()
    {
        var generationBest = _fitness.Max();
        var isNewBest = generationBest > BestFitness;

        if (isNewBest)
        {
            BestGenome = _genomes[Array.IndexOf(_fitness, generationBest)].ToArray();
        }

        BestFitness = Math.Max(BestFitness, generationBest);
        MeanFitness = _fitness.Average();
        Generation++;

        _genomes = _ga!.NextGeneration(_genomes, _fitness, _rng!);
        _fitness = new double[_genomes.Length];
        _schedule!.Reset();

        GenerationCompleted?.Invoke();
        TrainingProgressChanged?.Invoke();
        if (isNewBest)
        {
            NewBestFound?.Invoke();
        }

        if (_primaryCreature is null)
        {
            return;
        }

        StartAvailableSlots();
    }

    private void ReleaseSlots()
    {
        foreach (var controller in _trialControllers)
        {
            controller.Stop();
            RemoveChild(controller);
            controller.QueueFree();
        }

        for (var slot = 1; slot < _creatures.Count; slot++)
        {
            var creature = _creatures[slot];
            RemoveChild(creature);
            creature.QueueFree();
        }

        _trialControllers.Clear();
        _creatures.Clear();
        _schedule = null;
    }
}
