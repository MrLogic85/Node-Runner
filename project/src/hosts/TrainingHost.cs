using System.ComponentModel;
using Godot;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.Navigation;
using NodeRunner.App.Services;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Managers;
using NodeRunner.ML.Networks;
using NodeRunner.Sim;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Screens;
using NodeRunner.Ui.Widgets;

namespace NodeRunner.Hosts;

/// <summary>
/// The Training scene: trains one saved creation on the Training screen, or simulates
/// it. The scene authors the screen and the world in its arena: camera, background, ground
/// line and ruler. This root builds the map's ground along that line, adds the creature to that
/// world and points the camera and the ruler at it. To train, it adds the <see cref="Evolver"/>,
/// resumes from the creation's last finished generation and saves every finished generation, so
/// leaving drops only the one in progress. To simulate, it plays the saved network on the creature in
/// one run that lasts until the player leaves, and saves nothing. Run on its own (F6) it trains the
/// Walker example without saving.
/// </summary>
public partial class TrainingHost : Node, IRoutedScene
{
    private const double _signalRefreshIntervalSeconds = 1.0 / 30;


    private readonly VisualTheme _theme = VisualTheme.Neon;
    // The map Training runs on and records: the one Train setup selects.
    private readonly MapDef _map = Maps.Default;
    private readonly SelectionViewModel _selection = new();
    private readonly SignalFlowPresentationViewModel _signalFlow = new();
    private readonly NetworkFocusPresentationViewModel _networkFocus = new();
    private readonly List<double> _networkInputs = [];
    private TrainingRoute? _route;
    private ISceneNavigator? _navigator;
    private Guid? _creationId;
    // The network last saved, so the next save keeps its neuron ids and disabled genes.
    private TrainingStateDef? _saved;
    private Creature.Creature? _creature;
    // The shadow drawn in full, whose network and parts the screen shows.
    private Creature.Creature? _followed;
    private Evolver? _evolver;
    // Simulate's one endless run; null while training.
    private TrialController? _playback;
    private TrainingScreen _screen = null!;
    private TrainingPresentationViewModel _trainingPresentation = new();
    // The last network sample, taken once per signal refresh so every view of the network agrees.
    private NetworkPortValues _portValues = NetworkPortValues.Empty;
    private double _signalRefreshElapsed;
    // Training's slow-motion chip; null in Simulate, which races one shadow.
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
    private ArenaFlagMarker BestMarker => GetNode<ArenaFlagMarker>("%BestMarker");

    private ArenaFlagMarker CrashMarker => GetNode<ArenaFlagMarker>("%CrashMarker");

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
        // Always, so tapping a part and the screen's buttons still work while paused; the
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
    // while paused (a resize re-frames the camera). Moves the part callout's leader with the part
    // every frame and hands the camera the shelf the callout needs. Refreshes the signal flow, network focus and part callout from the creature's last
    // physics tick at a fixed cadence: the numbers are for a person to read, so every rendered
    // frame is wasted work.
    public override void _Process(double delta)
    {
        PartVisual.RedrawOnNewPixelScale(World, ref _partsPixelScale);
        WatchSlowMotion();
        if (_followed is null)
        {
            return;
        }

        // A stacked joint's parts share their joint, so the first one's anchor serves them all.
        if (_selection.SelectedParts is [var selected, ..])
        {
            _screen.MovePartCallout(_followed.PartAnchor(selected));
        }

        Camera.ScaleBand = Ruler.BandShare;
        Camera.Shelf = _screen.PartCalloutShelf;

        _signalRefreshElapsed += delta;
        if (_signalRefreshElapsed < _signalRefreshIntervalSeconds)
        {
            return;
        }

        // Keep the remainder: zeroing it would make a refresh wait an extra frame whenever two
        // smoothed frames fall a microsecond short of the interval, about 20 a second instead of 30.
        _signalRefreshElapsed %= _signalRefreshIntervalSeconds;
        _followed.ReadInputs(_networkInputs);
        _signalFlow.Update(FollowedDistance, FollowedSpeed, _trainingPresentation.BestShownDistance, FollowedCrashed);
        ShowCrash();

        _portValues = NetworkPortValues.Sample(_followed.Ports, _followed.Network, _networkInputs);
        _signalFlow.ShowPorts(_followed.Definition, _portValues);
        _networkFocus.Update(_portValues);
        ShowPartCallout();
    }

    // The selected parts' callout from the last sample; none with nothing selected.
    private void ShowPartCallout() =>
        _screen.ShowPartCallout(_followed?.Definition is { } definition
            ? [.. _selection.SelectedParts.Select(part => PartCalloutPresentation.For(definition, part, _portValues))]
            : []);

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
            // A saved drawing may be unfinished; Build stops it before training.
            GD.PrintErr($"Creation {_route.CreationId} cannot train yet.");
            Notify("Creations", "Finish the creation in Build before training it.");
        }
        else if (_route.Mode == TrainingRunMode.Simulate && creation.Training is null)
        {
            // Train setup offers Simulate only for a trained creation; its training may be reset since.
            GD.PrintErr($"Creation {_route.CreationId} has no network to simulate.");
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

    // Android Back and Escape: the network sheet closes first, then Back returns to the previous
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
        CrashMarker.Theme = _theme;
        CrashMarker.ZAsRelative = false;
        CrashMarker.ZIndex = ArenaLayers.BestMarker;
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
    // changes; it zooms to fit that shadow's box and keeps the ground in place. The
    // ruler counts from where the creature's front-most point starts, the point every shown
    // distance is measured from: every trial resets every shadow to the same pose, so it
    // starts there every time.
    private void FollowCreature()
    {
        if (_creature is not { } creature)
        {
            return;
        }

        Ruler.StartX = Ruler.ToLocal(creature.Bounds.End).X;
        BestMarker.StartX = Ruler.StartX;
        CrashMarker.StartX = Ruler.StartX;
        StartSign.StartX = Ruler.StartX;
        Camera.GroundY = GroundTopY;

        // Read every physics tick; OnFollowedShadowChanged retargets it when the followed shadow changes.
        Camera.Follow(() => Framed(_followed ?? creature));
    }

    private static FramedCreature Framed(Creature.Creature creature)
    {
        var bounds = creature.Bounds;
        return new FramedCreature(
            creature.Centre.X,
            creature.Centre.Y,
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
            _networkFocus);
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

    // Evolves a population of networks in parallel physics slots, one generation after another.
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

    // Resumes from the creation's saved latest network and generation, or starts at generation 0
    // without one.
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
        var disabledGenes = resume is null ? null : DirectNetwork.DisabledGenes(resume.Network, _creature.Ports);
        _networkFocus.Configure(NetworkPortLabels.For(definition), disabledGenes ?? []);
        var setup = EvolutionSetup.For(creation?.TrainSettings, Engine.PhysicsTicksPerSecond);
        _evolver.Start(
            _creature,
            setup.Population,
            DirectNetwork.LayerSizes(_creature.Ports),
            setup.Algorithm,
            Rng.Random,
            GroundTopY,
            resume is null ? null : DirectNetwork.Compile(resume.Network, _creature.Ports),
            resume?.Generation ?? 0,
            resumeBestFitness: resumeBest?.Distance ?? double.NegativeInfinity,
            resumeBestShownDistance: resumeBest?.FrontDistance ?? double.NaN,
            resumeBestGeneration: resumeBest?.Generation ?? 0,
            trialDurationTicks: setup.TrialTicks,
            creatureFactory: CreateCreatureInstance,
            disabledGenes: disabledGenes);
    }

    // Simulate plays the saved network on the creature in one run that lasts until the player
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
        _networkFocus.Configure(NetworkPortLabels.For(definition), DirectNetwork.DisabledGenes(training.Network, _creature.Ports));
        _creature.SetNetwork(DirectNetwork.Build(training.Network, _creature.Ports));
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

    private double FollowedSpeed =>
        _evolver?.FollowedTrialSpeed ?? (_playback is { IsRunning: true } playback ? playback.CurrentSpeed : double.NaN);

    // Whether the followed run's distance froze at a tear.
    private bool FollowedCrashed => _evolver?.FollowedTrialCrashed ?? _playback is { IsRunning: true, IsFrozen: true };

    // The crash flag stands where the followed run's distance froze, one row under the best
    // flag.
    private void ShowCrash()
    {
        var distance = FollowedDistance;
        var crashed = FollowedCrashed && double.IsFinite(distance);
        CrashMarker.Show(distance, crashed ? UiTextTranslation.Source(TrainingPresentationViewModel.CrashText(distance)) : null);
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

        _followed?.SetSelectedParts([]);
        _followed = followed;
        _followed?.SetSelectedParts(_selection.SelectedParts);
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

    // Saving reads the creation back off disk and writes it again, synchronous file IO that
    // would stall physics at a generation boundary, so it runs on the thread pool. Only plain values
    // cross to that thread, and nothing comes back to this scene, which may be gone by then. Build
    // reads the creation only once the queued saves have landed.
    private void PersistTraining()
    {
        // A generation without a valid trial has no latest network, so it isn't saved.
        if (_creationId is not { } id
            || _evolver?.LatestGenome is not { } genome
            || _evolver.LatestRun is not { } run
            || _creature is null)
        {
            return;
        }

        var saves = Saves;
        var epoch = saves.CurrentTrainingEpoch(id);
        var network = DirectNetwork.ToNeuralNetworkDef(_creature.Ports, genome, _saved?.Network);
        var latest = new TrainingRunDef(run.Distance, run.TopSpeed, run.Elevation, _map.Id, run.FrontDistance);
        var training = TrainingStateDef.Record(_saved, network, _evolver.Generation, latest);
        _saved = training;
        saves.PersistTrainingInBackground(id, epoch, training).ContinueWith(
            failed => ReportFailedSave(id, training.Generation, failed.Exception!),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private static void ReportFailedSave(Guid id, int generation, Exception exception)
    {
        // A data-loss condition: keep the whole exception and fail loud (docs/CODE_DESIGN_PRINCIPLES.md
        // → "Fail loud in dev, gracefully in prod"). Logged from the main thread, even after this scene is gone.
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

    // A tap in the arena selects the part of the creature under it, a stacked joint's all,
    // or clears the selection.
    private void SelectPartAt(Vector2 worldPosition)
    {
        if (_followed?.Definition is { } definition && _followed.TrySelectPart(worldPosition, out var selection) && selection is not null)
        {
            _selection.Select(definition, selection);
        }
        else
        {
            _selection.Clear();
        }
    }

    private void OnSelectionPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(SelectionViewModel.SelectedParts))
        {
            _followed?.SetSelectedParts(_selection.SelectedParts);
            BestMarker.Faded = _selection.SelectedParts.Count > 0 && _followed?.Definition is not null;
            StartSign.Faded = BestMarker.Faded;
            CrashMarker.Faded = BestMarker.Faded;
            ShowPartCallout();
        }
    }
}
