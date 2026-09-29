using System.ComponentModel;
using Godot;
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
using NodeRunner.Ui.Widgets;

namespace NodeRunner;

/// <summary>
/// The Simulate scene: trains one saved creation (#469). It owns the physics world and the
/// <see cref="Evolver"/>, resumes from the creation's last finished generation and saves every
/// finished generation, so leaving drops only the one in progress. Run on its own (F6) it trains the
/// built-in worm without saving.
/// </summary>
public partial class SimulateHost : Node2D, IRoutedScene
{
    private const double _extraCoreUnlockFitness = 50;
    private const double _signalRefreshIntervalSeconds = 0.15;
    private static readonly Vector2 _groundSize = new(900, 48);

    // Engine.TimeScale speeds every physics step up uniformly, so it changes how fast a fixed number
    // of ticks plays out, never the result.
    private static readonly float[] _timeScales = [1f, 2f, 4f];
    private static readonly TrainingProfile[] _trainingProfiles =
    [
        new("Quick", 4, 180, 30, 0.2, 0.35, 2, CrossoverStrategy.Uniform),
        new("Standard", 8, 600, 50, 0.1, 0.3, 3, CrossoverStrategy.Uniform),
        new("Deep", 16, 1200, 100, 0.06, 0.2, 3, CrossoverStrategy.Blend),
    ];

    private readonly VisualTheme _theme = VisualTheme.Neon;
    private readonly SelectionViewModel _selection = new();
    private readonly SignalFlowPresentationViewModel _signalFlow = new();
    private readonly BrainFocusPresentationViewModel _brainFocus = new();
    private readonly UnlockProgressPresentationViewModel _unlockProgress = new();
    private readonly TrainingProfileSummaryPresentationViewModel _profileSummary = new();
    private readonly TrainingProfileSettingsPresentationViewModel _profileSettings = new();
    private readonly List<SensorReading> _sensorReadings = [];
    private readonly List<MotorReading> _motorReadings = [];
    private SimulateRoute? _route;
    private ISceneNavigator? _navigator;
    private Guid? _creationId;
    private Creature.Creature? _creature;
    private Evolver? _evolver;
    private StaticBody2D? _ground;
    private SimulateScreen? _screen;
    private TrainingPresentationViewModel _trainingPresentation = new();
    private CanvasLayer? _brainFocusLayer;
    private Button? _brainFocusDismiss;
    private UiSheet? _brainFocusSheet;
    private Label? _brainFocusSummaryLabel;
    private Label? _brainFocusSelectedLabel;
    private int _trainingProfileIndex = 1;
    private int _sessionGenerationStart;
    private int _timeScaleIndex;
    private double _signalRefreshElapsed;

    private SaveManager Saves => GetNode<SaveManager>("/root/SaveManager");

    private RngProvider Rng => GetNode<RngProvider>("/root/RngProvider");

    private TrainingProfile CurrentTrainingProfile => _trainingProfiles[_trainingProfileIndex];

    // Every creation trains on flat ground until maps land (#443).
    private float GroundTopY => (_ground?.GlobalPosition.Y ?? 0) - (_groundSize.Y / 2);

    public void Enter(SceneRoute route, ISceneNavigator navigator)
    {
        _route = (SimulateRoute)route;
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
        _brainFocus.PropertyChanged += OnBrainFocusChanged;

        var creation = LoadRouteCreation();
        AddBackdrop();
        AddGround();
        AddCreature(creation);
        AddScreen();
        AddBrainFocusOverlay();
        AddEvolver();
        AddBackHandler();
        if (creation is not null)
        {
            StartEvolution(creation);
        }
        else if (_route is null)
        {
            StartEvolution();
        }
    }

    public override void _ExitTree()
    {
        // Don't let this scene's speed or pause leak into the next one.
        Engine.TimeScale = _timeScales[0];
        GetTree().Paused = false;
        _selection.PropertyChanged -= OnSelectionPropertyChanged;
        _brainFocus.PropertyChanged -= OnBrainFocusChanged;
        _trainingPresentation.PropertyChanged -= OnTrainingPresentationChanged;
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
        _signalFlow.Update(_sensorReadings, _motorReadings, _trainingPresentation.BestFitness, _trainingPresentation.MeanFitness);
        _brainFocus.Update(_creature.Brain, _sensorReadings, _motorReadings);
    }

    // Taps that reach the world select a part of the creature, or clear the selection.
    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (!PointerInput.TryGetPressPosition(inputEvent, out var screenPosition))
        {
            return;
        }

        var worldPosition = GetGlobalTransformWithCanvas().AffineInverse() * screenPosition;
        if (_creature is not null && _creature.TrySelectPart(worldPosition, out var selection) && selection is not null)
        {
            _selection.Select(selection);
        }
        else
        {
            _selection.Clear();
        }

        GetViewport().SetInputAsHandled();
    }

    private CreationDef? LoadRouteCreation()
    {
        if (_route is null)
        {
            return null;
        }

        if (Saves.Get(_route.CreationId) is { } creation)
        {
            _creationId = creation.Id;
            return creation;
        }

        GD.PrintErr($"Creation {_route.CreationId} was not found.");
        Notify("Creations", "That creation could not be found.");
        Callable.From(ReturnToCreations).CallDeferred();
        return null;
    }

    private void ReturnToCreations() => _navigator?.ReturnToRoot();

    private void Notify(string title, string message) =>
        UiNotificationLayer.Enqueue(this, new UiNotificationSpec(UiPopupType.Default, title, message));

    // Android Back and Escape: the brain focus sheet or the settings sheet closes first, then Back
    // returns to the previous scene. Training is already saved up to the last finished generation.
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
        if (_brainFocusLayer?.Visible == true)
        {
            HideBrainFocus();
        }
        else if (_screen?.CloseOverlay() != true)
        {
            _navigator?.Back();
        }
    }

    private void AddBackdrop()
    {
        AddChild(new ArenaBackdrop
        {
            Name = "ArenaBackdrop",
            Theme = _theme,
            Position = new Vector2(-450, 0),
            Size = new Vector2(1800, 720),
            ZIndex = -100,
        });
    }

    private void AddGround()
    {
        var ground = new StaticBody2D
        {
            Name = "Ground",
            Position = new Vector2(360, 420),
        };

        ground.AddChild(new CollisionShape2D
        {
            Shape = new RectangleShape2D { Size = _groundSize },
        });

        ground.AddChild(new Polygon2D
        {
            Color = _theme.GroundFill,
            Polygon =
            [
                new Vector2(-450, -24),
                new Vector2(450, -24),
                new Vector2(450, 24),
                new Vector2(-450, 24),
            ],
        });

        ground.AddChild(new Line2D
        {
            Points =
            [
                new Vector2(-450, -24),
                new Vector2(450, -24),
            ],
            DefaultColor = _theme.GroundEdge,
            Width = _theme.GroundEdgeWidth,
        });

        AddChild(ground);
        _ground = ground;
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
        creature.Position = new Vector2(250, 260);
        AddChild(creature);
        _creature = creature;
    }

    private static Creature.Creature CreateCreatureInstance() =>
        GD.Load<PackedScene>("res://scenes/Creature.tscn").Instantiate<Creature.Creature>();

    private void AddScreen()
    {
        var layer = new CanvasLayer
        {
            Name = "SimulateOverlay",
            Layer = 2,
            ProcessMode = ProcessModeEnum.Always,
        };
        AddChild(layer);

        _screen = new SimulateScreen
        {
            Name = "LiveSimulateScreen",
            Hosted = true,
            ShowArenaPlaceholder = false,
            ReadOnlyControls = true,
            InputPassthrough = true,
            Presentation = _trainingPresentation,
            SignalFlow = _signalFlow,
            UnlockProgress = _unlockProgress,
            ProfileSummary = _profileSummary,
            ProfileSettings = _profileSettings,
            PauseActionText = PauseButtonText(),
        };
        _screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _screen.BrainFocusRequested += ShowBrainFocus;
        _screen.CreationsRequested += ReturnToCreations;
        _screen.BackRequested += () => _navigator?.Back();
        _screen.PauseRequested += TogglePause;
        _screen.SpeedRequested += CycleTimeScale;
        _screen.ResetRequested += ResetEvolution;
        _screen.TrainingProfileRequested += CycleTrainingProfile;
        _screen.TrainingProfileSelected += SelectTrainingProfile;
        layer.AddChild(_screen);
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
        _trainingPresentation = new TrainingPresentationViewModel(new EvolverTrainingProgressSource(
            evolver,
            () => CurrentTrainingProfile.Name));
        _trainingPresentation.PropertyChanged += OnTrainingPresentationChanged;
        if (_screen is not null)
        {
            _screen.Presentation = _trainingPresentation;
        }

        RefreshTrainingProfileSummary();
        RefreshTrainingProfileSettings();
        RefreshUnlockProgress();
        AddChild(evolver);
        _evolver = evolver;
    }

    // A fresh population, from generation 0.
    private void StartEvolution()
    {
        _evolver?.Stop();
        if (_creature?.Brain is null || _evolver is null)
        {
            // No motors: nothing to evolve.
            return;
        }

        var profile = CurrentTrainingProfile;
        _sessionGenerationStart = 0;
        _evolver.Start(
            _creature,
            profile.PopulationSize,
            _creature.Brain.LayerSizes,
            CreateGeneticAlgorithm(profile),
            Rng.Random,
            GroundTopY,
            trialDurationTicks: profile.TrialDurationTicks,
            creatureFactory: CreateCreatureInstance);
    }

    // Resumes from the creation's saved best genome and generation, or starts fresh without one.
    private void StartEvolution(CreationDef creation)
    {
        _evolver?.Stop();
        if (_creature?.Brain is null || _evolver is null)
        {
            return;
        }

        var profile = CurrentTrainingProfile;
        var resume = creation.Training;
        _sessionGenerationStart = resume?.Generation ?? 0;
        _evolver.Start(
            _creature,
            profile.PopulationSize,
            _creature.Brain.LayerSizes,
            CreateGeneticAlgorithm(profile),
            Rng.Random,
            GroundTopY,
            resume?.BestGenome,
            resume?.Generation ?? 0,
            profile.TrialDurationTicks,
            CreateCreatureInstance);
    }

    private static GeneticAlgorithm CreateGeneticAlgorithm(TrainingProfile profile) =>
        new(profile.TournamentSize, profile.MutationRate, profile.MutationStrength, crossoverStrategy: profile.CrossoverStrategy);

    private void OnGenerationCompleted()
    {
        GD.Print($"Generation {_evolver!.Generation} — best: {_evolver.BestFitness:0.0}, mean: {_evolver.MeanFitness:0.0}");
        PersistTraining();
        RefreshUnlockProgress();
        if (_evolver.Generation - _sessionGenerationStart >= CurrentTrainingProfile.MaxGenerations)
        {
            _evolver.Stop();
            GD.Print($"Training session complete after {CurrentTrainingProfile.MaxGenerations} generations.");
        }
    }

    private void OnTrainingPresentationChanged(object? sender, PropertyChangedEventArgs args) => RefreshUnlockProgress();

    private void RefreshUnlockProgress() =>
        _unlockProgress.Update(Saves.Progression, _trainingPresentation.BestFitness, _extraCoreUnlockFitness);

    private void TryUnlockProgression()
    {
        if (_evolver is null || _evolver.BestFitness < _extraCoreUnlockFitness)
        {
            return;
        }

        var attributionId = _creationId is { } id && Saves.Get(id) is not null ? id : (Guid?)null;
        if (Saves.UnlockExtraCore(_evolver.Generation, attributionId))
        {
            RefreshUnlockProgress();
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
        if (_screen is not null)
        {
            _screen.PauseActionText = PauseButtonText();
        }
    }

    private string PauseButtonText() => GetTree().Paused ? "Run" : "Pause";

    private void CycleTimeScale()
    {
        _timeScaleIndex = (_timeScaleIndex + 1) % _timeScales.Length;
        Engine.TimeScale = _timeScales[_timeScaleIndex];
    }

    // Resets the saved training, reseeds and starts again from a fresh random population.
    private void ResetEvolution()
    {
        // If the reset can't be saved (#114), keep the live training too: a reset that worked on
        // screen but not on disk would silently come back on the next visit.
        if (_creationId is { } id && !CreationActions.TryRunFileOperation(
            () => Saves.ResetTraining(id),
            $"Resetting training for Creation {id}"))
        {
            return;
        }

        Rng.Reseed(Random.Shared.Next(int.MinValue, int.MaxValue));
        StartEvolution();
    }

    private void CycleTrainingProfile() => ApplyTrainingProfileIndex((_trainingProfileIndex + 1) % _trainingProfiles.Length);

    private void SelectTrainingProfile(int index)
    {
        if (index >= 0 && index < _trainingProfiles.Length && index != _trainingProfileIndex)
        {
            ApplyTrainingProfileIndex(index);
        }
    }

    // A new profile restarts the session from the training so far. The save runs in the background,
    // so the restart merges the live snapshot instead of reading it back off disk.
    private void ApplyTrainingProfileIndex(int index)
    {
        _trainingProfileIndex = index;
        RefreshTrainingProfileSummary();
        RefreshTrainingProfileSettings();
        PersistTraining();
        if (_creationId is { } id && Saves.Get(id) is { } creation)
        {
            StartEvolution(WithLiveTraining(creation));
            return;
        }

        StartEvolution();
    }

    private CreationDef WithLiveTraining(CreationDef creation)
    {
        if (_evolver?.BestGenome is not { } genome || _creature?.Brain is null)
        {
            return creation;
        }

        return new CreationDef(
            creation.Id,
            creation.Name,
            creation.Creature,
            creation.BrainShape,
            new TrainingStateDef(
                _evolver.LayerSizes,
                genome.ToArray(),
                _evolver.Generation,
                Activation.Tanh.ToString(),
                _evolver.BestFitness,
                BestRunOf(_evolver) ?? creation.Training?.BestRun));
    }

    private void RefreshTrainingProfileSummary()
    {
        var profile = CurrentTrainingProfile;
        _profileSummary.Update(profile.PopulationSize, profile.TrialDurationTicks / 60, profile.MutationRate, CrossoverText(profile));
    }

    private void RefreshTrainingProfileSettings()
    {
        _profileSettings.Update(_trainingProfiles.Select(profile => new TrainingProfileOptionPresentation(
            profile.Name,
            TargetSummaryText(profile))), _trainingProfileIndex);
    }

    private static string CrossoverText(TrainingProfile profile) =>
        profile.CrossoverStrategy == CrossoverStrategy.Blend ? "blended genes" : "uniform genes";

    private static string TargetSummaryText(TrainingProfile profile)
    {
        var mutation = (profile.MutationRate * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        return $"{profile.PopulationSize} candidates · {profile.TrialDurationTicks / 60}s trials · {mutation}% mutation · {CrossoverText(profile)}";
    }

    private void OnSelectionPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(SelectionViewModel.SelectedElement))
        {
            _creature?.SetSelectedElement(_selection.SelectedElement);
        }
    }

    private void AddBrainFocusOverlay()
    {
        _brainFocusLayer = new CanvasLayer
        {
            Name = "BrainFocusOverlay",
            Layer = 5,
            ProcessMode = ProcessModeEnum.Always,
            Visible = false,
        };
        AddChild(_brainFocusLayer);

        _brainFocusDismiss = new Button
        {
            Name = "BrainFocusDismiss",
            Flat = true,
            Text = string.Empty,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _brainFocusDismiss.Pressed += HideBrainFocus;
        _brainFocusLayer.AddChild(_brainFocusDismiss);

        _brainFocusSheet = new UiSheet
        {
            Name = "BrainFocusSheet",
            Title = "BrainFocus · Decides",
            CustomMinimumSize = new Vector2(540, 0),
        };
        _brainFocusLayer.AddChild(_brainFocusSheet);
    }

    private void ShowBrainFocus()
    {
        if (_brainFocusLayer is null || _brainFocusSheet is null)
        {
            return;
        }

        LayoutBrainFocusOverlay();
        _brainFocusSheet.SetBody(CreateBrainFocusBody());
        UpdateBrainFocusLabels();
        _brainFocusLayer.Show();
    }

    private void HideBrainFocus() => _brainFocusLayer?.Hide();

    private Control CreateBrainFocusBody()
    {
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 10);

        _brainFocusSummaryLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        stack.AddChild(_brainFocusSummaryLabel);

        stack.AddChild(new BrainFocusNetworkView
        {
            ViewModel = _brainFocus,
            CustomMinimumSize = new Vector2(500, 220),
            MouseFilter = Control.MouseFilterEnum.Stop,
        });

        _brainFocusSelectedLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        stack.AddChild(_brainFocusSelectedLabel);

        var close = new UiButton
        {
            Kind = UiButtonKind.Primary,
            Text = "Back to SignalFlow",
        };
        close.Pressed += HideBrainFocus;
        stack.AddChild(close);

        return stack;
    }

    private void OnBrainFocusChanged(object? sender, PropertyChangedEventArgs args) => UpdateBrainFocusLabels();

    private void UpdateBrainFocusLabels()
    {
        if (_brainFocusSummaryLabel is not null)
        {
            _brainFocusSummaryLabel.Text = _brainFocus.HasNetwork
                ? $"{_brainFocus.Summary}. Circles/solid cyan are positive; diamonds/dashed red are negative; stronger signals draw brighter/thicker."
                : _brainFocus.Summary;
        }

        if (_brainFocusSelectedLabel is not null)
        {
            _brainFocusSelectedLabel.Text = $"{_brainFocus.SelectedNeuronLabel}: {_brainFocus.SelectedNeuronSummary}";
        }
    }

    private void LayoutBrainFocusOverlay()
    {
        var viewportSize = GetViewportRect().Size;
        if (_brainFocusDismiss is not null)
        {
            _brainFocusDismiss.Position = Vector2.Zero;
            _brainFocusDismiss.Size = viewportSize;
        }

        if (_brainFocusSheet is not null)
        {
            _brainFocusSheet.Position = new Vector2(
                Mathf.Max(24, (viewportSize.X - _brainFocusSheet.CustomMinimumSize.X) / 2),
                72);
        }
    }
}
