using System.ComponentModel;
using System.Globalization;
using Godot;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.Navigation;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Managers;
using NodeRunner.ML.Brains;
using NodeRunner.Sim;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Screens;
using NodeRunner.Ui.Widgets;

namespace NodeRunner.Hosts;

/// <summary>
/// The Training scene: trains one saved creation (#469) on the Training screen (#386). The scene
/// authors the screen and the world in its arena: camera, background, ground line and ruler. This
/// root builds the map's ground along that line, adds the creature and the <see cref="Evolver"/> to that world, points the camera and the ruler at
/// it, resumes from the creation's last finished
/// generation and saves every finished generation, so leaving drops only the one in progress. Run
/// on its own (F6) it trains the built-in worm without saving.
/// </summary>
public partial class TrainingHost : Node, IRoutedScene
{
    private const double _signalRefreshIntervalSeconds = 0.15;
    private const string _sampleCreationName = "Worm";

    // Engine.TimeScale speeds every physics step up uniformly, so it changes how fast a fixed number
    // of ticks plays out, never the result.
    private static readonly float[] _timeScales = [1f, 2f, 4f];


    private readonly VisualTheme _theme = VisualTheme.Neon;
    // The map Training runs on and records: the one Train setup selects (#444).
    private readonly MapDef _map = Maps.Default;
    private readonly SelectionViewModel _selection = new();
    private readonly SignalFlowPresentationViewModel _signalFlow = new();
    private readonly BrainFocusPresentationViewModel _brainFocus = new();
    private readonly List<SensorReading> _sensorReadings = [];
    private readonly List<MotorReading> _motorReadings = [];
    private TrainingRoute? _route;
    private ISceneNavigator? _navigator;
    private Guid? _creationId;
    // The brain last saved, so the next save keeps its neuron ids and disabled genes.
    private TrainingStateDef? _saved;
    private Creature.Creature? _creature;
    // The shadow drawn in full, read by signal flow, the brain and part selection (#385).
    private Creature.Creature? _followed;
    private Evolver? _evolver;
    private TrainingScreen _screen = null!;
    private TrainingPresentationViewModel _trainingPresentation = new();
    private int _timeScaleIndex;
    // The selected part's Build name, shown above the followed shadow (#388); null with nothing selected.
    private string? _selectedPartName;
    private double _signalRefreshElapsed;

    private SaveManager Saves => GetNode<SaveManager>("/root/SaveManager");

    private RngProvider Rng => GetNode<RngProvider>("/root/RngProvider");

    private Node2D World => GetNode<Node2D>("%World");

    // The scene places the ground line; the map builds the ground along it (BuildWorld).
    private ArenaGround Ground => GetNode<ArenaGround>("%Ground");

    private float GroundTopY => Ground.GlobalPosition.Y;

    private ArenaRuler Ruler => GetNode<ArenaRuler>("%Ruler");
    private ArenaBestMarker BestMarker => GetNode<ArenaBestMarker>("%BestMarker");
    private ArenaCamera Camera => GetNode<ArenaCamera>("%Camera");

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
        // After the camera has moved this frame, so the part name lands on the part.
        ProcessPriority = 1;
        _selection.PropertyChanged += OnSelectionPropertyChanged;
        var creation = LoadRouteCreation();
        BuildWorld();
        AddCreature(creation);
        AddEvolver();
        BindScreen(creation);
        AddBackHandler();
        if (creation is not null || _route is null)
        {
            StartEvolution(creation);
        }

        FollowCreature();
        ShowBest();
    }

    public override void _ExitTree()
    {
        // Don't let this scene's speed or pause leak into the next one.
        Engine.TimeScale = _timeScales[0];
        GetTree().Paused = false;
        _selection.PropertyChanged -= OnSelectionPropertyChanged;
        _trainingPresentation.PropertyChanged -= OnTrainingChanged;
        _trainingPresentation.Dispose();
    }

    // Moves the part name with the creature every frame. Refreshes the signal flow and brain focus
    // from the creature's last physics tick at a fixed cadence: the numbers are for a person to
    // read, so every rendered frame is wasted work.
    public override void _Process(double delta)
    {
        if (_followed is null)
        {
            return;
        }

        if (_selectedPartName is not null && _selection.SelectedElement is { } selected)
        {
            _screen.ShowPartName(_selectedPartName, _followed.PartAnchor(selected), _followed.Bounds);
        }

        _signalRefreshElapsed += delta;
        if (_signalRefreshElapsed < _signalRefreshIntervalSeconds)
        {
            return;
        }

        _signalRefreshElapsed = 0;
        _followed.ReadMapping(_sensorReadings, _motorReadings);
        _signalFlow.Update(_sensorReadings, _motorReadings, _evolver?.FollowedTrialDistance ?? double.NaN);
        _brainFocus.Update(_followed.Brain, _sensorReadings);
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
            Notify("Creations", "Finish the creation in Build before training it.");
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

    // The world's colours come from the same theme as the creature's; the ground's shape from the map.
    private void BuildWorld()
    {
        GetNode<ColorRect>("%ArenaFill").Color = _theme.ArenaBackground;
        Ruler.Theme = _theme;
        BestMarker.Theme = _theme;
        // Behind every creature part: a shadow draws its hatch at z -2 and its beams at -1. The world
        // has its own viewport, so this z orders only the world.
        BestMarker.ZIndex = -3;
        Ground.Build(_map.Ground, _theme);
    }

    private void AddCreature(CreationDef? creation)
    {
        var creature = CreateCreatureInstance();
        creature.Name = "Creature";
        // Pausable, not Inherit, so it stops simulating while the tree is paused.
        creature.ProcessMode = ProcessModeEnum.Pausable;
        creature.Definition = creation?.Creature ?? CreationExamples.CreateWormCreature();
        creature.Theme = _theme;
        creature.Position = GetNode<Marker2D>("%Spawn").Position;
        World.AddChild(creature);
        _creature = creature;
        _followed = creature;
    }

    // The camera follows the followed shadow's centre, the point its distance is measured from, and
    // glides to the new one when following changes (#668); it zooms to fit that shadow's box and
    // keeps the ground in place (#675). The ruler counts from where that point starts: every trial
    // resets every shadow to the same pose, so it starts there every time.
    private void FollowCreature()
    {
        if (_creature is not { } creature)
        {
            return;
        }

        Ruler.StartX = Ruler.ToLocal(creature.CenterOfMass).X;
        BestMarker.StartX = Ruler.StartX;
        Camera.GroundY = GroundTopY;

        // Read every frame; OnFollowedShadowChanged retargets it when the followed shadow changes.
        Camera.Follow(() => Framed(_followed ?? creature));
    }

    private static FramedCreature Framed(Creature.Creature creature)
    {
        var bounds = creature.Bounds;
        return new FramedCreature(
            creature.CenterOfMass.X,
            bounds.Position.X,
            bounds.Position.Y,
            bounds.End.X,
            bounds.End.Y);
    }

    private static Creature.Creature CreateCreatureInstance() =>
        GD.Load<PackedScene>("res://scenes/Creature.tscn").Instantiate<Creature.Creature>();

    private void BindScreen(CreationDef? creation)
    {
        _screen = GetNode<TrainingScreen>("%TrainingScreen");
        _screen.Setup(
            TrainingHeaderPresentation.For(creation?.Name ?? _sampleCreationName, TrainingRunMode.Train, _map.Id),
            _trainingPresentation,
            _signalFlow,
            _brainFocus);
        _screen.ShowPaused(GetTree().Paused);
        _screen.ShowSpeed(SpeedText());
        _screen.BackRequested += () => _navigator?.Back();
        _screen.PauseRequested += TogglePause;
        _screen.SpeedRequested += CycleTimeScale;
        _screen.StatsRequested += () => Notify("Stats", "Stats come in a later version.");
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
        evolver.FollowedShadowChanged += OnFollowedShadowChanged;
        evolver.FollowedTrialStarted += () => Camera.Cut();
        _trainingPresentation.Dispose();
        _trainingPresentation = new TrainingPresentationViewModel(new EvolverTrainingProgressSource(evolver));
        _trainingPresentation.PropertyChanged += OnTrainingChanged;
        World.AddChild(evolver);
        _evolver = evolver;
    }

    // Resumes from the creation's saved latest brain and generation (#479), or starts at generation 0
    // (#537) without one.
    private void StartEvolution(CreationDef? creation)
    {
        _evolver?.Stop();
        if (_creature?.Brain is null || _creature.Definition is not { } definition || _evolver is null)
        {
            // No motors: nothing to evolve.
            return;
        }

        var resume = creation?.Training;
        _saved = resume;
        // A best ever is per map: one reached on another map is not this map's record to beat.
        var resumeBest = resume?.Best is { } best && best.MapId == _map.Id ? best : null;
        var disabledGenes = resume is null ? null : DirectBrain.DisabledGenes(resume.Brain, _creature.Ports);
        _brainFocus.Configure(BrainPortLabels.For(definition), disabledGenes ?? []);
        var setup = EvolutionSetup.For(creation?.TrainSettings, Engine.PhysicsTicksPerSecond);
        _evolver.Start(
            _creature,
            setup.Population,
            _creature.Brain.LayerSizes,
            setup.Algorithm,
            Rng.Random,
            GroundTopY,
            resume is null ? null : DirectBrain.Compile(resume.Brain, _creature.Ports),
            resume?.Generation ?? 0,
            resumeBest?.Distance ?? double.NegativeInfinity,
            resumeBest?.Generation ?? 0,
            setup.TrialTicks,
            CreateCreatureInstance,
            disabledGenes: disabledGenes);
    }

    // The selection and the camera move to the new subject: the old shadow drops the selection, the
    // new one shows it, and the camera glides over.
    private void OnFollowedShadowChanged()
    {
        var followed = _evolver?.FollowedCreature ?? _creature;
        if (followed == _followed)
        {
            return;
        }

        _followed?.SetSelectedElement(null);
        _followed = followed;
        _followed?.SetSelectedElement(_selection.SelectedElement);
        Camera.Retarget();
    }

    // The best marker moves only when a generation sets a new best; Show ignores the rest.
    private void OnTrainingChanged(object? sender, PropertyChangedEventArgs eventArgs) => ShowBest();

    private void ShowBest() => BestMarker.Show(_trainingPresentation.BestFitness, _trainingPresentation.BestMarkerText);

    private void OnGenerationCompleted()
    {
        GD.Print($"Generation {_evolver!.Generation} — best: {_evolver.BestFitness:0.0}, mean: {_evolver.MeanFitness:0.0}");
        PersistTraining();
    }

    // Saving reads the creation back off disk and writes it again, synchronous file IO (#113) that
    // would stall physics at a generation boundary, so it runs on the thread pool. Only plain values
    // cross to that thread, and nothing comes back to this scene, which may be gone by then. Build
    // reads the creation only once the queued saves have landed (#370).
    private void PersistTraining()
    {
        // A generation without a valid trial has no latest brain, so it isn't saved.
        if (_creationId is not { } id
            || _evolver?.LatestGenome is not { } genome
            || _evolver.LatestRun is not { } run
            || _creature?.Brain is null)
        {
            return;
        }

        var saves = Saves;
        var epoch = saves.CurrentTrainingEpoch(id);
        var brain = DirectBrain.ToBrainDef(_creature.Ports, genome, _saved?.Brain);
        var latest = new TrainingRunDef(run.Distance, run.TopSpeed, run.Elevation, _map.Id);
        var training = TrainingStateDef.Record(_saved, brain, _evolver.Generation, latest);
        _saved = training;
        saves.PersistTrainingInBackground(id, epoch, training).ContinueWith(
            failed => ReportFailedSave(id, training.Generation, failed.Exception!),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private static void ReportFailedSave(Guid id, int generation, Exception exception)
    {
        // A data-loss condition: keep the whole exception and fail loud (CODE_DESIGN_PRINCIPLES
        // "Fail loud in dev"). Logged from the main thread, even after this scene is gone.
        var error = $"Failed to persist training state for Creation {id} at generation {generation}: {exception}";
        Callable.From(() => GD.PrintErr(error)).CallDeferred();
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
        if (_followed is not null && _followed.TrySelectPart(worldPosition, out var selection) && selection is not null)
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
            _followed?.SetSelectedElement(_selection.SelectedElement);
            _selectedPartName = _selection.SelectedElement is { } selected && _followed?.Definition is { } definition
                ? PartNames.Display(definition.Nodes, definition.Beams, definition.Sensors, definition.Pistons, selected.Id)
                : null;
            BestMarker.Faded = _selectedPartName is not null;
            if (_selectedPartName is null)
            {
                _screen.ShowPartName(null, default, default);
            }
        }
    }
}
