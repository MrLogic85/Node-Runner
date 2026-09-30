using System.ComponentModel;
using System.Globalization;
using Godot;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.Navigation;
using NodeRunner.App.ViewModels;
using NodeRunner.Creature;
using NodeRunner.Domain;
using NodeRunner.Managers;
using NodeRunner.ML;
using NodeRunner.ML.Ga;
using NodeRunner.Sim;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Screens;

namespace NodeRunner.Hosts;

/// <summary>
/// The Training scene: trains one saved creation (#469) on the Training screen (#386). The scene
/// authors the screen and the world in its arena: camera, backdrop and ground. This root adds the
/// creature and the <see cref="Evolver"/> to that world, resumes from the creation's last finished
/// generation and saves every finished generation, so leaving drops only the one in progress. Run
/// on its own (F6) it trains the built-in worm without saving.
/// </summary>
public partial class TrainingHost : Node, IRoutedScene
{
    private const double _extraCoreUnlockFitness = 50;
    private const double _signalRefreshIntervalSeconds = 0.15;
    private const string _sampleCreationName = "Worm";

    // Engine.TimeScale speeds every physics step up uniformly, so it changes how fast a fixed number
    // of ticks plays out, never the result.
    private static readonly float[] _timeScales = [1f, 2f, 4f];

    // Every run trains with these settings until Train setup (#194) offers the choice.
    private static readonly TrainingProfile _profile = new("Standard", 8, 600, 50, 0.1, 0.3, 3, CrossoverStrategy.Uniform);

    private readonly VisualTheme _theme = VisualTheme.Neon;
    private readonly SelectionViewModel _selection = new();
    private readonly SignalFlowPresentationViewModel _signalFlow = new();
    private readonly BrainFocusPresentationViewModel _brainFocus = new();
    private readonly List<SensorReading> _sensorReadings = [];
    private readonly List<MotorReading> _motorReadings = [];
    private TrainingRoute? _route;
    private ISceneNavigator? _navigator;
    private Guid? _creationId;
    private Creature.Creature? _creature;
    private Evolver? _evolver;
    private TrainingScreen _screen = null!;
    private TrainingPresentationViewModel _trainingPresentation = new();
    private int _sessionGenerationStart;
    private int _timeScaleIndex;
    private double _signalRefreshElapsed;

    private SaveManager Saves => GetNode<SaveManager>("/root/SaveManager");

    private RngProvider Rng => GetNode<RngProvider>("/root/RngProvider");

    private Node2D World => GetNode<Node2D>("%World");

    // Every creation trains on flat ground until maps land (#443).
    private float GroundTopY
    {
        get
        {
            var ground = GetNode<StaticBody2D>("%Ground");
            var shape = (RectangleShape2D)GetNode<CollisionShape2D>("%GroundShape").Shape;
            return ground.GlobalPosition.Y - (shape.Size.Y / 2);
        }
    }

    public void Enter(SceneRoute route, ISceneNavigator navigator)
    {
        _route = (TrainingRoute)route;
        _navigator = navigator;
    }

    public override void _Ready()
    {
        // Engine.TimeScale and pause are global, not scoped to this scene: start from 1x, running.
        Engine.TimeScale = _timeScales[0];
        GetTree().Paused = false;
        // Always, so tapping a part and the screen's buttons still work while paused (#85); the
        // creature and the Evolver pin themselves back to Pausable.
        ProcessMode = ProcessModeEnum.Always;
        _selection.PropertyChanged += OnSelectionPropertyChanged;

        var creation = LoadRouteCreation();
        ApplyWorldTheme();
        AddCreature(creation);
        AddEvolver();
        BindScreen(creation);
        AddBackHandler();
        if (creation is not null || _route is null)
        {
            StartEvolution(creation);
        }
    }

    public override void _ExitTree()
    {
        // Don't let this scene's speed or pause leak into the next one.
        Engine.TimeScale = _timeScales[0];
        GetTree().Paused = false;
        _selection.PropertyChanged -= OnSelectionPropertyChanged;
        _trainingPresentation.Dispose();
    }

    // Refreshes the signal flow and brain focus from the creature's last physics tick, at a fixed
    // cadence: the numbers are for a person to read, so every rendered frame is wasted work.
    public override void _Process(double delta)
    {
        if (_creature is null)
        {
            return;
        }

        _signalRefreshElapsed += delta;
        if (_signalRefreshElapsed < _signalRefreshIntervalSeconds)
        {
            return;
        }

        _signalRefreshElapsed = 0;
        _creature.ReadMapping(_sensorReadings, _motorReadings);
        _signalFlow.Update(_sensorReadings, _motorReadings, _evolver?.VisibleTrialDistance ?? double.NaN);
        _brainFocus.Update(_creature.Brain, _sensorReadings, _motorReadings);
    }

    private CreationDef? LoadRouteCreation()
    {
        if (_route is null)
        {
            return null;
        }

        var creation = Saves.Get(_route.CreationId);
        if (creation is null)
        {
            GD.PrintErr($"Creation {_route.CreationId} was not found.");
            Notify("Creations", "That creation could not be found.");
        }
        else if (!CreatureReadiness.CanTrain(creation.Creature))
        {
            // A saved drawing may be unfinished (#515); Build stops it before training.
            GD.PrintErr($"Creation {_route.CreationId} cannot train yet.");
            Notify("Creations", "Finish the creature in Build before training it.");
        }
        else
        {
            _creationId = creation.Id;
            return creation;
        }

        Callable.From(ReturnToCreations).CallDeferred();
        return null;
    }

    private void ReturnToCreations() => _navigator?.ReturnToRoot();

    private void Notify(string title, string message) =>
        UiNotificationLayer.Enqueue(this, new UiNotificationSpec(UiPopupType.Default, title, message));

    // Android Back and Escape: the brain sheet closes first, then Back returns to the previous
    // scene. Training is already saved up to the last finished generation.
    private void AddBackHandler()
    {
        if (_navigator is null)
        {
            return;
        }

        var back = new UiBackHandler();
        back.BackRequested += OnBackRequested;
        AddChild(back, @internal: InternalMode.Front);
    }

    private void OnBackRequested()
    {
        if (!_screen.CloseOverlay())
        {
            _navigator?.Back();
        }
    }

    // The world's colours come from the same theme as the creature's.
    private void ApplyWorldTheme()
    {
        GetNode<ArenaBackdrop>("%ArenaBackdrop").Theme = _theme;
        GetNode<Polygon2D>("%GroundFill").Color = _theme.GroundFill;
        var edge = GetNode<Line2D>("%GroundEdge");
        edge.DefaultColor = _theme.GroundEdge;
        edge.Width = _theme.GroundEdgeWidth;
    }

    private void AddCreature(CreationDef? creation)
    {
        var creature = CreateCreatureInstance();
        creature.Name = "Creature";
        // Pausable, not Inherit, so it stops simulating while the tree is paused.
        creature.ProcessMode = ProcessModeEnum.Pausable;
        creature.Definition = creation?.Creature ?? HardcodedCreatureFactory.Create();
        creature.BrainShape = creation?.BrainShape ?? BrainShapeDef.Default;
        creature.Theme = _theme;
        creature.Position = GetNode<Marker2D>("%Spawn").Position;
        World.AddChild(creature);
        _creature = creature;
    }

    private static Creature.Creature CreateCreatureInstance() =>
        GD.Load<PackedScene>("res://scenes/Creature.tscn").Instantiate<Creature.Creature>();

    private void BindScreen(CreationDef? creation)
    {
        _screen = GetNode<TrainingScreen>("%TrainingScreen");
        _screen.Setup(
            TrainingHeaderPresentation.For(creation?.Name ?? _sampleCreationName, TrainingRunMode.Train, MapIds.Flat),
            _trainingPresentation,
            _signalFlow,
            _brainFocus);
        _screen.ShowPaused(GetTree().Paused);
        _screen.ShowSpeed(SpeedText());
        _screen.BackRequested += () => _navigator?.Back();
        _screen.PauseRequested += TogglePause;
        _screen.SpeedRequested += CycleTimeScale;
        _screen.StatsRequested += () => Notify("Stats", "Stats open in milestone 0.12.0.");
        _screen.ArenaPressed += SelectPartAt;
    }

    // Evolves a population of brains in parallel physics slots, one generation after another.
    private void AddEvolver()
    {
        var evolver = new Evolver
        {
            Name = "Evolver",
            // Pausable, not Inherit, so training stops while the tree is paused.
            ProcessMode = ProcessModeEnum.Pausable,
        };
        evolver.GenerationCompleted += OnGenerationCompleted;
        evolver.NewBestFound += TryUnlockProgression;
        _trainingPresentation.Dispose();
        _trainingPresentation = new TrainingPresentationViewModel(new EvolverTrainingProgressSource(evolver));
        World.AddChild(evolver);
        _evolver = evolver;
    }

    // Resumes from the creation's saved best genome and generation, or starts a fresh random
    // population without one.
    private void StartEvolution(CreationDef? creation)
    {
        _evolver?.Stop();
        if (_creature?.Brain is null || _evolver is null)
        {
            // No motors: nothing to evolve.
            return;
        }

        var resume = creation?.Training;
        _sessionGenerationStart = resume?.Generation ?? 0;
        _evolver.Start(
            _creature,
            _profile.PopulationSize,
            _creature.Brain.LayerSizes,
            new GeneticAlgorithm(_profile.TournamentSize, _profile.MutationRate, _profile.MutationStrength, crossoverStrategy: _profile.CrossoverStrategy),
            Rng.Random,
            GroundTopY,
            resume?.BestGenome,
            resume?.Generation ?? 0,
            _profile.TrialDurationTicks,
            CreateCreatureInstance);
    }

    private void OnGenerationCompleted()
    {
        GD.Print($"Generation {_evolver!.Generation} — best: {_evolver.BestFitness:0.0}, mean: {_evolver.MeanFitness:0.0}");
        PersistTraining();
        if (_evolver.Generation - _sessionGenerationStart >= _profile.MaxGenerations)
        {
            _evolver.Stop();
            GD.Print($"Training session complete after {_profile.MaxGenerations} generations.");
        }
    }

    private void TryUnlockProgression()
    {
        if (_evolver is null || _evolver.BestFitness < _extraCoreUnlockFitness)
        {
            return;
        }

        var attributionId = _creationId is { } id && Saves.Get(id) is not null ? id : (Guid?)null;
        if (Saves.UnlockExtraCore(_evolver.Generation, attributionId))
        {
            GD.Print($"Unlocked extra core at generation {_evolver.Generation}.");
        }
    }

    // Saving reads the creation back off disk and writes it again, synchronous file IO (#113) that
    // would stall physics at a generation boundary, so it runs on the thread pool. Only plain values
    // cross to that thread, and nothing comes back to this scene, which may be gone by then.
    private void PersistTraining()
    {
        if (_creationId is not { } id || _evolver?.BestGenome is not { } genome || _creature?.Brain is null)
        {
            return;
        }

        var saves = Saves;
        var epoch = saves.CurrentTrainingEpoch(id);
        var training = new TrainingStateDef(
            _evolver.LayerSizes,
            genome.ToArray(),
            _evolver.Generation,
            Activation.Tanh.ToString(),
            _evolver.BestFitness,
            BestRunOf(_evolver));
        Task.Run(() => PersistTrainingSnapshot(saves, id, epoch, training));
    }

    // Training runs on flat ground only until maps land (#443).
    private static TrainingRunDef? BestRunOf(Evolver evolver) =>
        evolver.BestRun is { } run
            ? new TrainingRunDef(run.Distance, run.TopSpeed, run.Elevation, MapIds.Flat)
            : null;

    private static void PersistTrainingSnapshot(SaveManager saves, Guid id, long epoch, TrainingStateDef training)
    {
        try
        {
            saves.TryPersistTraining(id, epoch, training);
        }
        catch (Exception ex)
        {
            // A data-loss condition: keep the whole exception and fail loud (CODE_DESIGN_PRINCIPLES
            // "Fail loud in dev"). Logged from the main thread, even after this scene is gone.
            var error = $"Failed to persist training state for Creation {id} at generation {training.Generation}: {ex}";
            Callable.From(() => GD.PrintErr(error)).CallDeferred();
        }
    }

    // Pausing freezes the whole tree, so the trial in progress, its fitness and its boundary checks
    // all stop with it; this scene and its screen stay Always so Run still responds.
    private void TogglePause()
    {
        var tree = GetTree();
        tree.Paused = !tree.Paused;
        _screen.ShowPaused(tree.Paused);
    }

    private void CycleTimeScale()
    {
        _timeScaleIndex = (_timeScaleIndex + 1) % _timeScales.Length;
        Engine.TimeScale = _timeScales[_timeScaleIndex];
        _screen.ShowSpeed(SpeedText());
    }

    private string SpeedText() => string.Create(CultureInfo.InvariantCulture, $"{_timeScales[_timeScaleIndex]:0}x");

    // A tap in the arena selects the part of the creature under it, or clears the selection.
    private void SelectPartAt(Vector2 worldPosition)
    {
        if (_creature is not null && _creature.TrySelectPart(worldPosition, out var selection) && selection is not null)
        {
            _selection.Select(selection);
        }
        else
        {
            _selection.Clear();
        }
    }

    private void OnSelectionPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(SelectionViewModel.SelectedElement))
        {
            _creature?.SetSelectedElement(_selection.SelectedElement);
        }
    }
}
