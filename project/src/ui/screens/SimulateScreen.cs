using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// Simulate shell bound to presentation data only. This scene does not bind to
/// simulation, managers, or persistence.
/// </summary>
public partial class SimulateScreen : Control
{
    private const int _topBarHeight = 64;
    private const int _modeSwitchHeight = 52;
    private const int _signalPanelWidth = 340;
    private UiTokens _tokens = UiTokens.Neon;
    private readonly List<UiPanel> _signalCards = new();
    private readonly List<Label> _signalBodies = new();
    private readonly List<ProgressBar> _sensorBars = new();
    private readonly List<ProgressBar> _motorBars = new();
    private readonly List<Control> _inputPassthroughExceptions = new();
    private int _selectedSignalIndex = -1;
    private TrainingPresentationViewModel? _presentation;
    private SignalFlowPresentationViewModel? _signalFlow;
    private UnlockProgressPresentationViewModel? _unlockProgress;
    private TrainingProfileSummaryPresentationViewModel? _profileSummary;
    private TrainingProfileSettingsPresentationViewModel? _profileSettings;
    private Label? _seesStatusLabel;
    private Label? _decidesStatusLabel;
    private Label? _twistsStatusLabel;
    private Label? _scoresStatusLabel;
    private Control? _settingsOverlay;
    private ColorRect? _settingsScrim;
    private UiSheet? _settingsSheet;
    private Control? _buildModeSegment;
    private UiActionButton? _livePauseButton;
    private bool _inputPassthrough;
    private string _pauseActionText = "Pause";

    [Signal]
    public delegate void BrainFocusRequestedEventHandler();

    [Signal]
    public delegate void TrainingProfileRequestedEventHandler();

    [Signal]
    public delegate void TrainingProfileSelectedEventHandler(int index);

    [Signal]
    public delegate void CreationsRequestedEventHandler();

    [Signal]
    public delegate void BuildRequestedEventHandler();

    [Signal]
    public delegate void PauseRequestedEventHandler();

    [Signal]
    public delegate void SpeedRequestedEventHandler();

    [Signal]
    public delegate void ResetRequestedEventHandler();

    [Export]
    public bool ShowTopBar { get; set; } = true;

    [Export]
    public bool Hosted { get; set; }

    [Export]
    public bool ShowArenaPlaceholder { get; set; } = true;

    [Export]
    public bool ReadOnlyControls { get; set; }

    [Export]
    public bool InputPassthrough
    {
        get => _inputPassthrough;
        set
        {
            _inputPassthrough = value;
            if (IsInsideTree())
            {
                if (_inputPassthrough)
                {
                    ApplyInputPassthrough(this);
                }
                else
                {
                    RebuildLayout();
                }
            }
        }
    }

    public TrainingPresentationViewModel? Presentation
    {
        get => _presentation;
        set
        {
            if (_presentation is not null)
            {
                _presentation.PropertyChanged -= OnPresentationChanged;
            }
            _presentation = value;
            if (_presentation is not null)
            {
                _presentation.PropertyChanged += OnPresentationChanged;
            }

            if (IsInsideTree())
            {
                RebuildLayout();
            }
        }
    }

    public SignalFlowPresentationViewModel? SignalFlow
    {
        get => _signalFlow;
        set
        {
            if (_signalFlow is not null)
            {
                _signalFlow.PropertyChanged -= OnSignalFlowChanged;
            }

            _signalFlow = value;
            if (_signalFlow is not null)
            {
                _signalFlow.PropertyChanged += OnSignalFlowChanged;
            }

            if (IsInsideTree())
            {
                RebuildLayout();
            }
        }
    }

    public UnlockProgressPresentationViewModel? UnlockProgress
    {
        get => _unlockProgress;
        set
        {
            if (_unlockProgress is not null)
            {
                _unlockProgress.PropertyChanged -= OnUnlockProgressChanged;
            }

            _unlockProgress = value;
            if (_unlockProgress is not null)
            {
                _unlockProgress.PropertyChanged += OnUnlockProgressChanged;
            }

            if (IsInsideTree())
            {
                RebuildLayout();
            }
        }
    }

    public TrainingProfileSummaryPresentationViewModel? ProfileSummary
    {
        get => _profileSummary;
        set
        {
            if (_profileSummary is not null)
            {
                _profileSummary.PropertyChanged -= OnProfileSummaryChanged;
            }

            _profileSummary = value;
            if (_profileSummary is not null)
            {
                _profileSummary.PropertyChanged += OnProfileSummaryChanged;
            }

            if (IsInsideTree())
            {
                RebuildLayout();
            }
        }
    }

    public TrainingProfileSettingsPresentationViewModel? ProfileSettings
    {
        get => _profileSettings;
        set
        {
            if (_profileSettings is not null)
            {
                _profileSettings.PropertyChanged -= OnProfileSettingsChanged;
            }

            _profileSettings = value;
            if (_profileSettings is not null)
            {
                _profileSettings.PropertyChanged += OnProfileSettingsChanged;
            }

            if (_settingsOverlay?.Visible == true)
            {
                ShowTrainingSettingsSheet();
            }
        }
    }

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            if (IsInsideTree())
            {
                RebuildLayout();
            }
        }
    }

    public string PauseActionText
    {
        get => _pauseActionText;
        set
        {
            _pauseActionText = string.IsNullOrWhiteSpace(value) ? "Pause" : value;
            if (_livePauseButton is not null)
            {
                _livePauseButton.LabelText = _pauseActionText;
            }
        }
    }

    public override void _Ready()
    {
        Name = nameof(SimulateScreen);
        if (_presentation is not null)
        {
            _presentation.PropertyChanged -= OnPresentationChanged;
            _presentation.PropertyChanged += OnPresentationChanged;
        }
        if (_signalFlow is not null)
        {
            _signalFlow.PropertyChanged -= OnSignalFlowChanged;
            _signalFlow.PropertyChanged += OnSignalFlowChanged;
        }
        if (_unlockProgress is not null)
        {
            _unlockProgress.PropertyChanged -= OnUnlockProgressChanged;
            _unlockProgress.PropertyChanged += OnUnlockProgressChanged;
        }
        if (_profileSummary is not null)
        {
            _profileSummary.PropertyChanged -= OnProfileSummaryChanged;
            _profileSummary.PropertyChanged += OnProfileSummaryChanged;
        }
        if (_profileSettings is not null)
        {
            _profileSettings.PropertyChanged -= OnProfileSettingsChanged;
            _profileSettings.PropertyChanged += OnProfileSettingsChanged;
        }
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (!Hosted)
        {
            Size = GetViewportRect().Size;
        }

        RebuildLayout();
        ApplyInputPassthrough(this);
    }

    public override void _ExitTree()
    {
        if (_presentation is not null)
        {
            _presentation.PropertyChanged -= OnPresentationChanged;
        }
        if (_signalFlow is not null)
        {
            _signalFlow.PropertyChanged -= OnSignalFlowChanged;
        }
        if (_unlockProgress is not null)
        {
            _unlockProgress.PropertyChanged -= OnUnlockProgressChanged;
        }
        if (_profileSummary is not null)
        {
            _profileSummary.PropertyChanged -= OnProfileSummaryChanged;
        }
        if (_profileSettings is not null)
        {
            _profileSettings.PropertyChanged -= OnProfileSettingsChanged;
        }
    }

    private void OnPresentationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (IsInsideTree())
        {
            RebuildLayout();
        }
    }

    private void OnSignalFlowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (IsInsideTree())
        {
            UpdateSignalFlowCards();
        }
    }

    private void OnUnlockProgressChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (IsInsideTree())
        {
            RebuildLayout();
        }
    }

    private void OnProfileSummaryChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (IsInsideTree())
        {
            RebuildLayout();
        }
    }

    private void OnProfileSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (_settingsOverlay?.Visible == true)
        {
            ShowTrainingSettingsSheet();
        }
    }

    private void RebuildLayout()
    {
        var reopenSettings = _settingsOverlay?.Visible == true;
        _signalCards.Clear();
        _signalBodies.Clear();
        _sensorBars.Clear();
        _motorBars.Clear();
        _inputPassthroughExceptions.Clear();
        _seesStatusLabel = null;
        _decidesStatusLabel = null;
        _twistsStatusLabel = null;
        _scoresStatusLabel = null;
        _buildModeSegment = null;
        _livePauseButton = null;
        _selectedSignalIndex = -1;
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        BuildLayout();
        if (reopenSettings)
        {
            ShowTrainingSettingsSheet();
        }
    }

    private void BuildLayout()
    {
        if (ShowArenaPlaceholder)
        {
            AddChild(new ColorRect
            {
                Color = _tokens.Background,
                MouseFilter = MouseFilterEnum.Ignore,
                AnchorRight = 1,
                AnchorBottom = 1,
            });
        }

        var safeFrame = CreateMargin(16);
        AddChild(safeFrame);

        var screen = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        screen.AddThemeConstantOverride("separation", 8);
        safeFrame.AddChild(screen);

        if (ShowTopBar)
        {
            screen.AddChild(CreateTopBar());
        }

        var contentRow = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        contentRow.AddThemeConstantOverride("separation", 8);
        screen.AddChild(contentRow);

        contentRow.AddChild(CreateArenaPanel());
        contentRow.AddChild(CreateSignalPanel());

        if (ReadOnlyControls)
        {
            screen.AddChild(CreateLiveControlRow());
        }
        else
        {
            screen.AddChild(CreateTrainingPanel());
        }

        AddSettingsOverlay();
        ApplyInputPassthrough(this);
    }

    private Control CreateTopBar()
    {
        var panel = CreatePanel(raised: true);
        panel.CustomMinimumSize = new Vector2(0, _topBarHeight);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.GuiInput += OnTopBarInput;
        _inputPassthroughExceptions.Add(panel);

        var margin = CreateMargin(0);
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 0);
        panel.AddChild(margin);

        var topBar = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, _topBarHeight),
        };
        topBar.AddThemeConstantOverride("separation", 8);
        margin.AddChild(topBar);

        var title = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        title.AddThemeConstantOverride("separation", 0);
        topBar.AddChild(title);
        title.AddChild(CreateLabel("Simulate", 22, _tokens.Ink, expand: true));
        title.AddChild(CreateLabel(_presentation?.GenerationText ?? "Training", 14, _tokens.Muted));

        var creations = CreateButton("Creations", UiActionButton.ActionKind.Secondary, "Open saved Creations");
        creations.Pressed += () => EmitSignal(SignalName.CreationsRequested);
        _inputPassthroughExceptions.Add(creations);
        topBar.AddChild(creations);

        topBar.AddChild(CreateModeSwitch());

        var settings = new UiIconButton
        {
            Tokens = _tokens,
            IconId = UiIconId.More,
            AccessibleLabel = "Training settings",
        };
        settings.Pressed += () =>
        {
            if (_profileSettings is null)
            {
                EmitSignal(SignalName.TrainingProfileRequested);
                return;
            }

            ShowTrainingSettingsSheet();
        };
        _inputPassthroughExceptions.Add(settings);
        topBar.AddChild(settings);

        var progress = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = _unlockProgress?.Progress ?? 0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 3),
            AnchorLeft = 0,
            AnchorRight = 1,
            AnchorTop = 1,
            AnchorBottom = 1,
            OffsetTop = -3,
        };
        progress.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = _tokens.Line });
        progress.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = _tokens.Accent });
        panel.AddChild(progress);

        return panel;
    }

    private Control CreateModeSwitch()
    {
        var frame = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, _modeSwitchHeight),
        };
        frame.AddThemeConstantOverride("separation", 0);
        frame.AddChild(CreateModeSegment("Simulate", active: true, first: true, last: false));
        frame.AddChild(CreateModeSegment("Build", active: false, first: false, last: true));
        return frame;
    }

    private Control CreateModeSegment(string label, bool active, bool first, bool last)
    {
        var segment = new PanelContainer
        {
            CustomMinimumSize = new Vector2(148, _modeSwitchHeight),
        };
        segment.AddThemeStyleboxOverride("panel", CreateSegmentStyle(active, first, last));
        var text = CreateLabel(label.ToUpperInvariant(), 16, _tokens.Ink);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;
        text.MouseFilter = MouseFilterEnum.Ignore;
        segment.AddChild(text);
        if (!active)
        {
            _buildModeSegment = segment;
            void RequestBuild(InputEvent @event)
            {
                if (@event is InputEventMouseButton { Pressed: true } or InputEventScreenTouch { Pressed: true })
                {
                    EmitSignal(SignalName.BuildRequested);
                    segment.AcceptEvent();
                }
            }

            segment.MouseDefaultCursorShape = CursorShape.PointingHand;
            segment.GuiInput += RequestBuild;
            text.GuiInput += RequestBuild;
            _inputPassthroughExceptions.Add(segment);
        }

        return segment;
    }

    private void OnTopBarInput(InputEvent @event)
    {
        if (_buildModeSegment is null || !TryGetPressedPosition(@event, out var position))
        {
            return;
        }

        if (_buildModeSegment.GetGlobalRect().HasPoint(position))
        {
            EmitSignal(SignalName.BuildRequested);
            AcceptEvent();
        }
    }

    private static bool TryGetPressedPosition(InputEvent @event, out Vector2 position)
    {
        switch (@event)
        {
            case InputEventMouseButton { Pressed: true } mouse:
                position = mouse.GlobalPosition;
                return true;
            case InputEventScreenTouch { Pressed: true } touch:
                position = touch.Position;
                return true;
            default:
                position = default;
                return false;
        }
    }

    private Control CreateArenaPanel()
    {
        if (!ShowArenaPlaceholder)
        {
            return new Control
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore,
            };
        }

        var panel = CreatePanel(raised: true);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;

        var margin = CreateMargin(18);
        panel.AddChild(margin);

        var layout = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        layout.AddThemeConstantOverride("separation", 12);
        margin.AddChild(layout);

        layout.AddChild(CreateLabel("Arena", 20, _tokens.Ink));
        layout.AddChild(CreateLabel(
            _presentation is null
                ? "Sample creature running on a flat test track"
                : "Live Creation training on the test track",
            14,
            _tokens.Muted));

        var placeholder = new Control
        {
            CustomMinimumSize = new Vector2(0, 220),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        placeholder.Draw += () => DrawArenaPlaceholder(placeholder);
        layout.AddChild(placeholder);

        layout.AddChild(CreateLabel("Progress: reach 50 fitness to unlock one extra core slot", 14, _tokens.Accent));

        return panel;
    }

    private Control CreateSignalPanel()
    {
        var panel = CreatePanel(raised: false);
        panel.CustomMinimumSize = new Vector2(_signalPanelWidth, 0);
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        _inputPassthroughExceptions.Add(panel);

        var margin = CreateMargin(12);
        panel.AddChild(margin);

        var stack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        stack.AddThemeConstantOverride("separation", 5);
        margin.AddChild(stack);

        stack.AddChild(CreateLabel("SignalFlow", 20, _tokens.Ink));
        stack.AddChild(CreateSignalCard(0, "1 Sees", "The cores sense nearby contact and body state."));
        stack.AddChild(CreateSignalConnector());
        stack.AddChild(CreateSignalCard(1, "2 Decides", "The neural network turns sensor values into joint targets."));
        stack.AddChild(CreateSignalConnector());
        stack.AddChild(CreateSignalCard(2, "3 Twists", "Motor relations apply the chosen targets to beams."));
        stack.AddChild(CreateSignalConnector());
        stack.AddChild(CreateSignalCard(3, "4 Scores", "Fitness is the distance reached before the trial ends."));
        if (!ReadOnlyControls)
        {
            stack.AddChild(CreateLabel("Tap one stage to expand its explanation.", 14, _tokens.Muted));
        }

        UpdateSignalFlowCards();
        return panel;
    }

    private Control CreateTrainingPanel()
    {
        var panel = CreatePanel(raised: false);
        panel.CustomMinimumSize = new Vector2(0, 116);

        var margin = CreateMargin(16);
        panel.AddChild(margin);

        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        row.AddThemeConstantOverride("separation", 14);
        margin.AddChild(row);

        var summary = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        summary.AddThemeConstantOverride("separation", 6);
        row.AddChild(summary);

        var generationText = _presentation?.GenerationText ?? "Generation 5 · try 3 of 8";
        var best = _presentation is null || double.IsNegativeInfinity(_presentation.BestFitness)
            ? "—"
            : $"{_presentation.BestFitness:0.0} m";
        var mean = _presentation?.MeanFitness ?? 8.4;
        var profile = _presentation?.Profile ?? "Quick";
        summary.AddChild(CreateLabel(generationText, 18, _tokens.Ink));
        summary.AddChild(CreateLabel($"Best {best} · mean {mean:0.0} m · {profile} profile", 14, _tokens.Muted));
        summary.AddChild(CreateSampleStrip());

        if (!ReadOnlyControls)
        {
            row.AddChild(CreateButton("Pause", UiActionButton.ActionKind.Secondary, "Pause sample training"));
            row.AddChild(CreateButton("Profile", UiActionButton.ActionKind.Secondary, "Open sample training settings"));
        }

        return panel;
    }

    private Control CreateLiveControlRow()
    {
        var panel = CreatePanel(raised: true);
        panel.CustomMinimumSize = new Vector2(0, 74);
        _inputPassthroughExceptions.Add(panel);

        var margin = CreateMargin(8);
        panel.AddChild(margin);

        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        row.AddThemeConstantOverride("separation", 8);
        margin.AddChild(row);

        var pause = CreateButton(PauseActionText, UiActionButton.ActionKind.Secondary, "Pause or resume simulation");
        pause.Pressed += () => EmitSignal(SignalName.PauseRequested);
        _livePauseButton = pause;
        _inputPassthroughExceptions.Add(pause);
        row.AddChild(pause);

        var speed = CreateButton("Speed", UiActionButton.ActionKind.Secondary, "Cycle simulation speed");
        speed.Pressed += () => EmitSignal(SignalName.SpeedRequested);
        _inputPassthroughExceptions.Add(speed);
        row.AddChild(speed);

        var reset = CreateButton("Reset", UiActionButton.ActionKind.Secondary, "Restart the active training run");
        reset.Pressed += () => EmitSignal(SignalName.ResetRequested);
        _inputPassthroughExceptions.Add(reset);
        row.AddChild(reset);

        var summary = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        summary.AddThemeConstantOverride("separation", 4);
        row.AddChild(summary);
        summary.AddChild(CreateLabel(_presentation?.GenerationText ?? "Generation 0 · try 1 of 8", 18, _tokens.Ink));
        summary.AddChild(CreateSampleStrip());

        return panel;
    }

    private Control CreateLiveTrainingSummary()
    {
        var panel = CreatePanel(raised: true);
        var margin = CreateMargin(12);
        panel.AddChild(margin);

        var stack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        stack.AddThemeConstantOverride("separation", 4);
        margin.AddChild(stack);

        var generationText = _presentation?.GenerationText ?? "Generation 5 · try 3 of 8";
        var best = _presentation is null || double.IsNegativeInfinity(_presentation.BestFitness)
            ? "—"
            : $"{_presentation.BestFitness:0.0} m";
        var mean = _presentation?.MeanFitness ?? 8.4;
        var profile = _presentation?.Profile ?? "Quick";
        var profileButton = new UiActionButton
        {
            Tokens = _tokens,
            Kind = UiActionButton.ActionKind.Secondary,
            LabelText = $"Training: {profile} · settings",
            TooltipText = "Open profile settings. Choosing a profile restarts the active run.",
            CustomMinimumSize = new Vector2(0, _tokens.TouchTarget),
        };
        profileButton.Pressed += () =>
        {
            if (_profileSettings is null)
            {
                EmitSignal(SignalName.TrainingProfileRequested);
                return;
            }

            ShowTrainingSettingsSheet();
        };
        _inputPassthroughExceptions.Add(profileButton);
        stack.AddChild(profileButton);
        stack.AddChild(CreateLabel(generationText, 15, _tokens.Ink));
        stack.AddChild(CreateLabel($"Best {best} · mean {mean:0.0} m", 13, _tokens.Muted));
        stack.AddChild(CreateLabel(_profileSummary?.Detail ?? "8 candidates · 10s · 10% mutation · uniform genes", 11, _tokens.Muted));
        stack.AddChild(CreateSampleStrip());
        stack.AddChild(CreateUnlockProgress());

        return panel;
    }

    private void AddSettingsOverlay()
    {
        _settingsOverlay = new Control
        {
            Visible = false,
            ZIndex = 30,
        };
        _settingsOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_settingsOverlay);

        _settingsScrim = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.18f),
            MouseFilter = MouseFilterEnum.Stop,
            ZIndex = 30,
        };
        _settingsScrim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _settingsScrim.GuiInput += OnSettingsScrimInput;
        _settingsOverlay.AddChild(_settingsScrim);
        _inputPassthroughExceptions.Add(_settingsScrim);

        _settingsSheet = new UiSheet
        {
            Tokens = _tokens,
            Title = "Training settings",
            CustomMinimumSize = new Vector2(460, 0),
            ZIndex = 31,
        };
        _settingsOverlay.AddChild(_settingsSheet);
        _settingsSheet.Hide();
        _inputPassthroughExceptions.Add(_settingsSheet);
    }

    private void ShowTrainingSettingsSheet()
    {
        if (_settingsOverlay is null || _settingsSheet is null || _profileSettings is null)
        {
            return;
        }

        _settingsSheet.SetBody(CreateTrainingSettingsBody());
        _settingsOverlay.Show();
        _settingsSheet.Show();
        RefreshSettingsSheetLayout();
        MakeSettingsInteractive(_settingsSheet);
    }

    private void CloseTrainingSettingsSheet()
    {
        _settingsSheet?.Hide();
        _settingsOverlay?.Hide();
    }

    private void OnSettingsScrimInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true } or InputEventScreenTouch { Pressed: true })
        {
            CloseTrainingSettingsSheet();
            _settingsScrim?.AcceptEvent();
        }
    }

    private static void MakeSettingsInteractive(Node node)
    {
        if (node is Control control)
        {
            control.MouseFilter = MouseFilterEnum.Stop;
        }

        foreach (var child in node.GetChildren())
        {
            MakeSettingsInteractive(child);
        }
    }

    private void RefreshSettingsSheetLayout()
    {
        if (_settingsSheet is null)
        {
            return;
        }

        var sheetWidth = _settingsSheet.CustomMinimumSize.X;
        _settingsSheet.Position = new Vector2(Mathf.Max(24, Size.X - sheetWidth - 48), 72);
    }

    private Control CreateTrainingSettingsBody()
    {
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 12);
        stack.AddChild(CreateLabel("Choose a profile. Changing it restarts this training run without changing saved Creation data.", 13, _tokens.Muted));

        var options = _profileSettings?.Options ?? Array.Empty<TrainingProfileOptionPresentation>();
        var selectedIndex = _profileSettings?.SelectedIndex ?? -1;
        for (var index = 0; index < options.Count; index++)
        {
            stack.AddChild(CreateTrainingProfileOption(index, options[index], index == selectedIndex));
        }

        var done = new UiActionButton
        {
            Tokens = _tokens,
            Kind = UiActionButton.ActionKind.Secondary,
            LabelText = "Done",
            CustomMinimumSize = new Vector2(0, _tokens.TouchTarget),
        };
        done.Pressed += CloseTrainingSettingsSheet;
        stack.AddChild(done);
        return stack;
    }

    private Control CreateTrainingProfileOption(int index, TrainingProfileOptionPresentation option, bool selected)
    {
        var card = CreatePanel(raised: true);
        var margin = CreateMargin(10);
        card.AddChild(margin);

        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        row.AddThemeConstantOverride("separation", 10);
        margin.AddChild(row);

        var text = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        text.AddThemeConstantOverride("separation", 3);
        text.AddChild(CreateLabel(option.Name, 15, selected ? _tokens.Accent : _tokens.Ink));
        text.AddChild(CreateLabel(option.Detail, 11, _tokens.Muted));
        row.AddChild(text);

        var choose = new UiActionButton
        {
            Tokens = _tokens,
            Kind = selected ? UiActionButton.ActionKind.Primary : UiActionButton.ActionKind.Secondary,
            LabelText = selected ? "Active" : "Restart",
            CustomMinimumSize = new Vector2(112, _tokens.TouchTarget),
        };
        choose.Pressed += () =>
        {
            if (!selected)
            {
                EmitSignal(SignalName.TrainingProfileSelected, index);
            }

            CloseTrainingSettingsSheet();
        };
        row.AddChild(choose);

        return card;
    }

    private Control CreateUnlockProgress()
    {
        var stack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        stack.AddThemeConstantOverride("separation", 4);

        var title = _unlockProgress?.Title ?? "Next unlock";
        var detail = _unlockProgress?.Detail ?? "Reach 50.0 m to unlock one extra core slot";
        var progress = _unlockProgress?.Progress ?? 0;
        stack.AddChild(CreateLabel(title, 13, _tokens.Accent));
        var bar = CreateSignalBar();
        bar.Value = progress;
        stack.AddChild(bar);
        stack.AddChild(CreateLabel(detail, 12, _tokens.Muted));
        return stack;
    }

    private Control CreateSampleStrip()
    {
        var strip = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 20),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        strip.AddThemeConstantOverride("separation", 4);

        var population = _presentation?.Population ?? 8;
        var currentCandidate = _presentation?.Candidate ?? 3;
        var completedCount = _presentation?.CompletedCandidateCount ?? Math.Max(0, currentCandidate - 1);
        if (population < 1)
        {
            population = 8;
        }

        for (var index = 1; index <= population; index++)
        {
            var isCurrent = _presentation?.IsTrialActive != false && index == currentCandidate;
            var color = index <= completedCount
                ? _tokens.AccentSoft
                : isCurrent
                    // Marks the current generation; never rely on color
                    // alone (see the LineStrong border below), so this
                    // stays legible in Paper and doesn't depend on the
                    // glow-flavored Halo tint from effects-lite (#134).
                    ? _tokens.Accent
                    : _tokens.Line;

            // Panel (not ColorRect) so the current-generation cell can carry
            // a themed border stylebox as its non-color "current" cue.
            var cell = new Panel
            {
                CustomMinimumSize = new Vector2(12, 18),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            };
            cell.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = color,
                BorderColor = isCurrent ? _tokens.LineStrong : color,
                BorderWidthLeft = isCurrent ? 2 : 0,
                BorderWidthTop = isCurrent ? 2 : 0,
                BorderWidthRight = isCurrent ? 2 : 0,
                BorderWidthBottom = isCurrent ? 2 : 0,
            });
            strip.AddChild(cell);
        }

        return strip;
    }

    private UiPanel CreateSignalCard(int index, string title, string detail)
    {
        var card = CreatePanel(raised: true);
        card.CustomMinimumSize = new Vector2(_signalPanelWidth - 28, ReadOnlyControls ? 78 : 72);
        _signalCards.Add(card);

        var margin = CreateMargin(6);
        card.AddChild(margin);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 3);
        margin.AddChild(stack);

        if (ReadOnlyControls)
        {
            if (index == 1)
            {
                var action = new UiActionButton
                {
                    Tokens = _tokens,
                    Kind = UiActionButton.ActionKind.Secondary,
                    LabelText = title,
                    TooltipText = "Open BrainFocus for the live network.",
                    CustomMinimumSize = new Vector2(0, _tokens.TouchTarget),
                };
                action.Pressed += () =>
                {
                    SelectSignal(index, detail);
                };
                _inputPassthroughExceptions.Add(action);
                stack.AddChild(action);
            }
            else
            {
                var heading = CreateLabel(title, 16, _tokens.Muted);
                heading.HorizontalAlignment = HorizontalAlignment.Center;
                heading.CustomMinimumSize = new Vector2(0, 20);
                stack.AddChild(heading);
            }
        }
        else
        {
            var action = new UiActionButton
            {
                Tokens = _tokens,
                Kind = UiActionButton.ActionKind.Secondary,
                LabelText = title,
                CustomMinimumSize = new Vector2(0, _tokens.TouchTarget),
            };
            action.Pressed += () =>
            {
                SelectSignal(index, detail);
            };
            stack.AddChild(action);
        }
        switch (index)
        {
            case 0:
                _seesStatusLabel = CreateLabel(string.Empty, 14, _tokens.Muted);
                stack.AddChild(CreateThreeBarPreview(_sensorBars));
                stack.AddChild(_seesStatusLabel);
                break;
            case 1:
                _decidesStatusLabel = CreateLabel(string.Empty, 15, _tokens.Accent);
                _decidesStatusLabel.HorizontalAlignment = HorizontalAlignment.Center;
                stack.AddChild(_decidesStatusLabel);
                break;
            case 2:
                _twistsStatusLabel = CreateLabel(string.Empty, 14, _tokens.Muted);
                stack.AddChild(CreateTwoBarPreview(_motorBars));
                stack.AddChild(_twistsStatusLabel);
                break;
            case 3:
                _scoresStatusLabel = CreateLabel(string.Empty, 15, _tokens.Accent);
                _scoresStatusLabel.HorizontalAlignment = HorizontalAlignment.Center;
                stack.AddChild(_scoresStatusLabel);
                break;
        }

        var bodyLabel = CreateLabel(detail, 13, _tokens.Muted);
        bodyLabel.Visible = false;
        bodyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _signalBodies.Add(bodyLabel);
        stack.AddChild(bodyLabel);

        return card;
    }

    private Control CreateThreeBarPreview(List<ProgressBar> bars)
    {
        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 18),
        };
        row.AddThemeConstantOverride("separation", 8);
        for (var index = 0; index < 3; index++)
        {
            var bar = CreateSignalBar();
            bars.Add(bar);
            row.AddChild(bar);
        }

        return row;
    }

    private Control CreateTwoBarPreview(List<ProgressBar> bars)
    {
        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 18),
        };
        row.AddThemeConstantOverride("separation", 8);
        for (var index = 0; index < 2; index++)
        {
            var bar = CreateSignalBar();
            bars.Add(bar);
            row.AddChild(bar);
        }

        return row;
    }

    private ProgressBar CreateSignalBar()
    {
        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 10),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = _tokens.Line });
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = _tokens.Accent });
        return bar;
    }

    private Control CreateSignalConnector()
    {
        var connector = new Label
        {
            Text = "\u2193",
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(0, 6),
        };
        connector.AddThemeColorOverride("font_color", _tokens.Accent);
        connector.AddThemeFontSizeOverride("font_size", 12);
        return connector;
    }

    private void UpdateSignalFlowCards()
    {
        if (_signalFlow is null)
        {
            SetLabelTextIfChanged(_seesStatusLabel, "Waiting for live sensors");
            SetLabelTextIfChanged(_decidesStatusLabel, "Brain waits");
            SetLabelTextIfChanged(_twistsStatusLabel, "Waiting for motors");
            SetLabelTextIfChanged(_scoresStatusLabel, "Distance pending");
            return;
        }

        UpdateBars(_sensorBars, _signalFlow.SensorRows);
        UpdateBars(_motorBars, _signalFlow.MotorRows);
        SetLabelTextIfChanged(_seesStatusLabel, _signalFlow.SensorCount == 0 ? _signalFlow.SeesSummary : string.Empty);
        SetLabelTextIfChanged(
            _decidesStatusLabel,
            _signalFlow.SensorCount == 0 || _signalFlow.MotorCount == 0
                ? "Brain waits"
                : $"{_signalFlow.SensorCount} inputs \u2192 {_signalFlow.MotorCount} targets");
        SetLabelTextIfChanged(_twistsStatusLabel, _signalFlow.MotorCount == 0 ? _signalFlow.TwistsSummary : string.Empty);
        SetLabelTextIfChanged(_scoresStatusLabel, _signalFlow.ScoresSummary);
    }

    private static void UpdateBars(IReadOnlyList<ProgressBar> bars, IReadOnlyList<SignalFlowReadingPresentation> readings)
    {
        for (var index = 0; index < bars.Count; index++)
        {
            bars[index].Value = index < readings.Count ? readings[index].Fill : 0;
        }
    }

    private static void SetLabelTextIfChanged(Label? label, string text)
    {
        if (label is not null && label.Text != text)
        {
            label.Text = text;
            label.Visible = !string.IsNullOrWhiteSpace(text);
        }
    }

    private void SelectSignal(int index, string detail)
    {
        for (var cardIndex = 0; cardIndex < _signalCards.Count; cardIndex++)
        {
            var selected = cardIndex == index;
            _signalCards[cardIndex].Variant = selected
                ? UiSurfaceContracts.FrameVariant.Pick
                : UiSurfaceContracts.FrameVariant.Frame;
            _signalBodies[cardIndex].Visible = selected;
        }

        _selectedSignalIndex = index;
        if (index == 1)
        {
            EmitSignal(SignalName.BrainFocusRequested);
        }
    }

    private void DrawArenaPlaceholder(Control control)
    {
        var size = control.Size;
        var grid = _tokens.Line;
        for (var x = 0f; x < size.X; x += 40f)
        {
            control.DrawLine(new Vector2(x, 0), new Vector2(x, size.Y), grid, 1);
        }

        for (var y = 0f; y < size.Y; y += 40f)
        {
            control.DrawLine(new Vector2(0, y), new Vector2(size.X, y), grid, 1);
        }

        var groundY = size.Y - 44;
        control.DrawLine(new Vector2(0, groundY), new Vector2(size.X, groundY), _tokens.LineStrong, 2);

        var center = new Vector2(size.X * 0.44f, groundY - 72);
        var front = center + new Vector2(86, 20);
        var rear = center + new Vector2(-86, 16);
        control.DrawLine(rear, center, _tokens.Accent, 5, antialiased: true);
        control.DrawLine(center, front, _tokens.Accent, 5, antialiased: true);
        // Purely decorative illustration -- effects-lite drops the glow
        // treatment for flat schematic dots instead of hiding them (#134).
        control.DrawCircle(rear, _tokens.EffectsEnabled ? 18 : 8, _tokens.EffectsEnabled ? _tokens.AccentGlow : _tokens.Line);
        control.DrawCircle(center, _tokens.EffectsEnabled ? 24 : 10, _tokens.EffectsEnabled ? _tokens.Halo : _tokens.LineStrong);
        control.DrawCircle(front, _tokens.EffectsEnabled ? 18 : 8, _tokens.EffectsEnabled ? _tokens.AccentGlow : _tokens.Line);
    }

    private UiPanel CreatePanel(bool raised)
    {
        return new UiPanel
        {
            Tokens = _tokens,
            Variant = raised
                ? UiSurfaceContracts.FrameVariant.Raised
                : UiSurfaceContracts.FrameVariant.Frame,
        };
    }

    private UiActionButton CreateModeButton(string label, bool active)
    {
        return new UiActionButton
        {
            Tokens = _tokens,
            Kind = active ? UiActionButton.ActionKind.Primary : UiActionButton.ActionKind.Secondary,
            LabelText = label,
            CustomMinimumSize = new Vector2(96, _tokens.TouchTarget),
        };
    }

    private StyleBoxFlat CreateSegmentStyle(bool active, bool first, bool last)
    {
        var radius = (int)_tokens.RadiusMedium;
        return new StyleBoxFlat
        {
            BgColor = active ? _tokens.AccentSoft : _tokens.PanelRaised,
            BorderColor = active ? _tokens.Accent : _tokens.LineStrong,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = last ? 1 : 0,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = first ? radius : 0,
            CornerRadiusBottomLeft = first ? radius : 0,
            CornerRadiusTopRight = last ? radius : 0,
            CornerRadiusBottomRight = last ? radius : 0,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
        };
    }

    private UiActionButton CreateButton(string label, UiActionButton.ActionKind kind, string tooltip)
    {
        return new UiActionButton
        {
            Tokens = _tokens,
            Kind = kind,
            LabelText = label,
            TooltipText = tooltip,
            CustomMinimumSize = new Vector2(128, _tokens.TouchTarget),
        };
    }

    private MarginContainer CreateMargin(int margin)
    {
        var container = new MarginContainer();
        container.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        container.AddThemeConstantOverride("margin_left", margin);
        container.AddThemeConstantOverride("margin_top", margin);
        container.AddThemeConstantOverride("margin_right", margin);
        container.AddThemeConstantOverride("margin_bottom", margin);
        return container;
    }

    private Label CreateLabel(string text, int fontSize, Color color, bool expand = false)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private Label CreatePill(string text, Color background, Color foreground)
    {
        var label = CreateLabel($"  {text}  ", 14, foreground);
        label.CustomMinimumSize = new Vector2(110, _tokens.TouchTarget);
        label.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = foreground,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = (int)_tokens.RadiusLarge,
            CornerRadiusTopRight = (int)_tokens.RadiusLarge,
            CornerRadiusBottomLeft = (int)_tokens.RadiusLarge,
            CornerRadiusBottomRight = (int)_tokens.RadiusLarge,
        });
        return label;
    }

    private void ApplyInputPassthrough(Node node)
    {
        if (!_inputPassthrough)
        {
            return;
        }

        if (node is Control control)
        {
            control.MouseFilter = MouseFilterEnum.Ignore;
        }

        foreach (var child in node.GetChildren())
        {
            ApplyInputPassthrough(child);
        }

        foreach (var exception in _inputPassthroughExceptions)
        {
            exception.MouseFilter = MouseFilterEnum.Stop;
        }
    }
}
