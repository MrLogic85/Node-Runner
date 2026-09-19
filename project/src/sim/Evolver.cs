using Godot;
using NodeRunner.ML;
using NodeRunner.ML.Ga;

namespace NodeRunner.Sim;

/// <summary>
/// Runs the generation cycle for one creature: evaluates every genome in
/// the current generation sequentially (one <see cref="TrialController"/>
/// trial each), then hands the resulting fitness scores to
/// <see cref="GeneticAlgorithm"/> to produce the next generation. See
/// docs/TRAINING_LOOP.md.
///
/// This evaluates candidates one at a time on a single creature instance
/// rather than running a parallel population — "repeated trials of one
/// creature" is an explicitly valid reading of the 0.4.0 roadmap goal, and
/// keeps this slice free of collision-layer/population-lifecycle concerns.
/// </summary>
public partial class Evolver : Node
{
    private readonly TrialController _trialController = new() { Name = "TrialController" };

    private GeneticAlgorithm? _ga;
    private Random? _rng;
    private Creature.Creature? _creature;
    private int[] _layerSizes = [];
    private double[][] _genomes = [];
    private double[] _fitness = [];
    private int _currentIndex;

    public int Generation { get; private set; }

    public double BestFitness { get; private set; } = double.NegativeInfinity;

    public double MeanFitness { get; private set; }

    public double[]? BestGenome { get; private set; }

    public int[] LayerSizes => _layerSizes.ToArray();

    /// <summary>Raised after every genome in a generation has been evaluated and the next generation has been produced.</summary>
    public event Action? GenerationCompleted;

    /// <summary>Raised when a generation's best fitness exceeds every previous generation's best.</summary>
    public event Action? NewBestFound;

    public override void _Ready()
    {
        AddChild(_trialController);
        _trialController.TrialCompleted += OnTrialCompleted;
    }

    /// <summary>
    /// Halts the current generation cycle without raising any events, and
    /// forgets the creature it was evolving. Safe to call when nothing is
    /// running. Callers must call <see cref="Start"/> again to resume.
    /// </summary>
    public void Stop()
    {
        _trialController.Stop();
        _creature = null;
    }

    /// <summary>
    /// Begins evolving brains for the given creature. <paramref name="layerSizes"/>
    /// must match the creature's sensor/motor counts (its input/output layers).
    /// </summary>
    public void Start(
        Creature.Creature creature,
        int populationSize,
        int[] layerSizes,
        GeneticAlgorithm ga,
        Random rng,
        double[]? resumeGenome = null,
        int resumeGeneration = 0,
        int trialDurationTicks = 600)
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

        if (resumeGenome is not null && resumeGenome.Length != NeuralNetwork.GenomeLength(layerSizes))
        {
            throw new ArgumentException("Resume genome must match the network layer sizes.", nameof(resumeGenome));
        }

        _creature = creature;
        _trialController.TrialDurationTicks = trialDurationTicks;
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
        _currentIndex = 0;
        EvaluateCurrent();
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

    private void EvaluateCurrent()
    {
        var brain = NeuralNetwork.FromGenome(_layerSizes, _genomes[_currentIndex], Activation.Tanh);
        _creature!.SetBrain(brain, seed: (Generation * _genomes.Length) + _currentIndex);
        _trialController.StartTrial(_creature);
    }

    private void OnTrialCompleted(float fitness)
    {
        _fitness[_currentIndex] = fitness;
        _currentIndex++;

        if (_currentIndex < _genomes.Length)
        {
            EvaluateCurrent();
            return;
        }

        FinishGeneration();
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
        _currentIndex = 0;

        GenerationCompleted?.Invoke();
        if (isNewBest)
        {
            NewBestFound?.Invoke();
        }

        if (_creature is null)
        {
            return;
        }

        EvaluateCurrent();
    }
}
