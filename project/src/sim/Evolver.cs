using Godot;
using NodeRunner.ML;
using NodeRunner.ML.Brains;
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
    private Activation[] _outputActivations = [];
    private int[] _disabledGenes = [];
    private double[][] _genomes = [];
    private double[] _fitness = [];
    private TrialResult[] _results = [];
    private ParallelEvaluationSchedule? _schedule;
    private int _followedShadow;
    private HashSet<int>? _drawn;
    private bool _opensWithPreviousBest;

    public int Generation { get; private set; }

    /// <summary>The best score ever reached, by any generation; it never goes down (#479).</summary>
    public double BestFitness { get; private set; } = double.NegativeInfinity;

    /// <summary>The generation that reached <see cref="BestFitness"/>; 0 before any has.</summary>
    public int BestGeneration { get; private set; }

    /// <summary>
    /// The furthest any latest run's front has ended up on this map (#725): the distance the best
    /// marker shows. It never goes down and never reads below the latest run, whichever run holds
    /// <see cref="BestFitness"/>. NaN before one is known.
    /// </summary>
    public double BestShownDistance { get; private set; } = double.NaN;

    public double MeanFitness { get; private set; }

    /// <summary>The best genome of the latest finished generation; null before one finishes, or when none of its trials was valid.</summary>
    public double[]? LatestGenome { get; private set; }

    /// <summary>What the trial behind <see cref="LatestGenome"/> measured.</summary>
    public TrialResult? LatestRun { get; private set; }

    public int[] LayerSizes => _layerSizes.ToArray();

    /// <summary>Number of candidates in the active generation.</summary>
    public int PopulationSize => _genomes.Length;

    /// <summary>Whether any candidate trial is currently active.</summary>
    public bool IsTrialActive => _primaryCreature is not null && _trialControllers.Any(controller => controller.IsRunning);

    /// <summary>Fitness values completed in the current generation.</summary>
    public double[] CompletedFitness => _fitness.ToArray();

    /// <summary>Number of candidates whose trials have completed in the current generation.</summary>
    public int CompletedCandidateCount => _schedule?.CompletedCount ?? 0;

    /// <summary>
    /// The shadow (zero-based slot, which is also its candidate index) drawn in full and shown in
    /// signal flow and the brain. Shadow 0 by default: the previous best, or shadow 1 in a fresh
    /// generation 0. It changes only through <see cref="Follow"/> (#385).
    /// </summary>
    public int FollowedShadow => _followedShadow;

    /// <summary>The followed shadow's creature, or the primary creature while stopped.</summary>
    public Creature.Creature? FollowedCreature =>
        _followedShadow < _creatures.Count ? _creatures[_followedShadow] : _primaryCreature;

    /// <summary>
    /// Whether shadow 0 runs the previous best: the resumed genome, or the elite the genetic
    /// algorithm carried over from the last generation.
    /// </summary>
    public bool HasPreviousBest => _opensWithPreviousBest;

    /// <summary>How far the followed shadow's front has got in its current trial (#725); NaN when none is running.</summary>
    public double FollowedTrialDistance => ShadowDistance(_followedShadow);

    /// <summary>Every shadow's front distance so far this trial (#725), NaN for a shadow that is not running.</summary>
    public double[] ShadowDistances => Enumerable.Range(0, _trialControllers.Count).Select(ShadowDistance).ToArray();

    /// <summary>Raised when the followed shadow changes.</summary>
    public event Action? FollowedShadowChanged;

    /// <summary>Raised when the followed shadow starts a new trial, back at the start.</summary>
    public event Action? FollowedTrialStarted;

    /// <summary>Raised after every genome in a generation has been evaluated and the next generation has been produced.</summary>
    public event Action? GenerationCompleted;

    /// <summary>Raised when active candidates, completed candidates, or the generation changes.</summary>
    public event Action? TrainingProgressChanged;

    /// <summary>Follows <paramref name="shadow"/> until the player picks another one.</summary>
    public void Follow(int shadow)
    {
        if (shadow < 0 || shadow >= Math.Max(_creatures.Count, 1))
        {
            throw new ArgumentOutOfRangeException(nameof(shadow));
        }

        if (shadow == _followedShadow)
        {
            return;
        }

        var previous = _followedShadow;
        SetShadowDrawing(previous, isShadow: true);
        _followedShadow = shadow;
        SetShadowDrawing(_followedShadow, isShadow: false);
        ApplyDrawn(previous);
        ApplyDrawn(_followedShadow);
        FollowedShadowChanged?.Invoke();
    }

    /// <summary>
    /// Draws only <paramref name="shadows"/> (zero-based) and the followed shadow: the shadow strip's
    /// page (#284). The others are hidden but still race and count. Every shadow is drawn until this
    /// is first called; the choice holds across <see cref="Start"/>.
    /// </summary>
    public void DrawOnly(IReadOnlyList<int> shadows)
    {
        ArgumentNullException.ThrowIfNull(shadows);
        _drawn = [.. shadows];
        for (var slot = 0; slot < _creatures.Count; slot++)
        {
            ApplyDrawn(slot);
        }
    }

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
        // The shadows are gone, so whoever followed one goes back to the scene's creature.
        FollowedShadowChanged?.Invoke();
        TrainingProgressChanged?.Invoke();
    }

    /// <summary>
    /// Begins evolving brains for the given creature. <paramref name="layerSizes"/>
    /// must match the creature's sensor/motor counts (its input/output layers).
    /// Supplying <paramref name="creatureFactory"/> runs every shadow of a
    /// generation at once, one slot per genome, so <paramref name="populationSize"/>
    /// is the Shadows value and at most <see cref="Creature.Creature.MaximumShadows"/>
    /// (#384); without it, evaluation remains sequential for compatibility. <paramref name="groundTopY"/> is the
    /// ground's top edge, which each trial measures elevation from.
    /// <paramref name="disabledGenes"/> are genome positions that stay 0 in every
    /// candidate: a saved brain's disabled connection genes. Without
    /// <paramref name="resumeGenome"/> the run starts at generation 0 (#537); with it, the saved
    /// elite and its children open the run (#538), and <see cref="BestFitness"/> starts from
    /// <paramref name="resumeBestFitness"/>, reached in <paramref name="resumeBestGeneration"/>,
    /// and <see cref="BestShownDistance"/> from <paramref name="resumeBestShownDistance"/>.
    /// </summary>
    public void Start(
        Creature.Creature creature,
        int populationSize,
        int[] layerSizes,
        GeneticAlgorithm ga,
        Random rng,
        float groundTopY,
        double[]? resumeGenome = null,
        int resumeGeneration = 0,
        double resumeBestFitness = double.NegativeInfinity,
        double resumeBestShownDistance = double.NaN,
        int resumeBestGeneration = 0,
        int trialDurationTicks = 600,
        Func<Creature.Creature>? creatureFactory = null,
        IReadOnlyList<int>? disabledGenes = null)
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

        if (creatureFactory is not null && populationSize > Creature.Creature.MaximumShadows)
        {
            throw new ArgumentOutOfRangeException(nameof(populationSize), $"At most {Creature.Creature.MaximumShadows} shadows run at once.");
        }

        if (resumeGenome is not null && resumeGenome.Length != NeuralNetwork.GenomeLength(layerSizes))
        {
            throw new ArgumentException("Resume genome must match the network layer sizes.", nameof(resumeGenome));
        }

        ReleaseSlots();
        _primaryCreature = creature;
        _layerSizes = layerSizes.ToArray();
        _outputActivations = DirectBrain.OutputActivations(creature.Ports);
        _ga = ga;
        _rng = rng;
        _disabledGenes = disabledGenes?.ToArray() ?? [];
        Generation = resumeGeneration;
        BestFitness = resumeGenome is null ? double.NegativeInfinity : resumeBestFitness;
        BestGeneration = resumeGenome is null ? 0 : resumeBestGeneration;
        BestShownDistance = resumeGenome is null ? double.NaN : resumeBestShownDistance;
        MeanFitness = 0;
        LatestGenome = null;
        LatestRun = null;

        _genomes = resumeGenome is null
            ? GenerationZero.Population(creature.Ports, populationSize, rng)
            : ga.FromElites([resumeGenome], populationSize, rng);
        _opensWithPreviousBest = resumeGenome is not null;

        SilenceDisabledGenes();
        _fitness = new double[populationSize];
        _results = new TrialResult[populationSize];

        var slotCount = creatureFactory is null ? 1 : populationSize;
        _schedule = new ParallelEvaluationSchedule(populationSize, slotCount);
        ConfigureSlots(creature, slotCount, trialDurationTicks, groundTopY, creatureFactory);
        StartAvailableSlots();
        FollowedShadowChanged?.Invoke();
    }

    private void SilenceDisabledGenes()
    {
        foreach (var genome in _genomes)
        {
            foreach (var gene in _disabledGenes)
            {
                genome[gene] = 0;
            }
        }
    }

    private void ConfigureSlots(
        Creature.Creature primaryCreature,
        int slotCount,
        int trialDurationTicks,
        float groundTopY,
        Func<Creature.Creature>? creatureFactory)
    {
        for (var slot = 0; slot < slotCount; slot++)
        {
            var creature = slot == 0
                ? primaryCreature
                : CreateParallelCreature(primaryCreature, creatureFactory!, slot);
            _creatures.Add(creature);
            ApplyDrawn(slot);

            var controller = new TrialController
            {
                Name = $"TrialController{slot + 1}",
                TrialDurationTicks = trialDurationTicks,
                GroundTopY = groundTopY,
            };
            var capturedSlot = slot;
            controller.TrialCompleted += result => OnTrialCompleted(capturedSlot, result);
            controller.TrialStarted += () => OnTrialStarted(capturedSlot);
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
        creature.Theme = primaryCreature.Theme;
        creature.Position = primaryCreature.Position;
        creature.ProcessMode = ProcessModeEnum.Pausable;
        creature.IsShadow = true;
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
        var brain = NeuralNetwork.FromGenome(_layerSizes, _genomes[genomeIndex], Activation.Tanh, _outputActivations);
        _creatures[slot].SetBrain(brain);
        _trialControllers[slot].StartTrial(_creatures[slot]);
    }

    private void OnTrialStarted(int slot)
    {
        if (slot == _followedShadow)
        {
            FollowedTrialStarted?.Invoke();
        }
    }

    private void OnTrialCompleted(int slot, TrialResult result)
    {
        var genomeIndex = _schedule!.ActiveCandidate(slot);
        if (genomeIndex < 0)
        {
            return;
        }

        _fitness[genomeIndex] = result.Fitness;
        _results[genomeIndex] = result;
        if (!result.IsValid)
        {
            GD.Print($"Generation {Generation}, candidate {genomeIndex}: invalid trial (physics blew up), scored worst.");
        }

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
        Generation++;
        var latestIndex = -1;
        for (var i = 0; i < _fitness.Length; i++)
        {
            if (double.IsFinite(_fitness[i]) && (latestIndex < 0 || _fitness[i] > _fitness[latestIndex]))
            {
                latestIndex = i;
            }
        }

        var isNewBest = latestIndex >= 0 && _fitness[latestIndex] > BestFitness;
        LatestGenome = latestIndex >= 0 ? _genomes[latestIndex].ToArray() : null;
        LatestRun = latestIndex >= 0 ? _results[latestIndex] : null;

        if (isNewBest)
        {
            BestFitness = _fitness[latestIndex];
            BestGeneration = Generation;
        }

        if (LatestRun is { } latest && !(latest.FrontDistance <= BestShownDistance))
        {
            BestShownDistance = latest.FrontDistance;
        }

        var validFitness = _fitness.Where(double.IsFinite).ToArray();
        MeanFitness = validFitness.Length > 0 ? validFitness.Average() : 0;

        _opensWithPreviousBest = _ga!.ElitismCount > 0 && _fitness.Any(double.IsFinite);
        _genomes = _ga.NextGeneration(_genomes, _fitness, _rng!);
        SilenceDisabledGenes();
        _fitness = new double[_genomes.Length];
        _results = new TrialResult[_genomes.Length];
        _schedule!.Reset();

        GenerationCompleted?.Invoke();
        TrainingProgressChanged?.Invoke();
        if (_primaryCreature is null)
        {
            return;
        }

        StartAvailableSlots();
    }

    private double ShadowDistance(int shadow) =>
        shadow < _trialControllers.Count && _trialControllers[shadow].IsRunning
            ? _trialControllers[shadow].Measured.FrontDistance
            : double.NaN;

    // Hiding a shadow's root stops Godot drawing all of its parts; its bodies still simulate.
    private void ApplyDrawn(int slot)
    {
        if (slot < _creatures.Count)
        {
            _creatures[slot].Visible = slot == _followedShadow || _drawn is null || _drawn.Contains(slot);
        }
    }

    private void SetShadowDrawing(int shadow, bool isShadow)
    {
        if (shadow < _creatures.Count)
        {
            _creatures[shadow].IsShadow = isShadow;
        }
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
        _followedShadow = 0;
        _opensWithPreviousBest = false;
        if (_primaryCreature is not null)
        {
            _primaryCreature.IsShadow = false;
            _primaryCreature.Visible = true;
        }
    }
}
