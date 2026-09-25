using System.ComponentModel;
using Godot;
using NodeRunner.App.Repositories;
using NodeRunner.App.Services;
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

public partial class Main : Node2D
{
    private const double _extraCoreUnlockFitness = 50;
    private readonly VisualTheme _theme = VisualTheme.Neon;
    private Creature.Creature? _creature;
    private Evolver? _evolver;
    private CreatureInspectorViewModel? _inspector;
    private ConstructionCanvas? _constructionCanvas;
    private ArenaBackdrop? _arenaBackdrop;
    private ColorRect? _buildModeBackdrop;
    private Button? _buildModeButton;
    private PanelContainer? _hudPanel;
    private PanelContainer? _toolPanel;
    private Button? _placeToolButton;
    private Button? _beamToolButton;
    private Button? _coreToolButton;
    private Button? _deleteToolButton;
    private Label? _editLockReasonLabel;
    private Button? _completeButton;
    private Button? _rebuildButton;
    private ConfirmationDialog? _rebuildConfirmationDialog;
    private PanelContainer? _buildInfoPanel;
    private Label? _buildInputSummaryLabel;
    private Label? _buildMotorSummaryLabel;
    private Label? _buildValidationLabel;
    private BuildScreen? _buildScreen;
    private Button? _creationsButton;
    private PanelContainer? _creationsPanel;
    private VBoxContainer? _creationsList;
    private CreationsScreen? _creationsScreen;
    private CanvasLayer? _componentGalleryLayer;
    private Control? _componentGalleryHost;
    private ComponentGalleryScreen? _componentGalleryScreen;
    private CanvasLayer? _colorsAndStylesLayer;
    private Control? _colorsAndStylesHost;
    private ColorsAndStylesScreen? _colorsAndStylesScreen;
    private ConfirmationDialog? _deleteCreationConfirmationDialog;
    private UiToast? _deleteCreationToast;
    private CreationDef? _lastDeletedCreation;
    private DuplicateCreationSheet? _duplicateCreationSheet;
    private Guid? _pendingDuplicateCreationId;
    private string? _pendingDuplicateCreationName;
    private Guid? _pendingDeleteCreationId;
    private string? _pendingDeleteCreationName;
    private bool _evolutionDeferredByHomeHub;
    private SimulateScreen? _simulateScreen;
    private Guid? _activeCreationId;
    private Label? _seedLabel;
    private Label? _generationLabel;
    private Label? _bestFitnessLabel;
    private Label? _meanFitnessLabel;
    private Button? _pauseButton;
    private Button? _timeScaleButton;
    private Button? _trainingProfileButton;
    private Label? _trainingProfileSummaryLabel;
    private Label? _trainingSaveStatusLabel;
    private long _trainingSaveStatusVersion;
    private Label? _progressionLabel;
    private PanelContainer? _trainingPanel;
    private GenerationStrip? _generationStrip;
    private int _timeScaleIndex;
    private Label? _inspectorTitle;
    private Label? _inspectorRole;
    private Label? _inspectorValues;
    private PanelContainer? _inspectorPanel;
    private StaticBody2D? _ground;
    private readonly MappingViewModel _mapping = new();
    private readonly SignalFlowPresentationViewModel _signalFlow = new();
    private readonly BrainFocusPresentationViewModel _brainFocus = new();
    private readonly UnlockProgressPresentationViewModel _unlockProgress = new();
    private readonly TrainingProfileSummaryPresentationViewModel _profileSummary = new();
    private readonly TrainingProfileSettingsPresentationViewModel _profileSettings = new();
    private readonly List<SensorReading> _sensorReadings = [];
    private readonly List<MotorReading> _motorReadings = [];
    private Button? _mappingToggleButton;
    private CanvasLayer? _brainFocusLayer;
    private Button? _brainFocusDismiss;
    private UiSheet? _brainFocusSheet;
    private Label? _brainFocusSummaryLabel;
    private Label? _brainFocusSelectedLabel;

    // Sensor/motor mapping (#42) defaults to visible when nothing is
    // selected and steps aside for the inspector once something is (see
    // OnSelectionPropertyChanged) — the "second tab" decision recorded on
    // issue #42.
    private bool _showMapping = true;
    private double _mappingRefreshElapsed;
    private const double _mappingRefreshIntervalSeconds = 0.15;

    // Cycled by the time-scale HUD button. Godot's Engine.TimeScale speeds
    // up or slows down every physics/process step uniformly, so it doesn't
    // affect determinism — only how quickly a fixed number of ticks play out.
    private static readonly float[] _timeScales = [1f, 2f, 4f];
    private static readonly TrainingProfile[] _trainingProfiles =
    [
        new("Quick", 4, 180, 30, 0.2, 0.35, 2, CrossoverStrategy.Uniform),
        new("Standard", 8, 600, 50, 0.1, 0.3, 3, CrossoverStrategy.Uniform),
        new("Deep", 16, 1200, 100, 0.06, 0.2, 3, CrossoverStrategy.Blend),
    ];
    private int _trainingProfileIndex = 1;
    private int _sessionGenerationStart;
    private TrainingPresentationViewModel _trainingPresentation = new();
    private ulong _lastModeToggleTicks;

    public SelectionViewModel Selection { get; } = new();

    public ConstructionViewModel Construction { get; } = new();

    private ConstructionPresentationViewModel ConstructionPresentation => new(Construction);

    public override void _Ready()
    {
        if (ProjectSettings.GetSetting("ui/popup_gallery", false).AsBool())
        {
            var gallery = GD.Load<PackedScene>("res://scenes/screens/PopupGalleryScreen.tscn").Instantiate<PopupGalleryScreen>();
            gallery.CloseRequested += () =>
            {
                gallery.QueueFree();
                AddChild(GD.Load<PackedScene>("res://scenes/screens/ComponentGalleryScreen.tscn").Instantiate<ComponentGalleryScreen>());
            };
            AddChild(gallery);
            return;
        }

        if (ProjectSettings.GetSetting("ui/component_gallery", false).AsBool())
        {
            var gallery = GD.Load<PackedScene>("res://scenes/screens/ComponentGalleryScreen.tscn").Instantiate<ComponentGalleryScreen>();
            AddChild(gallery);
            return;
        }

        if (ProjectSettings.GetSetting("ui/sample_preview", false).AsBool())
        {
            _trainingPresentation.Update(5, 3, 8, 12.8, 8.4, "Quick");
            var sample = GD.Load<PackedScene>("res://scenes/screens/SampleFlowScreen.tscn").Instantiate<SampleFlowScreen>();
            sample.Presentation = _trainingPresentation;
            AddChild(sample);
            return;
        }

        // Engine.TimeScale is a global engine setting, not scoped to this
        // scene — reset it on entry so a previous run's time-scale choice
        // (e.g. from CycleTimeScale) can't silently carry over.
        Engine.TimeScale = _timeScales[0];
        // Always-mode so tapping a part to select it still works while
        // paused (#85) -- the HUD buttons already need this for the same
        // reason (see AddHud). Physics- and training-critical children
        // (Creature, Evolver) explicitly pin themselves back to Pausable
        // below so they don't inherit this and keep simulating/training
        // while the scene tree is paused.
        ProcessMode = ProcessModeEnum.Always;
        Selection.PropertyChanged += OnSelectionPropertyChanged;
        Construction.PropertyChanged += OnConstructionPropertyChanged;
        Construction.AnatomyChanged += OnConstructionAnatomyChanged;
        _brainFocus.PropertyChanged += OnBrainFocusChanged;
        AddBackdrop();
        AddBuildModeBackdrop();
        AddGround();
        AddCamera();
        AddCreature();
        AddConstructionCanvas();
        AddSimulateScreen();
        AddBuildScreen();
        AddBrainFocusOverlay();
        AddHud();
        AddInspector();
        AddEvolver();
    }

    private void OnConstructionAnatomyChanged(object? sender, EventArgs eventArgs)
    {
        UpdateToolButtonVisibility();
        UpdateBuildPanelPresentation();
    }

    private void AddRebuildConfirmationDialog(CanvasLayer layer)
    {
        var presentation = ConstructionPresentation;
        _rebuildConfirmationDialog = new ConfirmationDialog
        {
            Title = presentation.RebuildConfirmationTitle,
            DialogText = presentation.RebuildConfirmationBody,
            OkButtonText = presentation.RebuildActionText,
            CancelButtonText = "Keep editing",
            ProcessMode = ProcessModeEnum.Always,
        };
        _rebuildConfirmationDialog.Confirmed += ConfirmRebuildCreation;
        layer.AddChild(_rebuildConfirmationDialog);
    }

    private string ProgressionText()
    {
        var progression = GetNode<SaveManager>("/root/SaveManager").Progression;
        if (progression.ExtraCoreUnlocked)
        {
            return $"Unlock: extra core available (Gen {progression.ExtraCoreUnlockedAtGeneration})";
        }

        var best = _evolver is null || double.IsNegativeInfinity(_evolver.BestFitness)
            ? 0
            : _evolver.BestFitness;
        return $"Next unlock: extra core at {_extraCoreUnlockFitness:0} fitness ({Math.Min(best, _extraCoreUnlockFitness):0.0}/{_extraCoreUnlockFitness:0})";
    }

    private void TryUnlockProgression()
    {
        if (_evolver is null || _evolver.BestFitness < _extraCoreUnlockFitness)
        {
            return;
        }

        var saveManager = GetNode<SaveManager>("/root/SaveManager");
        var attributionId = _activeCreationId is { } activeId && saveManager.Get(activeId) is not null
            ? activeId
            : (Guid?)null;
        if (saveManager.UnlockExtraCore(_evolver.Generation, attributionId))
        {
            ApplyProgression();
            RefreshCreationsPanel();
            GD.Print($"Unlocked extra core at generation {_evolver.Generation}.");
        }
    }

    private void ApplyProgression()
    {
        var unlocked = GetNode<SaveManager>("/root/SaveManager").Progression.ExtraCoreUnlocked;
        Construction.SetMaxCores(unlocked ? 2 : 1);
        UpdateToolButtonVisibility();
        UpdateTrainingLabels();
    }

    // Refreshes the sensor/motor mapping display (#42) from whatever the
    // creature's last physics tick computed. Only does the (small) list
    // work when the mapping view is actually showing, since the inspector
    // view doesn't need it refreshed every frame. Throttled to a fixed
    // cadence (see review discussion on #42) instead of every rendered
    // frame — the numbers are for a human to read, so 60 refreshes/second
    // is wasted allocation without adding legibility.
    public override void _Process(double delta)
    {
        if (Construction.IsActive || _creature is null)
        {
            return;
        }

        _mappingRefreshElapsed += delta;
        if (_mappingRefreshElapsed < _mappingRefreshIntervalSeconds)
        {
            return;
        }

        _mappingRefreshElapsed = 0;
        _creature.ReadMapping(_sensorReadings, _motorReadings);
        _signalFlow.Update(_sensorReadings, _motorReadings, _trainingPresentation.BestFitness, _trainingPresentation.MeanFitness);
        _brainFocus.Update(_creature.Brain, _sensorReadings, _motorReadings);
        if (_showMapping)
        {
            _mapping.Update(_sensorReadings, _motorReadings);
            UpdateInspector();
        }
    }

    private void AddBackdrop()
    {
        _arenaBackdrop = new ArenaBackdrop
        {
            Name = "ArenaBackdrop",
            Theme = _theme,
            Position = new Vector2(-450, 0),
            Size = new Vector2(1800, 720),
            ZIndex = -100,
        };
        AddChild(_arenaBackdrop);
    }

    private void AddBuildModeBackdrop()
    {
        var layer = new CanvasLayer
        {
            Name = "BuildModeBackdropLayer",
            Layer = -1,
        };
        AddChild(layer);

        _buildModeBackdrop = new ColorRect
        {
            Name = "BuildModeBackdrop",
            Color = UiTokens.Neon.Background,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = Construction.IsActive,
        };
        _buildModeBackdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(_buildModeBackdrop);
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
            Shape = new RectangleShape2D { Size = new Vector2(900, 48) },
        });

        ground.AddChild(new Polygon2D
        {
            Color = _theme.GroundFill,
            Polygon = new[]
            {
                new Vector2(-450, -24),
                new Vector2(450, -24),
                new Vector2(450, 24),
                new Vector2(-450, 24),
            },
        });

        ground.AddChild(new Line2D
        {
            Points = new[]
            {
                new Vector2(-450, -24),
                new Vector2(450, -24),
            },
            DefaultColor = _theme.GroundEdge,
            Width = _theme.GroundEdgeWidth,
        });

        AddChild(ground);
        _ground = ground;
    }

    private void AddCamera()
    {
        AddChild(new Camera2D
        {
            Name = "Camera",
            // Y is tuned so the ground/creature sit above the inspector
            // panel's reserved area (see AddInspector) instead of behind it.
            Position = new Vector2(360, 316),
            Zoom = new Vector2(1.15f, 1.15f),
            Enabled = true,
        });
    }

    private void AddCreature()
    {
        var creature = CreateCreatureInstance();
        creature.Name = "Creature";
        // Explicitly Pausable (not the default Inherit) so it doesn't pick
        // up Main's Always process mode (#85) and keep simulating physics
        // while the scene tree is paused.
        creature.ProcessMode = ProcessModeEnum.Pausable;
        creature.Definition = HardcodedCreatureFactory.Create();
        creature.Theme = _theme;
        creature.Position = new Vector2(250, 260);
        AddChild(creature);
        if (!creature.IsBuilt)
        {
            creature.BuildFrom(creature.Definition);
        }

        _creature = creature;
        SetActiveInspector(creature.Definition);
    }

    private static Creature.Creature CreateCreatureInstance()
    {
        var scene = GD.Load<PackedScene>("res://scenes/Creature.tscn");
        return scene.Instantiate<Creature.Creature>();
    }

    // Evolves a small population of brains via fixed parallel physics slots
    // and GeneticAlgorithm, one generation after another. The training HUD
    // binds to GenerationCompleted / NewBestFound below.
    private void AddEvolver()
    {
        var evolver = new Evolver { Name = "Evolver" };
        // Explicitly Pausable (not the default Inherit) so it doesn't pick
        // up Main's Always process mode (#85) and keep training while the
        // scene tree is paused.
        evolver.ProcessMode = ProcessModeEnum.Pausable;
        evolver.GenerationCompleted += OnGenerationCompleted;
        evolver.NewBestFound += OnNewBestFound;
        _trainingPresentation.PropertyChanged -= OnTrainingPresentationChanged;
        _trainingPresentation.Dispose();
        _trainingPresentation = new TrainingPresentationViewModel(new EvolverTrainingProgressSource(
            evolver,
            () => _trainingProfiles[_trainingProfileIndex].Name));
        _trainingPresentation.PropertyChanged += OnTrainingPresentationChanged;
        RefreshTrainingProfileSummary();
        RefreshTrainingProfileSettings();
        if (_simulateScreen is not null)
        {
            _simulateScreen.Presentation = _trainingPresentation;
        }
        RefreshUnlockProgress();

        AddChild(evolver);
        _evolver = evolver;
        if (_creationsScreen?.Visible == true)
        {
            _evolutionDeferredByHomeHub = true;
            UpdateTrainingLabels();
            ResetTrainingSaveStatus(null);
            return;
        }

        StartEvolution();
    }

    private void StartEvolution()
    {
        _evolver?.Stop();
        UpdateTrainingLabels();
        ResetTrainingSaveStatus(null);
        if (_creature?.Brain is null || _evolver is null)
        {
            // No motors (e.g. a just-cleared construction-mode anatomy) —
            // nothing to evolve.
            return;
        }

        var profile = CurrentTrainingProfile();
        _sessionGenerationStart = 0;
        var ga = CreateGeneticAlgorithm(profile);
        _evolver.Start(
            _creature,
            profile.PopulationSize,
            _creature.Brain.LayerSizes,
            ga,
            RngProvider().Random,
            trialDurationTicks: profile.TrialDurationTicks,
            creatureFactory: CreateCreatureInstance);
    }

    private void StartEvolution(CreationDef creation)
    {
        _evolver?.Stop();
        UpdateTrainingLabels();
        ResetTrainingSaveStatus(creation.Training is { } training ? $"Saved generation {training.Generation}" : "Saved draft");
        if (_creature?.Brain is null || _evolver is null)
        {
            return;
        }

        var profile = CurrentTrainingProfile();
        _sessionGenerationStart = creation.Training?.Generation ?? 0;
        var ga = CreateGeneticAlgorithm(profile);
        var resume = creation.Training;
        _evolver.Start(
            _creature,
            profile.PopulationSize,
            _creature.Brain.LayerSizes,
            ga,
            RngProvider().Random,
            resume?.BestGenome,
            resume?.Generation ?? 0,
            profile.TrialDurationTicks,
            CreateCreatureInstance);
    }

    private void OnGenerationCompleted()
    {
        GD.Print($"Generation {_evolver!.Generation} — best: {_evolver.BestFitness:0.0}, mean: {_evolver.MeanFitness:0.0}");
        PersistActiveTraining();
        UpdateTrainingLabels();
        if (_evolver.Generation - _sessionGenerationStart >= CurrentTrainingProfile().MaxGenerations)
        {
            _evolver.Stop();
            GD.Print($"Training session complete after {CurrentTrainingProfile().MaxGenerations} generations.");
        }
    }

    private void OnNewBestFound()
    {
        TryUnlockProgression();
    }

    private void OnTrainingPresentationChanged(object? sender, PropertyChangedEventArgs args)
    {
        UpdateTrainingLabels();
    }

    private void UpdateTrainingLabels()
    {
        if (_generationLabel is not null)
        {
            _generationLabel.Text = _trainingPresentation.GenerationText;
        }

        if (_bestFitnessLabel is not null)
        {
            _bestFitnessLabel.Text = _trainingPresentation.BestFitnessText;
        }

        if (_meanFitnessLabel is not null)
        {
            _meanFitnessLabel.Text = _trainingPresentation.MeanFitnessText;
        }

        if (_progressionLabel is not null)
        {
            _progressionLabel.Text = ProgressionText();
        }
        RefreshUnlockProgress();

        _generationStrip?.SetProgress(
            _trainingPresentation.Generation,
            _trainingPresentation.Candidate,
            _trainingPresentation.Population,
            _trainingPresentation.CompletedCandidateCount,
            _trainingPresentation.CompletedFitness.ToArray(),
            _trainingPresentation.IsTrialActive);
    }

    private RngProvider RngProvider() => GetNode<RngProvider>("/root/RngProvider");

    private static GeneticAlgorithm CreateGeneticAlgorithm(TrainingProfile profile) =>
        new(profile.TournamentSize, profile.MutationRate, profile.MutationStrength, crossoverStrategy: profile.CrossoverStrategy);

    // Swaps the inspector so it reads the currently active CreatureDef.
    // Needed both at startup and whenever construction mode replaces the
    // running creature (see ToggleConstructionMode / #72): the inspector
    // holds its CreatureDef by reference and won't pick up a new one on its
    // own.
    private void SetActiveInspector(CreatureDef definition)
    {
        if (_inspector is not null)
        {
            _inspector.PropertyChanged -= OnInspectorPropertyChanged;
            _inspector.Dispose();
        }

        _inspector = new CreatureInspectorViewModel(definition, Selection);
        _inspector.PropertyChanged += OnInspectorPropertyChanged;
    }

    private void AddConstructionCanvas()
    {
        var canvas = new ConstructionCanvas
        {
            Name = "ConstructionCanvas",
            Theme = _theme,
            ViewModel = Construction,
            Position = new Vector2(250, 260),
            Visible = false,
            // Explicitly Pausable (not the default Inherit) so it doesn't
            // pick up Main's Always process mode (#85) -- ToggleConstructionMode
            // relies on it staying Pausable to justify always resuming
            // pause before entering/leaving Construction (see there).
            ProcessMode = ProcessModeEnum.Pausable,
        };
        AddChild(canvas);
        _constructionCanvas = canvas;
    }

    private void AddSimulateScreen()
    {
        var simulateLayer = new CanvasLayer
        {
            Name = "SimulateOverlay",
            Layer = 2,
            ProcessMode = ProcessModeEnum.Always,
        };
        AddChild(simulateLayer);

        _simulateScreen = new SimulateScreen
        {
            Name = "LiveSimulateScreen",
            Tokens = UiTokens.Neon,
            Hosted = true,
            ShowTopBar = true,
            ShowArenaPlaceholder = false,
            ReadOnlyControls = true,
            InputPassthrough = true,
            Presentation = _trainingPresentation,
            SignalFlow = _signalFlow,
            UnlockProgress = _unlockProgress,
            ProfileSummary = _profileSummary,
            ProfileSettings = _profileSettings,
            PauseActionText = PauseButtonText(),
            Visible = !Construction.IsActive,
        };
        _simulateScreen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _simulateScreen.BrainFocusRequested += ShowBrainFocus;
        _simulateScreen.CreationsRequested += ToggleCreationsPanel;
        _simulateScreen.BuildRequested += ToggleConstructionMode;
        _simulateScreen.PauseRequested += TogglePause;
        _simulateScreen.SpeedRequested += CycleTimeScale;
        _simulateScreen.ResetRequested += ResetEvolution;
        _simulateScreen.TrainingProfileRequested += CycleTrainingProfile;
        _simulateScreen.TrainingProfileSelected += SelectTrainingProfile;
        simulateLayer.AddChild(_simulateScreen);
    }

    private void AddBuildScreen()
    {
        var buildLayer = new CanvasLayer
        {
            Name = "BuildOverlay",
            Layer = 2,
            ProcessMode = ProcessModeEnum.Always,
        };
        AddChild(buildLayer);

        _buildScreen = new BuildScreen
        {
            Name = "LiveBuildScreen",
            Tokens = UiTokens.Neon,
            Hosted = true,
            ShowCanvasPreview = false,
            Presentation = ConstructionPresentation,
            Visible = Construction.IsActive,
        };
        _buildScreen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _buildScreen.SimulateRequested += ToggleConstructionMode;
        _buildScreen.ToolRequested += tool => Construction.ActiveTool = (ConstructionTool)(int)tool;
        _buildScreen.BrainShapeChanged += (layers, neurons) =>
        {
            if (!Construction.IsMoveOnly)
            {
                Construction.SetBrainShape(new BrainShapeDef(layers, neurons));
            }
        };
        _buildScreen.SaveRequested += SaveCreationFromBuild;
        _buildScreen.TrainingRequested += CompleteCreationAndSimulate;
        _buildScreen.RebuildRequested += RebuildCreation;
        _buildScreen.BackRequested += BackFromBuildScreen;
        _buildScreen.CreationNameChanged += RenameActiveCreation;
        _buildScreen.ResetTrainingRequested += ResetActiveCreationTraining;
        _buildScreen.DeleteCreationRequested += RequestDeleteActiveCreation;
        _buildScreen.ClearSelectionRequested += Construction.ClearSelection;
        _buildScreen.DeleteSelectionRequested += DeleteSelectedConstructionParts;
        _buildScreen.ResumeTrainingRequested += ResumeTrainingFromSavedCreation;
        _buildScreen.StatsRequested += ShowStatsCueFromBuild;
        _buildScreen.BrainRequested += ShowBrainCueFromBuild;
        buildLayer.AddChild(_buildScreen);
    }

    private void RefreshUnlockProgress()
    {
        var progression = GetNode<SaveManager>("/root/SaveManager").Progression;
        _unlockProgress.Update(progression, _trainingPresentation.BestFitness, _extraCoreUnlockFitness);
    }

    private void RefreshTrainingProfileSummary()
    {
        var profile = CurrentTrainingProfile();
        var crossover = TrainingProfileCrossoverText(profile);
        _profileSummary.Update(profile.PopulationSize, profile.TrialDurationTicks / 60, profile.MutationRate, crossover);
    }

    private void RefreshTrainingProfileSettings()
    {
        _profileSettings.Update(_trainingProfiles.Select(profile => new TrainingProfileOptionPresentation(
            profile.Name,
            TrainingProfileTargetSummaryText(profile))), _trainingProfileIndex);
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
            Tokens = UiTokens.Neon,
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

        RefreshBrainFocusOverlayLayout();
        _brainFocusSheet.SetBody(CreateBrainFocusBody());
        UpdateBrainFocusLabels();
        _brainFocusLayer.Show();
    }

    private void HideBrainFocus()
    {
        _brainFocusLayer?.Hide();
    }

    private Control CreateBrainFocusBody()
    {
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 10);

        _brainFocusSummaryLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        stack.AddChild(_brainFocusSummaryLabel);

        stack.AddChild(new BrainFocusNetworkView
        {
            Tokens = UiTokens.Neon,
            ViewModel = _brainFocus,
            CustomMinimumSize = new Vector2(500, 220),
            MouseFilter = Control.MouseFilterEnum.Stop,
        });

        _brainFocusSelectedLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        stack.AddChild(_brainFocusSelectedLabel);

        var close = new UiButton
        {
            Tokens = UiTokens.Neon,
            Kind = UiButtonKind.Primary,
            LabelText = "Back to SignalFlow",
        };
        close.Pressed += HideBrainFocus;
        stack.AddChild(close);

        return stack;
    }

    private void OnBrainFocusChanged(object? sender, PropertyChangedEventArgs args)
    {
        UpdateBrainFocusLabels();
    }

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

    private void RefreshBrainFocusOverlayLayout()
    {
        if (_brainFocusLayer is null)
        {
            return;
        }

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

    // Base font size (Godot's default is 16px) and minimum touch target
    // height (Android's recommended ~48dp) for HUD/inspector controls. Sized
    // for the 720-tall design viewport (see [display] in project.godot).
    private const int _hudFontSize = 26;
    private const float _touchTargetHeight = 56f;

    private void AddHud()
    {
        var layer = new CanvasLayer
        {
            Name = "Hud",
            // Keep HUD buttons (Pause included) responsive when TogglePause
            // sets the scene tree's Paused flag — otherwise the "Run"
            // button pausing itself out of existence would be a soft lock.
            ProcessMode = ProcessModeEnum.Always,
        };

        var panel = new PanelContainer
        {
            Position = new Vector2(16, 16),
            Visible = false,
        };
        _hudPanel = panel;
        panel.AddThemeStyleboxOverride("panel", CreateHudPanelStyle());

        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(600, _touchTargetHeight),
        };
        row.AddThemeConstantOverride("separation", 20);

        var button = new Button
        {
            Name = "RandomizeButton",
            Text = "Randomize",
            CustomMinimumSize = new Vector2(180, _touchTargetHeight),
        };
        button.AddThemeColorOverride("font_color", _theme.GroundEdge);
        button.AddThemeFontSizeOverride("font_size", _hudFontSize);
        button.Pressed += RandomizeCreatureBrain;

        _buildModeButton = new Button
        {
            Name = "BuildModeButton",
            Text = BuildModeButtonText(),
            CustomMinimumSize = new Vector2(180, _touchTargetHeight),
        };
        _buildModeButton.AddThemeColorOverride("font_color", _theme.SelectionGlow);
        _buildModeButton.AddThemeFontSizeOverride("font_size", _hudFontSize);
        _buildModeButton.Pressed += ToggleConstructionMode;

        _seedLabel = new Label
        {
            Name = "SeedLabel",
            Text = SeedText(),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _seedLabel.AddThemeColorOverride("font_color", _theme.Beam);
        _seedLabel.AddThemeFontSizeOverride("font_size", _hudFontSize);

        row.AddChild(button);
        row.AddChild(_buildModeButton);
        row.AddChild(_seedLabel);
        _creationsButton = new Button
        {
            Name = "CreationsButton",
            Text = "Creations",
            CustomMinimumSize = new Vector2(180, _touchTargetHeight),
        };
        _creationsButton.AddThemeFontSizeOverride("font_size", _hudFontSize);
        _creationsButton.Pressed += ToggleCreationsPanel;
        row.AddChild(_creationsButton);
        panel.AddChild(row);
        layer.AddChild(panel);
        AddChild(layer);

        AddConstructionToolRow(layer);
        AddRebuildConfirmationDialog(layer);
        AddTrainingPanel(layer);
        AddCreationsPanel(layer);
    }

    private void AddCreationsPanel(CanvasLayer layer)
    {
        var overlayLayer = new CanvasLayer
        {
            Name = "CreationsOverlay",
            Layer = 20,
            ProcessMode = ProcessModeEnum.Always,
        };
        AddChild(overlayLayer);

        var saveManager = GetNode<SaveManager>("/root/SaveManager");
        _creationsScreen = new CreationsScreen
        {
            Tokens = UiTokens.Neon,
            ShowComponentLibraryLink = ShouldShowComponentLibraryLink(),
            Visible = true,
        };
        _creationsScreen.Setup(saveManager.CreationsPresentation);
        _creationsScreen.BackRequested += CloseCreationsHome;
        _creationsScreen.NewRequested += StartNewCreationFromHome;
        _creationsScreen.AchievementsRequested += ShowAchievementsCueFromHome;
        _creationsScreen.RestoreExampleRequested += RestoreExampleFromHome;
        _creationsScreen.ColorsAndStylesRequested += OpenColorsAndStylesFromHome;
        _creationsScreen.ComponentLibraryRequested += OpenComponentLibraryFromHome;
        _creationsScreen.OpenRequested += OpenCreationFromScreen;
        _creationsScreen.EditRequested += EditCreationFromScreen;
        _creationsScreen.DuplicateRequested += RequestDuplicateCreationFromScreen;
        _creationsScreen.DeleteRequested += RequestDeleteCreationFromScreen;
        overlayLayer.AddChild(_creationsScreen);

        _deleteCreationConfirmationDialog = new ConfirmationDialog
        {
            Title = "Delete Creation?",
            OkButtonText = "Delete",
            CancelButtonText = "Keep",
            ProcessMode = ProcessModeEnum.Always,
        };
        _deleteCreationConfirmationDialog.Confirmed += ConfirmDeleteCreationFromScreen;
        overlayLayer.AddChild(_deleteCreationConfirmationDialog);
        AddDuplicateCreationSheet(overlayLayer);
        AddDeleteCreationToast(overlayLayer);

        RefreshCreationsPanel();
    }

    private static bool ShouldShowComponentLibraryLink()
    {
        return OS.IsDebugBuild()
            && ProjectSettings.GetSetting("ui/show_component_library_link", true).AsBool();
    }

    private void OpenComponentLibraryFromHome()
    {
        if (_componentGalleryScreen is not null)
        {
            _componentGalleryHost?.Show();
            _componentGalleryLayer?.Show();
            return;
        }

        _componentGalleryLayer = new CanvasLayer
        {
            Name = "ComponentGalleryOverlay",
            Layer = 30,
            ProcessMode = ProcessModeEnum.Always,
        };
        AddChild(_componentGalleryLayer);

        _componentGalleryHost = new Control
        {
            Name = "ComponentGalleryHost",
            ProcessMode = ProcessModeEnum.Always,
        };
        _componentGalleryHost.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _componentGalleryHost.MouseFilter = Control.MouseFilterEnum.Stop;
        _componentGalleryLayer.AddChild(_componentGalleryHost);

        _componentGalleryScreen = GD.Load<PackedScene>("res://scenes/screens/ComponentGalleryScreen.tscn").Instantiate<ComponentGalleryScreen>();
        _componentGalleryScreen.ShowCloseAction = true;
        _componentGalleryScreen.CloseRequested += CloseComponentLibrary;
        _componentGalleryScreen.ProcessMode = ProcessModeEnum.Always;
        _componentGalleryHost.AddChild(_componentGalleryScreen);
    }

    private void CloseComponentLibrary()
    {
        if (_componentGalleryScreen is null)
        {
            return;
        }

        _componentGalleryLayer?.QueueFree();
        _componentGalleryLayer = null;
        _componentGalleryHost = null;
        _componentGalleryScreen = null;
    }

    private void OpenColorsAndStylesFromHome()
    {
        if (_colorsAndStylesScreen is not null)
        {
            _colorsAndStylesHost?.Show();
            _colorsAndStylesLayer?.Show();
            return;
        }

        _colorsAndStylesLayer = new CanvasLayer
        {
            Name = "ColorsAndStylesOverlay",
            Layer = 30,
            ProcessMode = ProcessModeEnum.Always,
        };
        AddChild(_colorsAndStylesLayer);

        _colorsAndStylesHost = new Control
        {
            Name = "ColorsAndStylesHost",
            ProcessMode = ProcessModeEnum.Always,
        };
        _colorsAndStylesHost.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _colorsAndStylesHost.MouseFilter = Control.MouseFilterEnum.Stop;
        _colorsAndStylesLayer.AddChild(_colorsAndStylesHost);

        _colorsAndStylesScreen = GD.Load<PackedScene>("res://scenes/screens/ColorsAndStylesScreen.tscn").Instantiate<ColorsAndStylesScreen>();
        _colorsAndStylesScreen.ShowCloseAction = true;
        _colorsAndStylesScreen.CloseRequested += CloseColorsAndStyles;
        _colorsAndStylesScreen.ProcessMode = ProcessModeEnum.Always;
        _colorsAndStylesHost.AddChild(_colorsAndStylesScreen);
    }

    private void CloseColorsAndStyles()
    {
        if (_colorsAndStylesScreen is null)
        {
            return;
        }

        _colorsAndStylesLayer?.QueueFree();
        _colorsAndStylesLayer = null;
        _colorsAndStylesHost = null;
        _colorsAndStylesScreen = null;
    }

    private void AddDeleteCreationToast(CanvasLayer overlayLayer)
    {
        _deleteCreationToast = new UiToast
        {
            Name = "DeleteCreationToast",
            Tokens = UiTokens.Neon,
            CustomMinimumSize = new Vector2(420, _touchTargetHeight),
            Position = new Vector2(UiSpacing.ScreenEdgeInset(UiTokens.Neon) * 2, UiLayout.CanvasHeight - _touchTargetHeight - (UiSpacing.ScreenEdgeInset(UiTokens.Neon) * 2)),
            ProcessMode = ProcessModeEnum.Always,
        };
        _deleteCreationToast.UndoPressed += RestoreDeletedCreationFromToast;
        overlayLayer.AddChild(_deleteCreationToast);
    }

    private void AddDuplicateCreationSheet(CanvasLayer overlayLayer)
    {
        _duplicateCreationSheet = new DuplicateCreationSheet
        {
            Name = "DuplicateCreationSheet",
            Tokens = UiTokens.Neon,
            ProcessMode = ProcessModeEnum.Always,
        };
        _duplicateCreationSheet.CopyBrainRequested += () => ConfirmDuplicateCreationFromScreen(CreationDuplicateMode.CopyTraining);
        _duplicateCreationSheet.StartFreshRequested += () => ConfirmDuplicateCreationFromScreen(CreationDuplicateMode.StartFresh);
        _duplicateCreationSheet.CancelRequested += CancelDuplicateCreationFromScreen;
        overlayLayer.AddChild(_duplicateCreationSheet);
    }

    private void ToggleCreationsPanel()
    {
        if (_creationsScreen is null)
        {
            return;
        }

        _creationsScreen.Visible = !_creationsScreen.Visible;
        if (_creationsScreen.Visible)
        {
            RefreshCreationsPanel();
        }
    }

    private void CloseCreationsHome()
    {
        if (_creationsScreen is not null)
        {
            _creationsScreen.Visible = false;
        }

        if (_evolutionDeferredByHomeHub)
        {
            _evolutionDeferredByHomeHub = false;
            StartEvolution();
        }
    }

    private void RefreshCreationsPanel()
    {
        var saveManager = GetNode<SaveManager>("/root/SaveManager");
        saveManager.CreationsPresentation.Refresh();
        if (saveManager.CreationsPresentation.LoadError is { } error)
        {
            GD.PrintErr($"Loading Creations failed: {error}");
        }
    }

    private void OpenCreationFromScreen(string creationKey, string creationName)
    {
        if (TryGetCreationFromScreen(creationKey, creationName, out var creation))
        {
            EditCreation(creation);
            if (_creationsScreen is not null)
            {
                _creationsScreen.Visible = false;
            }
        }
    }

    private void EditCreationFromScreen(string creationKey, string creationName)
    {
        if (TryGetCreationFromScreen(creationKey, creationName, out var creation))
        {
            EditCreation(creation);
            if (_creationsScreen is not null)
            {
                _creationsScreen.Visible = false;
            }
        }
    }

    private void RequestDuplicateCreationFromScreen(string creationKey, string creationName)
    {
        if (!Guid.TryParse(creationKey, out var id))
        {
            GD.PrintErr($"Could not duplicate Creation '{creationName}': invalid id '{creationKey}'.");
            return;
        }

        var saveManager = GetNode<SaveManager>("/root/SaveManager");
        CreationDef? copy = null;
        if (TryRunFileOperation(
            () => copy = saveManager.Duplicate(id, CreationDuplicateMode.CopyTraining),
            $"Duplicating Creation '{creationName}' with {CreationDuplicateMode.CopyTraining}"))
        {
            _deleteCreationToast?.ShowMessage($"Copied · {copy?.Name ?? creationName}");
        }

        RefreshCreationsPanel();
    }

    private void ConfirmDuplicateCreationFromScreen(CreationDuplicateMode mode)
    {
        if (_pendingDuplicateCreationId is not { } id)
        {
            return;
        }

        var name = _pendingDuplicateCreationName ?? id.ToString();
        _pendingDuplicateCreationId = null;
        _pendingDuplicateCreationName = null;
        if (_duplicateCreationSheet is not null)
        {
            _duplicateCreationSheet.Visible = false;
        }

        var saveManager = GetNode<SaveManager>("/root/SaveManager");
        TryRunFileOperation(
            () => saveManager.Duplicate(id, mode),
            $"Duplicating Creation '{name}' with {mode}");
        RefreshCreationsPanel();
    }

    private void CancelDuplicateCreationFromScreen()
    {
        _pendingDuplicateCreationId = null;
        _pendingDuplicateCreationName = null;
        if (_duplicateCreationSheet is not null)
        {
            _duplicateCreationSheet.Visible = false;
        }
    }

    private void RequestDeleteCreationFromScreen(string creationKey, string creationName)
    {
        if (!Guid.TryParse(creationKey, out var id))
        {
            GD.PrintErr($"Could not delete Creation '{creationName}': invalid id '{creationKey}'.");
            return;
        }

        _pendingDeleteCreationId = id;
        _pendingDeleteCreationName = creationName;
        if (_deleteCreationConfirmationDialog is null)
        {
            ConfirmDeleteCreationFromScreen();
            return;
        }

        _deleteCreationConfirmationDialog.DialogText =
            $"Delete {creationName}? It will leave Creations now. You can Undo for 10 seconds.";
        _deleteCreationConfirmationDialog.PopupCentered();
    }

    private void ConfirmDeleteCreationFromScreen()
    {
        if (_pendingDeleteCreationId is not { } id)
        {
            return;
        }

        var name = _pendingDeleteCreationName ?? id.ToString();
        _pendingDeleteCreationId = null;
        _pendingDeleteCreationName = null;
        var saveManager = GetNode<SaveManager>("/root/SaveManager");
        CreationDef? deleted = null;
        var succeeded = TryRunFileOperation(
            () =>
            {
                deleted = saveManager.DeleteAndCapture(id);
                if (deleted is null)
                {
                    throw new FileNotFoundException($"Creation '{id}' was not found.");
                }
            },
            $"Deleting Creation '{name}'");
        if (!succeeded)
        {
            return;
        }

        if (_activeCreationId == id)
        {
            _activeCreationId = null;
            _evolver?.Stop();
            UpdateTrainingLabels();
            ResetTrainingSaveStatus(null);
            Construction.IsActive = false;
            if (_creationsScreen is not null)
            {
                _creationsScreen.Visible = true;
            }

            UpdateToolButtonVisibility();
        }

        _lastDeletedCreation = deleted;
        RefreshCreationsPanel();
        _deleteCreationToast?.ShowMessage($"Deleted {name}.", "Undo", 10);
    }

    private void RestoreDeletedCreationFromToast()
    {
        if (_lastDeletedCreation is not { } creation)
        {
            return;
        }

        var saveManager = GetNode<SaveManager>("/root/SaveManager");
        if (!TryRunFileOperation(
            () => saveManager.Save(creation),
            $"Restoring Creation '{creation.Name}'"))
        {
            return;
        }

        _lastDeletedCreation = null;
        RefreshCreationsPanel();
    }

    private void StartNewCreationFromHome()
    {
        if (GetTree().Paused)
        {
            TogglePause();
        }

        _activeCreationId = null;
        _evolutionDeferredByHomeHub = false;
        Selection.Clear();
        Construction.ResetDraft();
        Construction.IsActive = true;
        UpdateToolButtonVisibility();
        if (_creationsScreen is not null)
        {
            _creationsScreen.Visible = false;
        }
    }

    private void ShowAchievementsCueFromHome()
    {
        _deleteCreationToast?.ShowMessage("Achievements open in milestone 0.13.0.");
    }

    private void RestoreExampleFromHome()
    {
        var saveManager = GetNode<SaveManager>("/root/SaveManager");
        if (saveManager.Get(DefaultCreationTemplates.StarterWormId) is not null)
        {
            _deleteCreationToast?.ShowMessage("Example already restored.");
            return;
        }

        if (!TryRunFileOperation(
            () => saveManager.Save(DefaultCreationTemplates.CreateStarterWorm()),
            "Restoring example Creation"))
        {
            return;
        }

        RefreshCreationsPanel();
        _deleteCreationToast?.ShowMessage("Restored example.");
    }

    private bool TryGetCreationFromScreen(string creationKey, string creationName, out CreationDef creation)
    {
        creation = null!;
        if (!Guid.TryParse(creationKey, out var id))
        {
            GD.PrintErr($"Could not open Creation '{creationName}': invalid id '{creationKey}'.");
            return false;
        }

        var saveManager = GetNode<SaveManager>("/root/SaveManager");
        var loaded = saveManager.Get(id);
        if (loaded is null)
        {
            GD.PrintErr($"Creation '{creationName}' ({id}) was not found.");
            RefreshCreationsPanel();
            return false;
        }

        creation = loaded;
        return true;
    }

    private void OpenCreation(CreationDef creation)
    {
        _evolutionDeferredByHomeHub = false;
        if (Construction.IsActive)
        {
            Construction.IsActive = false;
        }

        ApplyLoadedCreature(creation);
        if (_creationsPanel is not null)
        {
            _creationsPanel.Visible = false;
        }
    }

    private void EditCreation(CreationDef creation)
    {
        OpenCreation(creation);
        Construction.Load(creation.Creature, moveOnly: true, brainShape: creation.BrainShape, creationName: creation.Name, training: creation.Training);
        Construction.IsActive = true;
        UpdateToolButtonVisibility();
    }

    private void RebuildCreation()
    {
        if (!Construction.IsMoveOnly)
        {
            return;
        }

        if (_rebuildConfirmationDialog is null)
        {
            ConfirmRebuildCreation();
            return;
        }

        var presentation = ConstructionPresentation;
        _rebuildConfirmationDialog.Title = presentation.RebuildConfirmationTitle;
        _rebuildConfirmationDialog.DialogText = presentation.RebuildConfirmationBody;
        _rebuildConfirmationDialog.OkButtonText = presentation.RebuildActionText;
        _rebuildConfirmationDialog.PopupCentered();
    }

    private void ConfirmRebuildCreation()
    {
        if (!Construction.IsMoveOnly)
        {
            return;
        }

        var saveManager = GetNode<SaveManager>("/root/SaveManager");
        var draft = saveManager.ConstructionDraftWorkflow.BeginRebuildDraft(
            _creature?.Definition ?? throw new InvalidOperationException("No creature is loaded."));
        _activeCreationId = draft.ActiveCreationId;
        Construction.Load(draft.Creature);
        UpdateToolButtonVisibility();
        Construction.SetCompletedMessage(draft.StatusMessage);
    }

    private void ApplyLoadedCreature(CreationDef creation)
    {
        ApplyLoadedCreature(creation, startEvolution: true);
    }

    private void ApplyLoadedCreature(CreationDef creation, bool startEvolution)
    {
        if (_creature is null)
        {
            return;
        }

        Selection.Clear();
        _activeCreationId = creation.Id;
        _creature.BrainShape = creation.BrainShape;
        _creature.BuildFrom(creation.Creature);
        Construction.Load(creation.Creature, brainShape: creation.BrainShape, creationName: creation.Name, training: creation.Training);
        SetActiveInspector(creation.Creature);
        if (startEvolution)
        {
            StartEvolution(creation);
        }
        else
        {
            _evolver?.Stop();
            UpdateTrainingLabels();
            ResetTrainingSaveStatus(null);
        }
    }

    private void ResumeTrainingFromSavedCreation()
    {
        Construction.IsActive = false;
        UpdateToolButtonVisibility();
        _deleteCreationToast?.ShowMessage("Train setup opens in milestone 0.12.0.");
    }

    private void ShowStatsCueFromBuild()
    {
        _deleteCreationToast?.ShowMessage("Stats open in milestone 0.12.0.");
    }

    private void ShowBrainCueFromBuild()
    {
        _deleteCreationToast?.ShowMessage("Brain view opens in milestone 0.12.0.");
    }

    private void BackFromBuildScreen()
    {
        if (Construction.IsMoveOnly)
        {
            if (!LeaveConstructionAndPersistEdits())
            {
                return;
            }

            Construction.IsActive = false;
            if (_creationsScreen is not null)
            {
                RefreshCreationsPanel();
                _creationsScreen.Visible = true;
            }

            UpdateToolButtonVisibility();
            return;
        }

        ToggleConstructionMode();
    }

    private bool LeaveConstructionAndPersistEdits()
    {
        if (!Construction.TryLeave(out var editedCreature, out var errors))
        {
            Construction.SetBlockedLeaveMessage(errors);
            return false;
        }

        if (editedCreature is not null)
        {
            ApplyEditedCreature(editedCreature);
        }

        return true;
    }

    private void RenameActiveCreation(string name)
    {
        if (_activeCreationId is not { } id)
        {
            Construction.SetCreationName(name);
            return;
        }

        var saveManager = GetNode<SaveManager>("/root/SaveManager");
        CreationDef? renamed = null;
        if (!TryRunFileOperation(
            () => renamed = saveManager.UpdateIfPresent(
                id,
                source => new CreationDef(source.Id, name, source.Creature, source.BrainShape, source.Training)),
            $"Renaming Creation '{id}'"))
        {
            return;
        }

        if (renamed is null)
        {
            return;
        }

        Construction.SetCreationName(renamed.Name);
        RefreshCreationsPanel();
        _deleteCreationToast?.ShowMessage($"Renamed to {renamed.Name}.");
    }

    private void DeleteSelectedConstructionParts()
    {
        Construction.DeleteSelectedParts();
    }

    private void ResetActiveCreationTraining()
    {
        if (_activeCreationId is not { } id)
        {
            return;
        }

        if (!TryRunFileOperation(
            () => GetNode<SaveManager>("/root/SaveManager").ResetTraining(id),
            $"Resetting training for Creation {id}"))
        {
            return;
        }

        ResetTrainingSaveStatus(null);
        if (GetNode<SaveManager>("/root/SaveManager").Get(id) is { } creation)
        {
            ApplyLoadedCreature(creation, startEvolution: false);
            Construction.Load(creation.Creature, moveOnly: true, brainShape: creation.BrainShape, creationName: creation.Name, training: creation.Training);
            Construction.IsActive = true;
        }

        _deleteCreationToast?.ShowMessage("Training reset.");
    }

    private void RequestDeleteActiveCreation()
    {
        if (_activeCreationId is not { } id)
        {
            return;
        }

        var name = Construction.CreationName;
        _pendingDeleteCreationId = id;
        _pendingDeleteCreationName = name;
        if (_deleteCreationConfirmationDialog is null)
        {
            ConfirmDeleteCreationFromScreen();
            return;
        }

        _deleteCreationConfirmationDialog.DialogText =
            $"Delete {name}? The creation and its trained brain are removed. You can Undo for 10 seconds.";
        _deleteCreationConfirmationDialog.PopupCentered();
    }

    // Training HUD (#51): generation/best/mean readout plus run/pause,
    // reset, and time-scale controls for the Evolver started in AddEvolver.
    // Occupies the same row position as the construction tool row (below)
    // since the two are mutually exclusive — this panel is only visible
    // outside construction mode, the tool row only inside it.
    private void AddTrainingPanel(CanvasLayer layer)
    {
        var panel = new PanelContainer
        {
            Position = new Vector2(16, 16 + _touchTargetHeight + 12),
            Visible = false,
        };
        panel.AddThemeStyleboxOverride("panel", CreateHudPanelStyle());

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);

        var statsRow = new HBoxContainer();
        statsRow.AddThemeConstantOverride("separation", 20);
        _generationLabel = CreateTrainingLabel("GenerationLabel", "Gen: 0");
        _bestFitnessLabel = CreateTrainingLabel("BestFitnessLabel", "Best: —");
        _meanFitnessLabel = CreateTrainingLabel("MeanFitnessLabel", "Mean: 0.0");
        _progressionLabel = CreateTrainingLabel("ProgressionLabel", ProgressionText());
        statsRow.AddChild(_generationLabel);
        statsRow.AddChild(_bestFitnessLabel);
        statsRow.AddChild(_meanFitnessLabel);
        _generationStrip = new GenerationStrip
        {
            Name = "GenerationStrip",
            Palette = _theme,
        };

        var controlsRow = new HBoxContainer();
        controlsRow.AddThemeConstantOverride("separation", 20);
        _pauseButton = new Button
        {
            Name = "PauseButton",
            Text = PauseButtonText(),
            CustomMinimumSize = new Vector2(160, _touchTargetHeight),
        };
        _pauseButton.AddThemeFontSizeOverride("font_size", _hudFontSize);
        _pauseButton.Pressed += TogglePause;

        var resetButton = new Button
        {
            Name = "ResetButton",
            Text = "Reset",
            CustomMinimumSize = new Vector2(160, _touchTargetHeight),
        };
        resetButton.AddThemeFontSizeOverride("font_size", _hudFontSize);
        resetButton.Pressed += ResetEvolution;

        _timeScaleButton = new Button
        {
            Name = "TimeScaleButton",
            Text = TimeScaleButtonText(),
            CustomMinimumSize = new Vector2(160, _touchTargetHeight),
        };
        _timeScaleButton.AddThemeFontSizeOverride("font_size", _hudFontSize);
        _timeScaleButton.Pressed += CycleTimeScale;

        _trainingProfileButton = new Button
        {
            Name = "TrainingProfileButton",
            Text = TrainingProfileButtonText(),
            CustomMinimumSize = new Vector2(260, _touchTargetHeight),
            TooltipText = "Tap to cycle the session profile. The active run restarts with the new settings.",
        };
        _trainingProfileButton.AddThemeFontSizeOverride("font_size", _hudFontSize);
        _trainingProfileButton.Pressed += CycleTrainingProfile;

        controlsRow.AddChild(_pauseButton);
        controlsRow.AddChild(resetButton);
        controlsRow.AddChild(_timeScaleButton);
        controlsRow.AddChild(_trainingProfileButton);

        _trainingProfileSummaryLabel = CreateTrainingLabel("TrainingProfileSummaryLabel", TrainingProfileSummaryText());
        _trainingSaveStatusLabel = CreateTrainingLabel("TrainingSaveStatusLabel", string.Empty);
        _trainingSaveStatusLabel.AddThemeColorOverride("font_color", _theme.SelectionGlow);
        column.AddChild(statsRow);
        column.AddChild(_generationStrip);
        column.AddChild(controlsRow);
        column.AddChild(_trainingProfileSummaryLabel);
        column.AddChild(_trainingSaveStatusLabel);
        column.AddChild(_progressionLabel);
        panel.AddChild(column);
        layer.AddChild(panel);

        _trainingPanel = panel;
        UpdateTrainingLabels();
    }

    private Label CreateTrainingLabel(string name, string text)
    {
        var label = new Label
        {
            Name = name,
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeColorOverride("font_color", _theme.Beam);
        label.AddThemeFontSizeOverride("font_size", _hudFontSize);
        return label;
    }

    // Pausing freezes the whole scene tree — physics stops advancing, so
    // the in-progress trial's creature motion, fitness recording, and
    // trial-boundary checks all freeze in place with it (Evolver/
    // TrialController/Creature all use the default Pausable process mode).
    // The Hud CanvasLayer is set to Always below so its buttons (including
    // this one) keep responding while paused.
    private void TogglePause()
    {
        var tree = GetTree();
        tree.Paused = !tree.Paused;
        if (_pauseButton is not null)
        {
            _pauseButton.Text = PauseButtonText();
        }
        if (_simulateScreen is not null)
        {
            _simulateScreen.PauseActionText = PauseButtonText();
        }
    }

    private string PauseButtonText() => GetTree().Paused ? "Run" : "Pause";

    // Same effect as "Randomize": reseed and restart evolution from a fresh
    // random population. Exposed as its own training-HUD control per issue
    // #51's acceptance criteria, even though it shares RandomizeCreatureBrain's
    // implementation.
    private void ResetEvolution()
    {
        if (_activeCreationId is { } id)
        {
            // If persisting the reset fails (#114), don't randomize the live
            // brain either: the disk copy would still hold the old training,
            // so a reset that "worked" on screen but not on disk would look
            // like it silently reverted after the next app restart. The
            // failure is already logged inside TryRunFileOperation.
            if (!TryRunFileOperation(
                () => GetNode<SaveManager>("/root/SaveManager").ResetTraining(id),
                $"Resetting training for Creation {id}"))
            {
                return;
            }
        }

        RandomizeCreatureBrain();
    }

    private void CycleTimeScale()
    {
        _timeScaleIndex = (_timeScaleIndex + 1) % _timeScales.Length;
        Engine.TimeScale = _timeScales[_timeScaleIndex];
        if (_timeScaleButton is not null)
        {
            _timeScaleButton.Text = TimeScaleButtonText();
        }
    }

    private string TimeScaleButtonText() => $"{_timeScales[_timeScaleIndex]:0.#}x";

    private TrainingProfile CurrentTrainingProfile() => _trainingProfiles[_trainingProfileIndex];

    private string TrainingProfileButtonText()
    {
        var profile = CurrentTrainingProfile();
        return $"Training: {profile.Name} (restart)";
    }

    private string TrainingProfileSummaryText()
    {
        var profile = CurrentTrainingProfile();
        var crossover = TrainingProfileCrossoverText(profile);
        return $"{profile.PopulationSize} candidates | {profile.TrialDurationTicks / 60}s trials | {profile.MutationRate:P0} mutation | {crossover}";
    }

    private static string TrainingProfileCrossoverText(TrainingProfile profile)
    {
        return profile.CrossoverStrategy == CrossoverStrategy.Blend ? "blended genes" : "uniform genes";
    }

    private static string TrainingProfileTargetSummaryText(TrainingProfile profile)
    {
        var mutation = (profile.MutationRate * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
        return $"{profile.PopulationSize} candidates · {profile.TrialDurationTicks / 60}s trials · {mutation}% mutation · {TrainingProfileCrossoverText(profile)}";
    }

    private void CycleTrainingProfile()
    {
        ApplyTrainingProfileIndex((_trainingProfileIndex + 1) % _trainingProfiles.Length);
    }

    private void SelectTrainingProfile(int index)
    {
        if (index < 0 || index >= _trainingProfiles.Length || index == _trainingProfileIndex)
        {
            return;
        }

        ApplyTrainingProfileIndex(index);
    }

    private void ApplyTrainingProfileIndex(int index)
    {
        _trainingProfileIndex = index;
        if (_trainingProfileButton is not null)
        {
            _trainingProfileButton.Text = TrainingProfileButtonText();
        }

        if (_trainingProfileSummaryLabel is not null)
        {
            _trainingProfileSummaryLabel.Text = TrainingProfileSummaryText();
        }

        RefreshTrainingProfileSummary();
        RefreshTrainingProfileSettings();

        if (!Construction.IsActive)
        {
            // PersistActiveTraining now writes to disk in the background
            // (#113), so a Get() right after it can't be relied on to
            // observe that write yet. Merge the in-memory training snapshot
            // into the read Creation directly instead of waiting on disk.
            PersistActiveTraining();
            if (_activeCreationId is { } id)
            {
                var creation = GetNode<SaveManager>("/root/SaveManager").Get(id);
                if (creation is not null)
                {
                    StartEvolution(WithActiveTrainingSnapshot(creation));
                    return;
                }
            }

            StartEvolution();
        }
    }

    private CreationDef WithActiveTrainingSnapshot(CreationDef creation)
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
            new TrainingStateDef(_evolver.LayerSizes, genome.ToArray(), _evolver.Generation, Activation.Tanh.ToString(), _evolver.BestFitness));
    }

    private void AddConstructionToolRow(CanvasLayer layer)
    {
        var panel = new PanelContainer
        {
            Position = new Vector2(16, 16 + _touchTargetHeight + 28),
            Visible = false,
        };
        panel.AddThemeStyleboxOverride("panel", CreateHudPanelStyle());

        var rail = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(330, 0),
        };
        rail.AddThemeConstantOverride("separation", 10);

        rail.AddChild(CreateBuildPanelLabel("Tools", _theme.SelectionGlow, _hudFontSize));

        _placeToolButton = CreateToolButton("PlaceToolButton", "Place");
        _placeToolButton.Pressed += () => Construction.ActiveTool = ConstructionTool.Place;

        _beamToolButton = CreateToolButton("BeamToolButton", "Beam");
        _beamToolButton.Pressed += () => Construction.ActiveTool = ConstructionTool.Beam;

        _coreToolButton = CreateToolButton("CoreToolButton", "Core");
        _coreToolButton.Pressed += () => Construction.ActiveTool = ConstructionTool.Core;

        _deleteToolButton = CreateToolButton("DeleteToolButton", "Delete");
        _deleteToolButton.Pressed += () => Construction.ActiveTool = ConstructionTool.Delete;

        _editLockReasonLabel = new Label
        {
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(300, 0),
        };
        _editLockReasonLabel.AddThemeColorOverride("font_color", _theme.GroundEdge);
        _editLockReasonLabel.AddThemeFontSizeOverride("font_size", 20);

        _completeButton = CreateToolButton("CompleteButton", "Complete");
        _completeButton.Pressed += CompleteCreation;
        _rebuildButton = CreateToolButton("RebuildButton", "Rebuild");
        _rebuildButton.Pressed += RebuildCreation;
        _rebuildButton.AddThemeColorOverride("font_color", Colors.OrangeRed);

        rail.AddChild(_placeToolButton);
        rail.AddChild(_beamToolButton);
        rail.AddChild(_coreToolButton);
        rail.AddChild(_deleteToolButton);
        rail.AddChild(_editLockReasonLabel);
        rail.AddChild(_completeButton);
        rail.AddChild(_rebuildButton);
        panel.AddChild(rail);
        layer.AddChild(panel);

        _toolPanel = panel;
        AddBuildInfoPanel(layer);
        ApplyProgression();
        UpdateToolButtonHighlight();
        UpdateToolButtonVisibility();
        UpdateBuildPanelPresentation();
    }

    private void AddBuildInfoPanel(CanvasLayer layer)
    {
        var panel = new PanelContainer
        {
            Position = new Vector2(820, 16 + _touchTargetHeight + 28),
            Visible = false,
        };
        panel.AddThemeStyleboxOverride("panel", CreateHudPanelStyle());

        var column = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(420, 0),
        };
        column.AddThemeConstantOverride("separation", 10);

        column.AddChild(CreateBuildPanelLabel(ConstructionBuildPanelPresentation.Title, _theme.SelectionGlow, _hudFontSize));
        column.AddChild(CreateBuildPanelLabel("Inputs", _theme.Beam, 22));
        _buildInputSummaryLabel = CreateBuildPanelLabel(string.Empty, _theme.GroundEdge, 20);
        column.AddChild(_buildInputSummaryLabel);
        column.AddChild(CreateBuildPanelLabel("Motor relations", _theme.Beam, 22));
        _buildMotorSummaryLabel = CreateBuildPanelLabel(string.Empty, _theme.GroundEdge, 20);
        column.AddChild(_buildMotorSummaryLabel);
        column.AddChild(CreateBuildPanelLabel("Validation", _theme.Beam, 22));
        _buildValidationLabel = CreateBuildPanelLabel(string.Empty, _theme.GroundEdge, 20);
        column.AddChild(_buildValidationLabel);

        panel.AddChild(column);
        layer.AddChild(panel);
        _buildInfoPanel = panel;
    }

    private Label CreateBuildPanelLabel(string text, Color color, int fontSize)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(380, 0),
        };
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }

    private Button CreateToolButton(string name, string text)
    {
        var button = new Button
        {
            Name = name,
            Text = text,
            CustomMinimumSize = new Vector2(300, _touchTargetHeight),
        };
        button.AddThemeFontSizeOverride("font_size", _hudFontSize);
        return button;
    }

    private void RandomizeCreatureBrain()
    {
        if (_creature is null)
        {
            return;
        }

        // Reseeds the run and restarts evolution from a fresh random
        // population, rather than hand-editing one brain: with the Evolver
        // driving trials continuously, a single RandomizeBrain() call would
        // just be overwritten by the next generation anyway.
        var seed = Random.Shared.Next(int.MinValue, int.MaxValue);
        RngProvider().Reseed(seed);
        StartEvolution();
        if (_seedLabel is not null)
        {
            _seedLabel.Text = SeedText();
        }
    }

    // 0.3.0 construction mode: toggling swaps the running creature for an
    // editable node canvas. Leaving Build mode with a non-empty, valid
    // anatomy replaces the running creature with the edited one (#72); an
    // empty anatomy leaves the current creature untouched, which is how the
    // original hardcoded worm keeps working for a user who never edits
    // anything (see docs/CONSTRUCTION_MODE.md).
    private void ToggleConstructionMode()
    {
        var now = Time.GetTicksMsec();
        if (_lastModeToggleTicks != 0 && now - _lastModeToggleTicks < 250)
        {
            return;
        }

        _lastModeToggleTicks = now;

        // ConstructionCanvas uses the default Pausable process mode (unlike
        // the Always-mode Hud), so entering or leaving construction mode
        // while training is paused would leave editing half-broken: the
        // Build button stays tappable but taps/drags on the canvas itself
        // wouldn't register. Always resume first so Build reliably works.
        if (GetTree().Paused)
        {
            TogglePause();
        }

        if (Construction.IsActive)
        {
            if (!LeaveConstructionAndPersistEdits())
            {
                return;
            }
        }

        Construction.IsActive = !Construction.IsActive;
    }

    private void CompleteCreation()
    {
        _ = TryCompleteCreation(out _, out _);
    }

    private void SaveCreationFromBuild()
    {
        if (!TryCompleteCreation(out _, out var creation) || creation is null)
        {
            return;
        }

        OpenCreation(creation);
    }

    private void CompleteCreationAndSimulate()
    {
        if (!TryCompleteCreation(out var creature, out var creation) || creature is null || creation is null || _creature is null)
        {
            return;
        }
        Selection.Clear();
        Selection.Clear();
        _creature.BrainShape = creation.BrainShape;
        _creature.BuildFrom(creature);
        SetActiveInspector(creature);
        if (_seedLabel is not null)
        {
            _seedLabel.Text = SeedText();
        }

        Construction.IsActive = false;
        StartEvolution(creation);
    }

    private bool TryCompleteCreation(out CreatureDef? creature, out CreationDef? creation)
    {
        creation = null;
        if (!Construction.TryLeave(out creature, out var errors) || creature is null)
        {
            Construction.SetBlockedLeaveMessage(errors);
            return false;
        }

        var saveManager = GetNode<SaveManager>("/root/SaveManager");
        var completedCreation = saveManager.ConstructionDraftWorkflow.CompleteDraft(
            creature,
            $"Creation {saveManager.List().Count + 1}",
            Construction.HasCustomBrainShape ? Construction.BrainShape : RecommendedBrainShape(creature));
        if (TryRunFileOperation(
            () => saveManager.Save(completedCreation),
            $"Saving Creation '{completedCreation.Name}'"))
        {
            creation = completedCreation;
            _activeCreationId = creation.Id;
            if (saveManager.TryAttributeExtraCoreUnlock(creation.Id))
            {
                ApplyProgression();
                RefreshCreationsPanel();
            }

            Construction.SetCompletedMessage($"Saved {creation.Name}.");
            return true;
        }

        Construction.SetCompletedMessage("Save failed — see log.");
        return false;
    }

    private static BrainShapeDef RecommendedBrainShape(CreatureDef creature)
    {
        var motorRelationCount = MotorTopology.BuildNodeConnections(creature)
            .Count(connection => connection.IsMotorized);
        var inputCount = (creature.Cores.Count * 6) + (motorRelationCount * 2);
        var outputCount = motorRelationCount;
        return new BrainShapeDef(
            BrainShapeDef.DefaultHiddenLayers,
            Math.Clamp(
                (int)Math.Ceiling((inputCount + outputCount) / 2.0),
                BrainShapeDef.MinimumNeuronsPerLayer,
                BrainShapeDef.MaximumNeuronsPerLayer));
    }

    // Persisting reads the current Creation back off disk and writes the
    // updated one, both synchronous File IO (see #113). At a generation
    // boundary that would stall the physics thread, so the round trip runs
    // on the thread pool instead. Everything captured below is a plain value
    // snapshot (arrays, primitives, a Guid) — no Godot object ever crosses
    // onto the background thread; only SaveManager/FileCreationRepository
    // (plain C#/file APIs) are touched there.
    private void PersistActiveTraining()
    {
        if (_activeCreationId is not { } id || _evolver?.BestGenome is not { } genome || _creature?.Brain is null)
        {
            return;
        }

        var saveManager = GetNode<SaveManager>("/root/SaveManager");
        var layerSizes = _evolver.LayerSizes;
        var genomeSnapshot = genome.ToArray();
        var generation = _evolver.Generation;
        var epoch = saveManager.CurrentTrainingEpoch(id);
        var saveStatusVersion = BeginTrainingSaveStatus($"Saving generation {generation}...");

        var bestFitness = _evolver.BestFitness;
        Task.Run(() => PersistTrainingSnapshot(saveManager, id, epoch, saveStatusVersion, layerSizes, genomeSnapshot, generation, bestFitness));
    }

    private void PersistTrainingSnapshot(SaveManager saveManager, Guid id, long epoch, long saveStatusVersion, int[] layerSizes, double[] genome, int generation, double bestFitness)
    {
        try
        {
            var persisted = saveManager.TryPersistTraining(
                id,
                epoch,
                new TrainingStateDef(layerSizes, genome, generation, Activation.Tanh.ToString(), bestFitness));
            if (persisted)
            {
                CallDeferred(nameof(ShowTrainingPersisted), id.ToString(), saveStatusVersion, generation);
            }
            else
            {
                CallDeferred(nameof(ShowTrainingSaveSkipped), id.ToString(), saveStatusVersion, generation);
            }
        }
        catch (Exception ex)
        {
            // Keep the full exception (not just its message) and the id/
            // generation it failed for — this is a data-loss condition, not
            // a benign one-liner, and dev builds should fail loud (see
            // docs/CODE_DESIGN_PRINCIPLES.md § "Fail loud in dev").
            CallDeferred(nameof(LogPersistTrainingError), id.ToString(), generation, ex.ToString());
            CallDeferred(nameof(ShowTrainingSaveFailed), id.ToString(), saveStatusVersion);
        }
    }

    // Deferred back to the main thread so the log call never touches the
    // engine from the background save thread.
    private void LogPersistTrainingError(string id, int generation, string exception)
    {
        GD.PrintErr($"Failed to persist training state for Creation {id} at generation {generation}: {exception}");
    }

    private void ShowTrainingPersisted(string id, long saveStatusVersion, int generation)
    {
        if (!IsCurrentTrainingSaveStatus(id, saveStatusVersion))
        {
            return;
        }

        SetTrainingSaveStatus($"Saved generation {generation}");
    }

    private void ShowTrainingSaveSkipped(string id, long saveStatusVersion, int generation)
    {
        if (!IsCurrentTrainingSaveStatus(id, saveStatusVersion))
        {
            return;
        }

        SetTrainingSaveStatus($"Saved generation {generation}");
    }

    private void ShowTrainingSaveFailed(string id, long saveStatusVersion)
    {
        if (!IsCurrentTrainingSaveStatus(id, saveStatusVersion))
        {
            return;
        }

        SetTrainingSaveStatus("Save failed — see log");
    }

    private long BeginTrainingSaveStatus(string text)
    {
        var version = Interlocked.Increment(ref _trainingSaveStatusVersion);
        SetTrainingSaveStatus(text);
        return version;
    }

    private void ResetTrainingSaveStatus(string? text)
    {
        Interlocked.Increment(ref _trainingSaveStatusVersion);
        SetTrainingSaveStatus(text);
    }

    private bool IsCurrentTrainingSaveStatus(string id, long saveStatusVersion)
    {
        return saveStatusVersion == Interlocked.Read(ref _trainingSaveStatusVersion)
            && _activeCreationId?.ToString() == id;
    }

    private void SetTrainingSaveStatus(string? text)
    {
        if (_trainingSaveStatusLabel is null)
        {
            return;
        }

        _trainingSaveStatusLabel.Text = text ?? string.Empty;
        _trainingSaveStatusLabel.Visible = !string.IsNullOrWhiteSpace(text);
    }

    // Synchronous Creation file operations (save/duplicate/delete/reset/edit)
    // can throw on unreadable or corrupt on-disk state (#114). One bad file
    // must not crash the whole app from a button press; log and let the
    // caller treat it as a no-op instead.
    private static bool TryRunFileOperation(Action action, string description)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex) when (FilePersistenceExceptions.IsRecoverable(ex))
        {
            GD.PrintErr($"{description} failed: {ex}");
            return false;
        }
    }

    private void ApplyEditedCreature(CreatureDef editedCreature)
    {
        if (_creature is null)
        {
            return;
        }

        // Persist before ever touching the live creature (#114): if saving
        // this Creation fails, the on-disk and in-memory anatomy must stay
        // identical. Applying the edit live anyway and continuing to train
        // would mean a later periodic training save (#113) writes a genome
        // sized for the new, never-persisted anatomy onto the still-old
        // Creature record on disk -- the same resurrection-style hazard
        // #113's epoch invalidation fixed, just triggered by an I/O failure
        // instead of a race.
        ConstructionEditResult? editResult = null;
        if (_activeCreationId is { } id)
        {
            var saveManager = GetNode<SaveManager>("/root/SaveManager");
            var succeeded = TryRunFileOperation(
                () => editResult = saveManager.PersistMoveOnlyEdit(id, editedCreature),
                $"Applying creature edit for Creation {id}");

            // A null result (no exception, but nothing to update -- e.g. the
            // Creation was deleted concurrently) means nothing was actually
            // persisted either, so it must be treated the same as a thrown
            // failure: discard the edit rather than applying it live.
            if (!succeeded || editResult is null || !editResult.ShouldApplyLive)
            {
                Construction.SetCompletedMessage(editResult?.StatusMessage ?? "Could not save the edited creature; your edit was discarded.");
                return;
            }
        }

        // Clear any selection from the old creature before rebuilding the
        // inspector: CreatureInspectorViewModel reads Selection.SelectedElement
        // as soon as it's constructed, and a stale index into the old
        // definition could point past the end of (or at a different element
        // in) the newly edited one.
        Selection.Clear();
        _creature.BuildFrom(editedCreature);
        SetActiveInspector(editedCreature);
        if (_seedLabel is not null)
        {
            _seedLabel.Text = SeedText();
        }

        if (editResult?.UpdatedCreation is { } updated)
        {
            Construction.SetCompletedMessage(editResult.StatusMessage);
            StartEvolution(updated);
            return;
        }

        StartEvolution();
    }


    private void OnConstructionPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        switch (eventArgs.PropertyName)
        {
            case nameof(ConstructionViewModel.IsActive):
                if (_creature is not null)
                {
                    _creature.Visible = !Construction.IsActive;
                }

                if (_constructionCanvas is not null)
                {
                    _constructionCanvas.Visible = Construction.IsActive;
                }

                if (_arenaBackdrop is not null)
                {
                    _arenaBackdrop.Visible = !Construction.IsActive;
                }

                if (_buildModeBackdrop is not null)
                {
                    _buildModeBackdrop.Visible = Construction.IsActive;
                }

                if (_ground is not null)
                {
                    _ground.Visible = !Construction.IsActive;
                }

                if (_toolPanel is not null)
                {
                    _toolPanel.Visible = false;
                }

                if (_buildInfoPanel is not null)
                {
                    _buildInfoPanel.Visible = false;
                }

                if (_trainingPanel is not null)
                {
                    _trainingPanel.Visible = false;
                }

                if (_hudPanel is not null)
                {
                    _hudPanel.Visible = false;
                }

                if (_simulateScreen is not null)
                {
                    _simulateScreen.Visible = !Construction.IsActive;
                }

                if (_buildScreen is not null)
                {
                    _buildScreen.Visible = Construction.IsActive;
                }

                if (_buildModeButton is not null)
                {
                    _buildModeButton.Text = BuildModeButtonText();
                }

                UpdateToolButtonVisibility();
                UpdateBuildPanelPresentation();
                UpdateInspector();
                break;
            case nameof(ConstructionViewModel.ActiveTool):
                UpdateToolButtonHighlight();
                UpdateInspector();
                break;
            case nameof(ConstructionViewModel.StatusMessage):
                UpdateBuildPanelPresentation();
                UpdateInspector();
                break;
            case nameof(ConstructionViewModel.MaxCores):
                UpdateToolButtonVisibility();
                UpdateBuildPanelPresentation();
                break;
        }
    }

    private void UpdateToolButtonHighlight()
    {
        if (_placeToolButton is null || _beamToolButton is null || _coreToolButton is null || _deleteToolButton is null)
        {
            return;
        }

        HighlightToolButton(_placeToolButton, Construction.ActiveTool == ConstructionTool.Place);
        HighlightToolButton(_beamToolButton, Construction.ActiveTool == ConstructionTool.Beam);
        HighlightToolButton(_coreToolButton, Construction.ActiveTool == ConstructionTool.Core);
        HighlightToolButton(_deleteToolButton, Construction.ActiveTool == ConstructionTool.Delete);
    }

    private void UpdateToolButtonVisibility()
    {
        var presentation = ConstructionPresentation;
        if (_placeToolButton is not null)
        {
            _placeToolButton.Visible = true;
            _placeToolButton.Disabled = false;
            _placeToolButton.Text = presentation.PlaceToolText;
            _placeToolButton.TooltipText = ConstructionPresentationViewModel.ToolHint(ConstructionTool.Place);
        }
        if (_beamToolButton is not null)
        {
            _beamToolButton.Visible = true;
            _beamToolButton.Disabled = presentation.LockTopologyTools;
            _beamToolButton.Text = presentation.BeamToolText;
            _beamToolButton.TooltipText = presentation.LockTopologyTools ? presentation.MoveOnlyLockReason : ConstructionPresentationViewModel.ToolHint(ConstructionTool.Beam);
        }
        if (_coreToolButton is not null)
        {
            _coreToolButton.Visible = true;
            _coreToolButton.Disabled = presentation.LockTopologyTools;
            _coreToolButton.Text = presentation.CoreToolText;
            _coreToolButton.TooltipText = presentation.CoreToolTooltip;
        }
        if (_deleteToolButton is not null)
        {
            _deleteToolButton.Visible = true;
            _deleteToolButton.Disabled = presentation.LockTopologyTools;
            _deleteToolButton.Text = presentation.DeleteToolText;
            _deleteToolButton.TooltipText = presentation.LockTopologyTools ? presentation.MoveOnlyLockReason : ConstructionPresentationViewModel.ToolHint(ConstructionTool.Delete);
        }
        if (_editLockReasonLabel is not null)
        {
            _editLockReasonLabel.Visible = presentation.LockTopologyTools;
            _editLockReasonLabel.Text = presentation.LockedTopologyToolsText;
        }
        if (_completeButton is not null)
        {
            _completeButton.Visible = presentation.ShowCompleteAction;
        }
        if (_rebuildButton is not null)
        {
            _rebuildButton.Visible = presentation.ShowRebuildAction;
            _rebuildButton.Text = presentation.RebuildActionText;
            _rebuildButton.TooltipText = presentation.RebuildConfirmationBody;
        }
        if (_buildInfoPanel is not null)
        {
            _buildInfoPanel.Visible = false;
        }
    }

    private void UpdateBuildPanelPresentation()
    {
        var buildPanel = ConstructionPresentation.BuildPanel;
        if (_buildInputSummaryLabel is not null)
        {
            SetLabelTextIfChanged(_buildInputSummaryLabel, buildPanel.InputSummary);
        }
        if (_buildMotorSummaryLabel is not null)
        {
            SetLabelTextIfChanged(_buildMotorSummaryLabel, buildPanel.MotorRelationSummary);
        }
        if (_buildValidationLabel is not null)
        {
            SetLabelTextIfChanged(_buildValidationLabel, buildPanel.ValidationLine);
            _buildValidationLabel.AddThemeColorOverride("font_color", buildPanel.CanStartTraining ? _theme.GroundEdge : Colors.Orange);
        }
        if (_completeButton is not null)
        {
            _completeButton.Disabled = !buildPanel.CanCompleteCreation;
            _completeButton.TooltipText = buildPanel.CanCompleteCreation
                ? "Save this anatomy as a new Creation."
                : buildPanel.DisabledReason ?? "Finish the anatomy before saving.";
        }
    }

    private void HighlightToolButton(Button button, bool isActive)
    {
        button.AddThemeColorOverride("font_color", isActive ? _theme.SelectionGlow : _theme.GroundEdge);
    }

    private string BuildModeButtonText()
    {
        return ConstructionPresentation.BuildModeButtonText;
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (Construction.IsActive)
        {
            return;
        }

        if (!PointerInput.TryGetPressPosition(inputEvent, out var screenPosition))
        {
            return;
        }

        var worldPosition = GetGlobalTransformWithCanvas().AffineInverse() * screenPosition;
        if (_creature is not null && _creature.TrySelectPart(worldPosition, out var selection) && selection is not null)
        {
            Selection.Select(selection);
        }
        else
        {
            Selection.Clear();
        }

        GetViewport().SetInputAsHandled();
    }

    public override void _ExitTree()
    {
        // Restore the global time scale so it doesn't leak into whatever
        // runs next (another scene, a future scene reload, tests).
        Engine.TimeScale = _timeScales[0];
        Selection.PropertyChanged -= OnSelectionPropertyChanged;
        Construction.PropertyChanged -= OnConstructionPropertyChanged;
        Construction.AnatomyChanged -= OnConstructionAnatomyChanged;
        if (_inspector is not null)
        {
            _inspector.PropertyChanged -= OnInspectorPropertyChanged;
        }

        _inspector?.Dispose();
    }

    private void OnSelectionPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(SelectionViewModel.SelectedElement))
        {
            _creature?.SetSelectedElement(Selection.SelectedElement);
            _showMapping = Selection.SelectedElement is null;
            if (_mappingToggleButton is not null)
            {
                _mappingToggleButton.Text = MappingToggleButtonText();
            }

            UpdateInspector();
        }
    }

    private void OnInspectorPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        UpdateInspector();
    }

    private void AddInspector()
    {
        var layer = new CanvasLayer
        {
            Name = "Inspector",
        };

        var panel = new PanelContainer
        {
            AnchorsPreset = (int)Control.LayoutPreset.BottomWide,
            // Reserve less of the screen than before (was 0.64, which,
            // combined with the camera framing, covered the creature's
            // resting position). Paired with the camera Y in AddCamera().
            AnchorTop = 0.78f,
            AnchorRight = 1,
            AnchorBottom = 1,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Begin,
        };
        panel.AddThemeStyleboxOverride("panel", CreateHudPanelStyle());

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_bottom", 14);

        var outer = new VBoxContainer();
        outer.AddThemeConstantOverride("separation", 6);

        _mappingToggleButton = new Button
        {
            Name = "MappingToggleButton",
            Text = MappingToggleButtonText(),
            CustomMinimumSize = new Vector2(0, _touchTargetHeight),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
        };
        _mappingToggleButton.AddThemeColorOverride("font_color", _theme.GroundEdge);
        _mappingToggleButton.AddThemeFontSizeOverride("font_size", _hudFontSize);
        _mappingToggleButton.Pressed += ToggleMappingView;
        outer.AddChild(_mappingToggleButton);

        // The Mapping view (#42) can have far more lines than Inspector's
        // 3 (one core alone is 6 sensor lines). A ScrollContainer with a
        // fixed height keeps the panel's footprint constant instead of
        // growing over the HUD/scene above it — see #42 review.
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 130),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 6);
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _inspectorTitle = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _inspectorTitle.AddThemeColorOverride("font_color", _theme.SelectionGlow);
        _inspectorTitle.AddThemeFontSizeOverride("font_size", _hudFontSize);
        _inspectorRole = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _inspectorRole.AddThemeColorOverride("font_color", _theme.GroundEdge);
        _inspectorRole.AddThemeFontSizeOverride("font_size", _hudFontSize);
        _inspectorValues = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _inspectorValues.AddThemeColorOverride("font_color", _theme.Beam);
        _inspectorValues.AddThemeFontSizeOverride("font_size", _hudFontSize);
        content.AddChild(_inspectorTitle);
        content.AddChild(_inspectorRole);
        content.AddChild(_inspectorValues);
        scroll.AddChild(content);
        outer.AddChild(scroll);
        margin.AddChild(outer);
        panel.AddChild(margin);
        layer.AddChild(panel);
        AddChild(layer);
        _inspectorPanel = panel;
        UpdateInspector();
    }

    private void UpdateInspector()
    {
        if (_inspectorTitle is null || _inspectorRole is null || _inspectorValues is null)
        {
            return;
        }

        if (_mappingToggleButton is not null)
        {
            _mappingToggleButton.Visible = !Construction.IsActive;
        }
        if (_inspectorPanel is not null)
        {
            _inspectorPanel.Visible = false;
        }

        if (Construction.IsActive)
        {
            var presentation = ConstructionPresentation;
            _inspectorTitle.Text = presentation.InspectorTitle;
            _inspectorRole.Text = presentation.InspectorRole;
            _inspectorValues.Text = presentation.InspectorValues;
            return;
        }

        if (_showMapping)
        {
            SetLabelTextIfChanged(_inspectorTitle, "Sensor \u2192 motor mapping");
            SetLabelTextIfChanged(_inspectorRole, _mapping.SensorsText);
            SetLabelTextIfChanged(_inspectorValues, _mapping.OutputsText);
            return;
        }

        if (_inspector is null)
        {
            return;
        }

        _inspectorTitle.Text = _inspector.Title;
        _inspectorRole.Text = _inspector.Role;
        _inspectorValues.Text = _inspector.Values;
    }

    // Godot's Label.Text setter re-triggers layout/redraw even when the
    // assigned string is identical, which matters here since the mapping
    // view reassigns these labels on every refresh tick (see _Process).
    private static void SetLabelTextIfChanged(Label label, string text)
    {
        if (label.Text != text)
        {
            label.Text = text;
        }
    }

    // Manual override of the #42 mapping/#41 inspector auto-switch (see
    // OnSelectionPropertyChanged) — lets the user check the mapping even
    // while something is selected, or vice versa.
    private void ToggleMappingView()
    {
        _showMapping = !_showMapping;
        if (_mappingToggleButton is not null)
        {
            _mappingToggleButton.Text = MappingToggleButtonText();
        }

        UpdateInspector();
    }

    private string MappingToggleButtonText() => _showMapping ? "Inspector" : "Mapping";

    private string SeedText()
    {
        return $"Seed: {RngProvider().Seed}";
    }

    private StyleBoxFlat CreateHudPanelStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = _theme.ArenaBackground with { A = 0.82f },
            BorderColor = _theme.GroundEdge,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 10,
            ContentMarginTop = 8,
            ContentMarginRight = 10,
            ContentMarginBottom = 8,
        };
    }
}
