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
    private Button? _buildModeButton;
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
    private Button? _creationsButton;
    private PanelContainer? _creationsPanel;
    private VBoxContainer? _creationsList;
    private CreationsScreen? _creationsScreen;
    private ConfirmationDialog? _deleteCreationConfirmationDialog;
    private Guid? _pendingDeleteCreationId;
    private string? _pendingDeleteCreationName;
    private Guid? _activeCreationId;
    private Label? _seedLabel;
    private Label? _generationLabel;
    private Label? _bestFitnessLabel;
    private Label? _meanFitnessLabel;
    private Button? _pauseButton;
    private Button? _timeScaleButton;
    private Button? _trainingProfileButton;
    private Label? _trainingProfileSummaryLabel;
    private Label? _progressionLabel;
    private PanelContainer? _trainingPanel;
    private GenerationStrip? _generationStrip;
    private int _bestGeneration;
    private int _timeScaleIndex;
    private Label? _inspectorTitle;
    private Label? _inspectorRole;
    private Label? _inspectorValues;
    private readonly MappingViewModel _mapping = new();
    private readonly List<SensorReading> _sensorReadings = [];
    private readonly List<MotorReading> _motorReadings = [];
    private Button? _mappingToggleButton;

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
    private readonly TrainingPresentationViewModel _trainingPresentation = new();

    public SelectionViewModel Selection { get; } = new();

    public ConstructionViewModel Construction { get; } = new();

    private ConstructionPresentationViewModel ConstructionPresentation => new(Construction);

    public override void _Ready()
    {
        if (ProjectSettings.GetSetting("ui/component_gallery", false).AsBool())
        {
            var gallery = GD.Load<PackedScene>("res://scenes/ui/ComponentGalleryScreen.tscn").Instantiate<ComponentGalleryScreen>();
            AddChild(gallery);
            return;
        }

        if (ProjectSettings.GetSetting("ui/sample_preview", false).AsBool())
        {
            _trainingPresentation.Update(5, 3, 8, 12.8, 8.4, "Quick");
            var sample = GD.Load<PackedScene>("res://scenes/ui/SampleFlowScreen.tscn").Instantiate<SampleFlowScreen>();
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
        AddBackdrop();
        AddGround();
        AddCamera();
        AddCreature();
        AddConstructionCanvas();
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
        if (saveManager.UnlockExtraCore(_evolver.Generation))
        {
            ApplyProgression();
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
        if (!_showMapping || Construction.IsActive || _creature is null)
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
        _mapping.Update(_sensorReadings, _motorReadings);
        UpdateInspector();
    }

    private void AddBackdrop()
    {
        AddChild(new ArenaBackdrop
        {
            Name = "ArenaBackdrop",
            Theme = _theme,
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
        var scene = GD.Load<PackedScene>("res://scenes/Creature.tscn");
        var creature = scene.Instantiate<Creature.Creature>();
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

    // 0.4.0 first training slice (#50): evolves a small population of
    // brains for the current creature via GeneticAlgorithm, one generation
    // after another. The training HUD (#51) binds to GenerationCompleted /
    // NewBestFound below.
    private void AddEvolver()
    {
        var evolver = new Evolver { Name = "Evolver" };
        // Explicitly Pausable (not the default Inherit) so it doesn't pick
        // up Main's Always process mode (#85) and keep training while the
        // scene tree is paused.
        evolver.ProcessMode = ProcessModeEnum.Pausable;
        evolver.GenerationCompleted += OnGenerationCompleted;
        evolver.NewBestFound += OnNewBestFound;
        // Temporary composition-root bridge until the Watch ViewModel seam
        // is extracted in the UI implementation plan.
        evolver.TrainingProgressChanged += OnTrainingProgressChanged;
        AddChild(evolver);
        _evolver = evolver;
        StartEvolution();
    }

    private void StartEvolution()
    {
        _evolver?.Stop();
        _bestGeneration = 0;
        if (_creature?.Brain is null || _evolver is null)
        {
            // No motors (e.g. a just-cleared construction-mode anatomy) —
            // nothing to evolve.
            return;
        }

        var profile = CurrentTrainingProfile();
        _sessionGenerationStart = 0;
        var ga = CreateGeneticAlgorithm(profile);
        _evolver.Start(_creature, profile.PopulationSize, _creature.Brain.LayerSizes, ga, RngProvider().Random, trialDurationTicks: profile.TrialDurationTicks);
        UpdateTrainingLabels();
    }

    private void StartEvolution(CreationDef creation)
    {
        _evolver?.Stop();
        _bestGeneration = creation.Training?.Generation ?? 0;
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
            profile.TrialDurationTicks);
        UpdateTrainingLabels();
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

    // Records which generation produced the current all-time best, for the
    // "Best" HUD label. Evolver doesn't retain a replayable per-genome seed
    // today (see issue #51's scope decision), so this is the closest
    // reproducible pointer to "where the best came from."
    private void OnNewBestFound()
    {
        _bestGeneration = _evolver!.Generation;
        TryUnlockProgression();
        // GenerationCompleted (which also calls UpdateTrainingLabels) fires
        // before NewBestFound, so the "Best" label would otherwise render
        // with the previous _bestGeneration on the very generation the new
        // best was found. Refresh again now that it's current.
        UpdateTrainingLabels();
    }

    private void OnTrainingProgressChanged()
    {
        UpdateTrainingLabels();
        if (_evolver is not null)
        {
            _trainingPresentation.Update(
                _evolver.Generation,
                _evolver.CurrentCandidate,
                _evolver.PopulationSize,
                _evolver.BestFitness,
                _evolver.MeanFitness,
                _trainingProfiles[_trainingProfileIndex].Name);
        }
    }

    private void UpdateTrainingLabels()
    {
        if (_evolver is null)
        {
            return;
        }

        if (_generationLabel is not null)
        {
            _generationLabel.Text = _evolver.IsTrialActive
                ? $"Generation {_evolver.Generation} · try {_evolver.CurrentCandidate} of {_evolver.PopulationSize}"
                : $"Generation {_evolver.Generation} · session complete";
        }

        if (_bestFitnessLabel is not null)
        {
            _bestFitnessLabel.Text = double.IsNegativeInfinity(_evolver.BestFitness)
                ? "Best: —"
                : $"Best: {_evolver.BestFitness:0.0} (gen {_bestGeneration})";
        }

        if (_meanFitnessLabel is not null)
        {
            _meanFitnessLabel.Text = $"Mean: {_evolver.MeanFitness:0.0}";
        }

        if (_progressionLabel is not null)
        {
            _progressionLabel.Text = ProgressionText();
        }

        _generationStrip?.SetProgress(
            _evolver.Generation,
            _evolver.CurrentCandidate,
            _evolver.PopulationSize,
            _evolver.CompletedCandidateCount,
            _evolver.CompletedFitness,
            _evolver.IsTrialActive);
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
        };
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
            Visible = false,
        };
        _creationsScreen.Setup(saveManager.CreationsPresentation);
        _creationsScreen.BackRequested += () => _creationsScreen.Visible = false;
        _creationsScreen.OpenRequested += OpenCreationFromScreen;
        _creationsScreen.EditRequested += EditCreationFromScreen;
        _creationsScreen.DuplicateRequested += DuplicateCreationFromScreen;
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

        RefreshCreationsPanel();
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
            OpenCreation(creation);
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

    private void DuplicateCreationFromScreen(string creationKey, string creationName)
    {
        if (!Guid.TryParse(creationKey, out var id))
        {
            GD.PrintErr($"Could not duplicate Creation '{creationName}': invalid id '{creationKey}'.");
            return;
        }

        var saveManager = GetNode<SaveManager>("/root/SaveManager");
        TryRunFileOperation(
            () => saveManager.Duplicate(id),
            $"Duplicating Creation '{creationName}'");
        RefreshCreationsPanel();
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

        _deleteCreationConfirmationDialog.DialogText = $"Delete {creationName}? This permanently removes this saved Creation.";
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
        TryRunFileOperation(
            () => saveManager.Delete(id),
            $"Deleting Creation '{name}'");
        RefreshCreationsPanel();
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
        Construction.Load(creation.Creature, moveOnly: true);
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
        if (_creature is null)
        {
            return;
        }

        Selection.Clear();
        _activeCreationId = creation.Id;
        _creature.BuildFrom(creation.Creature);
        Construction.Load(creation.Creature);
        SetActiveInspector(creation.Creature);
        StartEvolution(creation);
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
        column.AddChild(statsRow);
        column.AddChild(_generationStrip);
        column.AddChild(controlsRow);
        column.AddChild(_trainingProfileSummaryLabel);
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
        var crossover = profile.CrossoverStrategy == CrossoverStrategy.Blend ? "blended genes" : "uniform genes";
        return $"{profile.PopulationSize} candidates | {profile.TrialDurationTicks / 60}s trials | {profile.MutationRate:P0} mutation | {crossover}";
    }

    private void CycleTrainingProfile()
    {
        _trainingProfileIndex = (_trainingProfileIndex + 1) % _trainingProfiles.Length;
        if (_trainingProfileButton is not null)
        {
            _trainingProfileButton.Text = TrainingProfileButtonText();
        }

        if (_trainingProfileSummaryLabel is not null)
        {
            _trainingProfileSummaryLabel.Text = TrainingProfileSummaryText();
        }

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
            new TrainingStateDef(_evolver.LayerSizes, genome.ToArray(), _evolver.Generation, Activation.Tanh.ToString()));
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
            if (!Construction.TryLeave(out var editedCreature, out var errors))
            {
                Construction.SetBlockedLeaveMessage(errors);
                return;
            }

            if (editedCreature is not null)
            {
                ApplyEditedCreature(editedCreature);
            }
        }

        Construction.IsActive = !Construction.IsActive;
    }

    private void CompleteCreation()
    {
        if (!Construction.TryLeave(out var creature, out var errors) || creature is null)
        {
            Construction.SetBlockedLeaveMessage(errors);
            return;
        }

        var saveManager = GetNode<SaveManager>("/root/SaveManager");
        var creation = saveManager.ConstructionDraftWorkflow.CompleteDraft(
            creature,
            $"Creation {saveManager.List().Count + 1}");
        if (TryRunFileOperation(
            () => saveManager.Save(creation),
            $"Saving Creation '{creation.Name}'"))
        {
            _activeCreationId = creation.Id;
            Construction.SetCompletedMessage($"Saved {creation.Name}.");
        }
        else
        {
            Construction.SetCompletedMessage("Save failed — see log.");
        }
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

        Task.Run(() => PersistTrainingSnapshot(saveManager, id, epoch, layerSizes, genomeSnapshot, generation));
    }

    private void PersistTrainingSnapshot(SaveManager saveManager, Guid id, long epoch, int[] layerSizes, double[] genome, int generation)
    {
        try
        {
            saveManager.TryPersistTraining(
                id,
                epoch,
                new TrainingStateDef(layerSizes, genome, generation, Activation.Tanh.ToString()));
        }
        catch (Exception ex)
        {
            // Keep the full exception (not just its message) and the id/
            // generation it failed for — this is a data-loss condition, not
            // a benign one-liner, and dev builds should fail loud (see
            // docs/CODE_DESIGN_PRINCIPLES.md § "Fail loud in dev").
            CallDeferred(nameof(LogPersistTrainingError), id.ToString(), generation, ex.ToString());
        }
    }

    // Deferred back to the main thread so the log call never touches the
    // engine from the background save thread.
    private void LogPersistTrainingError(string id, int generation, string exception)
    {
        GD.PrintErr($"Failed to persist training state for Creation {id} at generation {generation}: {exception}");
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

                if (_toolPanel is not null)
                {
                    _toolPanel.Visible = Construction.IsActive;
                }

                if (_buildInfoPanel is not null)
                {
                    _buildInfoPanel.Visible = Construction.IsActive && !Construction.IsMoveOnly;
                }

                if (_trainingPanel is not null)
                {
                    _trainingPanel.Visible = !Construction.IsActive;
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
            _buildInfoPanel.Visible = Construction.IsActive && !Construction.IsMoveOnly;
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
