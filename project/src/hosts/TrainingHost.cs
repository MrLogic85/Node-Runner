using System.ComponentModel;
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
/// The Training scene: trains one saved creation (#469) on the Training screen (#386), or simulates
/// it (#702). The scene authors the screen and the world in its arena: camera, background, ground
/// line and ruler. This root builds the map's ground along that line, adds the creature to that
/// world and points the camera and the ruler at it. To train, it adds the <see cref="Evolver"/>,
/// resumes from the creation's last finished generation and saves every finished generation, so
/// leaving drops only the one in progress. To simulate, it plays the saved brain on the creature in
/// one run that lasts until the player leaves, and saves nothing. Run on its own (F6) it trains the
/// Walker example without saving.
/// </summary>
public partial class TrainingHost : Node, IRoutedScene
{
    private const double _signalRefreshIntervalSeconds = 0.15;


    private readonly VisualTheme _theme = VisualTheme.Neon;
    // The map Training runs on and records: the one Train setup selects (#444).
    private readonly MapDef _map = Maps.Default;
    private readonly SelectionViewModel _selection = new();
    private readonly SignalFlowPresentationViewModel _signalFlow = new();
    private readonly BrainFocusPresentationViewModel _brainFocus = new();
    private readonly List<double> _brainInputs = [];
    private TrainingRoute? _route;
    private ISceneNavigator? _navigator;
    private Guid? _creationId;
    // The brain last saved, so the next save keeps its neuron ids and disabled genes.
    private TrainingStateDef? _saved;
    private Creature.Creature? _creature;
    // The shadow drawn in full, read by signal flow, the brain and part selection (#385).
    private Creature.Creature? _followed;
    private Evolver? _evolver;
    // Simulate's one endless run (#702); null while training.
    private TrialController? _playback;
    private TrainingScreen _screen = null!;
    private TrainingPresentationViewModel _trainingPresentation = new();
    // The selected part's Build name in the player's language, shown above the followed shadow (#388);
    // null with nothing selected.
    private Func<string>? _selectedPartName;
    private double _signalRefreshElapsed;
    // Training's slow-motion chip (#318); null in Simulate, which races one shadow.
    private SlowMotionWatch? _slowMotion;
    private ulong _watchedPhysicsFrames;
    private ulong _watchedUsec;
    private float _partsPixelScale;

    private SaveManager Saves => GetNode<SaveManager>("/root/SaveManager");

    private RngProvider Rng => GetNode<RngProvider>("/root/RngProvider");

    private Node2D World => GetNode<Node2D>("%World");

    // The scene places the ground line; the map builds the ground along it (BuildWorld).
    private ArenaGround Ground => GetNode<ArenaGround>("%Ground");

    private float GroundTopY => Ground.GlobalPosition.Y;

    private TrainingRunMode Mode => _route?.Mode ?? TrainingRunMode.Train;

    private ArenaRuler Ruler => GetNode<ArenaRuler>("%Ruler");
    private ArenaBestMarker BestMarker => GetNode<ArenaBestMarker>("%BestMarker");

    private ArenaStartSign StartSign => GetNode<ArenaStartSign>("%StartSign");
    private ArenaShadows Shadows => GetNode<ArenaShadows>("%Shadows");
    private ArenaCamera Camera => GetNode<ArenaCamera>("%Camera");

    public void Enter(SceneRoute route, ISceneNavigator navigator)
    {
        _route = (TrainingRoute)route;
        _navigator = navigator;
    }

    public override void _Ready()
    {
        // Pause is global, not scoped to this scene: start running.
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
        if (Mode == TrainingRunMode.Simulate)
        {
            if (creation is not null)
            {
                StartPlayback(creation);
            }

            BindScreen(creation);
            AddBackHandler();
        }
        else
        {
            AddEvolver();
            _slowMotion = new SlowMotionWatch(Engine.PhysicsTicksPerSecond);
            BindScreen(creation);
            AddBackHandler();
            if (creation is not null || _route is null)
            {
                StartEvolution(creation);
            }
        }

        FollowCreature();
        ShowBest();
    }

    public override void _ExitTree()
    {
        // Don't let this scene's pause leak into the next one.
        GetTree().Paused = false;
        _selection.PropertyChanged -= OnSelectionPropertyChanged;
        _trainingPresentation.PropertyChanged -= OnTrainingChanged;
        _trainingPresentation.Dispose();
    }

    // Redraws the creatures' parts once the camera has zoomed them to a new pixel scale, also
    // while paused (a resize re-frames the camera). Moves the part name with the creature every frame. Refreshes the signal flow and brain focus
    // from the creature's last physics tick at a fixed cadence: the numbers are for a person to
    // read, so every rendered frame is wasted work.
    public override void _Process(double delta)
    {
        PartVisual.RedrawOnNewPixelScale(World, ref _partsPixelScale);
        WatchSlowMotion();
        if (_followed is null)
        {
            return;
        }

        if (_selectedPartName is not null && _selection.SelectedElement is { } selected)
        {
            _screen.ShowPartName(_selectedPartName(), _followed.PartAnchor(selected), _followed.Bounds);
        }

        _signalRefreshElapsed += delta;
        if (_signalRefreshElapsed < _signalRefreshIntervalSeconds)
        {
            return;
        }

        _signalRefreshElapsed = 0;
        _followed.ReadInputs(_brainInputs);
        _signalFlow.Update(_brainInputs.Count, _followed.Brain is null ? 0 : _followed.MotorCount, FollowedDistance);
        _brainFocus.Update(_followed.Brain, _brainInputs);
    }

    // Counts the physics ticks run against real time; a pause stops physics on purpose, so it starts
    // the count over and leaves the chip as it is.
    private void WatchSlowMotion()
    {
        if (_slowMotion is null)
        {
            return;
        }

        var physicsFrames = Engine.GetPhysicsFrames();
        var usec = Time.GetTicksUsec();
        var ticks = (long)(physicsFrames - _watchedPhysicsFrames);
        var seconds = (usec - _watchedUsec) / 1_000_000.0;
        var first = _watchedUsec == 0;
        _watchedPhysicsFrames = physicsFrames;
        _watchedUsec = usec;
        if (first || GetTree().Paused)
        {
            _slowMotion.Restart();
            return;
        }

        _screen.ShowSlowMotion(_slowMotion.Advance(seconds, ticks));
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
        else if (_route.Mode == TrainingRunMode.Simulate && creation.Training is null)
        {
            // Train setup offers Simulate only for a trained creation; its training may be reset since.
            GD.PrintErr($"Creation {_route.CreationId} has no brain to simulate.");
            Notify("Creations", "Train the creation before simulating it.");
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
        Shadows.Theme = _theme;
        Ground.ZIndex = ArenaLayers.Ground;
        // The markers are children of the ground, so their layers are made absolute, not added to the ground's.
        BestMarker.ZAsRelative = false;
        BestMarker.ZIndex = ArenaLayers.BestMarker;
        StartSign.Theme = _theme;
        StartSign.ZAsRelative = false;
        StartSign.ZIndex = ArenaLayers.StartSign;
        // The shadows' picture stands in for every shadow, so it takes their layer, under the followed
        // creature. Simulate runs no shadows, so its viewport is off there.
        Shadows.ZAsRelative = false;
        Shadows.ZIndex = ArenaLayers.Shadows;
        Shadows.Visible = Mode == TrainingRunMode.Train;
        // Every creature is under the world, so it must be on both arena views for either to draw one.
        World.VisibilityLayer = ArenaVisibility.Both;
        Ground.Build(_map.Ground, _theme);
    }

    private void AddCreature(CreationDef? creation)
    {
        var creature = CreateCreatureInstance();
        creature.Name = "Creature";
        // Pausable, not Inherit, so it stops simulating while the tree is paused.
        creature.ProcessMode = ProcessModeEnum.Pausable;
        creature.Definition = creation?.Creature ?? CreationExamples.Walker.Creature;
        creature.Theme = _theme;
        creature.Position = GetNode<Marker2D>("%Spawn").Position;
        World.AddChild(creature);
        _creature = creature;
        _followed = creature;
    }

    // The camera follows the followed shadow's centre and glides to the new one when following
    // changes (#668); it zooms to fit that shadow's box and keeps the ground in place (#675). The
    // ruler counts from where the creature's front-most point starts, the point every shown
    // distance is measured from (#725): every trial resets every shadow to the same pose, so it
    // starts there every time.
    private void FollowCreature()
    {
        if (_creature is not { } creature)
        {
            return;
        }

        Ruler.StartX = Ruler.ToLocal(creature.Bounds.End).X;
        BestMarker.StartX = Ruler.StartX;
        StartSign.StartX = Ruler.StartX;
        Camera.GroundY = GroundTopY;

        // Read every physics tick; OnFollowedShadowChanged retargets it when the followed shadow changes.
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
            creation is null
                ? TrainingHeaderPresentation.ForWalker(Mode, _map.Id)
                : TrainingHeaderPresentation.For(creation.Name, Mode, _map.Id),
            _trainingPresentation,
            _signalFlow,
            _brainFocus);
        _screen.ShowPaused(GetTree().Paused);
        _screen.BackRequested += () => _navigator?.Back();
        _screen.PauseRequested += TogglePause;
        _screen.StatsRequested += () => UiNotificationLayer.Enqueue(this, new UiNotificationSpec(
            UiPopupType.Default,
            "Stats",
            "Stats come in a later version.",
            Icon: new(UiIconId.Chart))
        {
            Id = "training.stats",
            Replaceable = true,
        });
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
        if (_creature?.Definition is not { } definition || _evolver is null)
        {
            return;
        }

        var resume = creation?.Training;
        _saved = resume;
        var resumeBest = resume?.BestOn(_map.Id);
        var disabledGenes = resume is null ? null : DirectBrain.DisabledGenes(resume.Brain, _creature.Ports);
        _brainFocus.Configure(BrainPortLabels.For(definition), disabledGenes ?? []);
        var setup = EvolutionSetup.For(creation?.TrainSettings, Engine.PhysicsTicksPerSecond);
        _evolver.Start(
            _creature,
            setup.Population,
            DirectBrain.LayerSizes(_creature.Ports),
            setup.Algorithm,
            Rng.Random,
            GroundTopY,
            resume is null ? null : DirectBrain.Compile(resume.Brain, _creature.Ports),
            resume?.Generation ?? 0,
            resumeBestFitness: resumeBest?.Distance ?? double.NegativeInfinity,
            resumeBestShownDistance: resumeBest?.FrontDistance ?? double.NaN,
            resumeBestGeneration: resumeBest?.Generation ?? 0,
            trialDurationTicks: setup.TrialTicks,
            creatureFactory: CreateCreatureInstance,
            disabledGenes: disabledGenes);
    }

    // Simulate (#702) plays the saved brain on the creature in one run that lasts until the player
    // leaves: no Evolver, so no generation, nothing saved and nothing counted. The run measures how
    // far its front has got, for the Distance card.
    private void StartPlayback(CreationDef creation)
    {
        if (_creature?.Definition is not { } definition || creation.Training is not { } training)
        {
            return;
        }

        _trainingPresentation.Dispose();
        _trainingPresentation = TrainingPresentationViewModel.Saved(training, _map.Id);
        _brainFocus.Configure(BrainPortLabels.For(definition), DirectBrain.DisabledGenes(training.Brain, _creature.Ports));
        _creature.SetBrain(DirectBrain.Network(training.Brain, _creature.Ports));
        var playback = new TrialController
        {
            Name = "Playback",
            // Pausable, not Inherit, so the run stops while the tree is paused.
            ProcessMode = ProcessModeEnum.Pausable,
            TrialDurationTicks = int.MaxValue,
            GroundTopY = GroundTopY,
        };
        World.AddChild(playback);
        playback.StartTrial(_creature);
        _playback = playback;
    }

    private double FollowedDistance =>
        _evolver?.FollowedTrialDistance ?? (_playback is { IsRunning: true } playback ? playback.Measured.FrontDistance : double.NaN);

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

    // The best marker moves only when a generation's front goes past it; Show ignores the rest.
    private void OnTrainingChanged(object? sender, PropertyChangedEventArgs eventArgs) => ShowBest();

    private void ShowBest() => BestMarker.Show(_trainingPresentation.BestShownDistance, UiTextTranslation.Source(_trainingPresentation.BestMarkerText));

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
            || _creature is null)
        {
            return;
        }

        var saves = Saves;
        var epoch = saves.CurrentTrainingEpoch(id);
        var brain = DirectBrain.ToBrainDef(_creature.Ports, genome, _saved?.Brain);
        var latest = new TrainingRunDef(run.Distance, run.TopSpeed, run.Elevation, _map.Id, run.FrontDistance);
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
                ? UiTextTranslation.Source(PartNames.Display(definition.Nodes, definition.Beams, definition.Sensors, definition.Servos, definition.Pistons, definition.Springs, definition.Wheels, selected.Id))
                : null;
            BestMarker.Faded = _selectedPartName is not null;
            StartSign.Faded = BestMarker.Faded;
            if (_selectedPartName is null)
            {
                _screen.ShowPartName(null, default, default);
            }
        }
    }
}
