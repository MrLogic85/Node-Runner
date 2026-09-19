using System.ComponentModel;
using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Creature;
using NodeRunner.Domain;
using NodeRunner.Managers;
using NodeRunner.ML.Ga;
using NodeRunner.Sim;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Widgets;

namespace NodeRunner;

public partial class Main : Node2D
{
    // Population size and GA hyperparameters for the 0.4.0 first-training
    // slice (issue #50). Tuned for a short, teachable demo, not for a
    // fast/strong result — revisit once a training HUD (#51) makes tuning
    // observable.
    private const int _populationSize = 8;
    private const int _tournamentSize = 3;
    private const double _mutationRate = 0.1;
    private const double _mutationStrength = 0.3;

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
    private Label? _seedLabel;
    private Label? _generationLabel;
    private Label? _bestFitnessLabel;
    private Label? _meanFitnessLabel;
    private Button? _pauseButton;
    private Button? _timeScaleButton;
    private PanelContainer? _trainingPanel;
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

    public SelectionViewModel Selection { get; } = new();

    public ConstructionViewModel Construction { get; } = new();

    public override void _Ready()
    {
        // Engine.TimeScale is a global engine setting, not scoped to this
        // scene — reset it on entry so a previous run's time-scale choice
        // (e.g. from CycleTimeScale) can't silently carry over.
        Engine.TimeScale = _timeScales[0];
        Selection.PropertyChanged += OnSelectionPropertyChanged;
        Construction.PropertyChanged += OnConstructionPropertyChanged;
        AddBackdrop();
        AddGround();
        AddCamera();
        AddCreature();
        AddConstructionCanvas();
        AddHud();
        AddInspector();
        AddEvolver();
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
        evolver.GenerationCompleted += OnGenerationCompleted;
        evolver.NewBestFound += OnNewBestFound;
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

        var ga = new GeneticAlgorithm(_tournamentSize, _mutationRate, _mutationStrength);
        _evolver.Start(_creature, _populationSize, _creature.Brain.LayerSizes, ga, RngProvider().Random);
        UpdateTrainingLabels();
    }

    private void OnGenerationCompleted()
    {
        GD.Print($"Generation {_evolver!.Generation} — best: {_evolver.BestFitness:0.0}, mean: {_evolver.MeanFitness:0.0}");
        UpdateTrainingLabels();
    }

    // Records which generation produced the current all-time best, for the
    // "Best" HUD label. Evolver doesn't retain a replayable per-genome seed
    // today (see issue #51's scope decision), so this is the closest
    // reproducible pointer to "where the best came from."
    private void OnNewBestFound()
    {
        _bestGeneration = _evolver!.Generation;
        // GenerationCompleted (which also calls UpdateTrainingLabels) fires
        // before NewBestFound, so the "Best" label would otherwise render
        // with the previous _bestGeneration on the very generation the new
        // best was found. Refresh again now that it's current.
        UpdateTrainingLabels();
    }

    private void UpdateTrainingLabels()
    {
        if (_evolver is null)
        {
            return;
        }

        if (_generationLabel is not null)
        {
            _generationLabel.Text = $"Gen: {_evolver.Generation}";
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
    }

    private RngProvider RngProvider() => GetNode<RngProvider>("/root/RngProvider");

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
        panel.AddChild(row);
        layer.AddChild(panel);
        AddChild(layer);

        AddConstructionToolRow(layer);
        AddTrainingPanel(layer);
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
        statsRow.AddChild(_generationLabel);
        statsRow.AddChild(_bestFitnessLabel);
        statsRow.AddChild(_meanFitnessLabel);

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

        controlsRow.AddChild(_pauseButton);
        controlsRow.AddChild(resetButton);
        controlsRow.AddChild(_timeScaleButton);

        column.AddChild(statsRow);
        column.AddChild(controlsRow);
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
    private void ResetEvolution() => RandomizeCreatureBrain();

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

    private void AddConstructionToolRow(CanvasLayer layer)
    {
        var panel = new PanelContainer
        {
            Position = new Vector2(16, 16 + _touchTargetHeight + 12),
            Visible = false,
        };
        panel.AddThemeStyleboxOverride("panel", CreateHudPanelStyle());

        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(800, _touchTargetHeight),
        };
        row.AddThemeConstantOverride("separation", 20);

        _placeToolButton = CreateToolButton("PlaceToolButton", "Place");
        _placeToolButton.Pressed += () => Construction.ActiveTool = ConstructionTool.Place;

        _beamToolButton = CreateToolButton("BeamToolButton", "Beam");
        _beamToolButton.Pressed += () => Construction.ActiveTool = ConstructionTool.Beam;

        _coreToolButton = CreateToolButton("CoreToolButton", "Core");
        _coreToolButton.Pressed += () => Construction.ActiveTool = ConstructionTool.Core;

        _deleteToolButton = CreateToolButton("DeleteToolButton", "Delete");
        _deleteToolButton.Pressed += () => Construction.ActiveTool = ConstructionTool.Delete;

        row.AddChild(_placeToolButton);
        row.AddChild(_beamToolButton);
        row.AddChild(_coreToolButton);
        row.AddChild(_deleteToolButton);
        panel.AddChild(row);
        layer.AddChild(panel);

        _toolPanel = panel;
        UpdateToolButtonHighlight();
    }

    private Button CreateToolButton(string name, string text)
    {
        var button = new Button
        {
            Name = name,
            Text = text,
            CustomMinimumSize = new Vector2(180, _touchTargetHeight),
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

    private void ApplyEditedCreature(CreatureDef editedCreature)
    {
        if (_creature is null)
        {
            return;
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

        // The anatomy just changed shape entirely (different sensor/motor
        // counts), so any evolution in progress was measuring a creature
        // that no longer exists in this form. Start a fresh population
        // sized for the new anatomy.
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

                if (_trainingPanel is not null)
                {
                    _trainingPanel.Visible = !Construction.IsActive;
                }

                if (_buildModeButton is not null)
                {
                    _buildModeButton.Text = BuildModeButtonText();
                }

                UpdateInspector();
                break;
            case nameof(ConstructionViewModel.ActiveTool):
                UpdateToolButtonHighlight();
                UpdateInspector();
                break;
            case nameof(ConstructionViewModel.StatusMessage):
                UpdateInspector();
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

    private void HighlightToolButton(Button button, bool isActive)
    {
        button.AddThemeColorOverride("font_color", isActive ? _theme.SelectionGlow : _theme.GroundEdge);
    }

    private string BuildModeButtonText()
    {
        return Construction.IsActive ? "Simulate" : "Build";
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
            _inspectorTitle.Text = "Building";
            _inspectorRole.Text = $"Tool: {Construction.ActiveTool}";
            _inspectorValues.Text = Construction.StatusMessage ?? ConstructionToolHint(Construction.ActiveTool);
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

    private static string ConstructionToolHint(ConstructionTool tool)
    {
        return tool switch
        {
            ConstructionTool.Place => "Tap empty space to place a node. Drag a node to move it.",
            ConstructionTool.Beam => "Tap a node, then another node, to connect them with a beam.",
            ConstructionTool.Core => "Tap a node to attach a core, tap again to remove it.",
            ConstructionTool.Delete => "Tap a node or beam to delete it.",
            _ => string.Empty,
        };
    }

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
