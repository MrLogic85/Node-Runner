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
    private const int _signalPanelWidth = 340;
    private readonly List<UiCard> _signalCards = new();
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
    private UiButton? _livePauseButton;
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
    public delegate void BackRequestedEventHandler();

    [Signal]
    public delegate void PauseRequestedEventHandler();

    [Signal]
    public delegate void SpeedRequestedEventHandler();

    [Signal]
    public delegate void ResetRequestedEventHandler();

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

    public string PauseActionText
    {
        get => _pauseActionText;
        set
        {
            _pauseActionText = string.IsNullOrWhiteSpace(value) ? "Pause" : value;
            if (_livePauseButton is not null)
            {
                _livePauseButton.Text = _pauseActionText;
            }
        }
    }

    /// <summary>Closes the training settings sheet, as Android Back does first. False when it was closed.</summary>
    public bool CloseOverlay()
    {
        if (_settingsOverlay?.Visible != true)
        {
            return false;
        }

        CloseTrainingSettingsSheet();
        return true;
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

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged && IsNodeReady())
        {
            UiThemeRefresh.Guarded(this, RebuildLayout);
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
                Color = UiThemeLookup.Color(this, UiTokens.Color.Background),
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

        screen.AddChild(CreateTopBar());

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
        _inputPassthroughExceptions.Add(panel);

        var margin = CreateMargin(0);
        margin.AddThemeConstantOverride("margin_left", 4);
        margin.AddThemeConstantOverride("margin_right", 0);
        panel.AddChild(margin);

        var topBar = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, _topBarHeight),
        };
        topBar.AddThemeConstantOverride("separation", 8);
        margin.AddChild(topBar);

        var back = new UiButton
        {
            ContentLayout = UiButtonContentLayout.Stacked,
            IconId = UiIconId.Back,
            TooltipText = "Back",
        };
        back.Pressed += () => EmitSignal(SignalName.BackRequested);
        _inputPassthroughExceptions.Add(back);
        topBar.AddChild(back);

        var title = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        title.AddThemeConstantOverride("separation", 0);
        topBar.AddChild(title);
        title.AddChild(CreateLabel("Simulate", 22, UiThemeLookup.Color(this, UiTokens.Color.Ink), expand: true));
        title.AddChild(CreateLabel(_presentation?.GenerationText ?? "Training", 14, UiThemeLookup.Color(this, UiTokens.Color.Muted)));

        var creations = CreateButton("Creations", UiButtonKind.Secondary, "Open saved Creations");
        creations.Pressed += () => EmitSignal(SignalName.CreationsRequested);
        _inputPassthroughExceptions.Add(creations);
        topBar.AddChild(creations);

        var settings = new UiButton
        {
            ContentLayout = UiButtonContentLayout.Stacked,
            IconId = UiIconId.More,
            TooltipText = "Training settings",
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
        progress.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = UiThemeLookup.Color(this, UiTokens.Color.Line) });
        progress.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = UiThemeLookup.Color(this, UiTokens.Color.Accent) });
        panel.AddChild(progress);

        return panel;
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

        layout.AddChild(CreateLabel("Arena", 20, UiThemeLookup.Color(this, UiTokens.Color.Ink)));
        layout.AddChild(CreateLabel(
            _presentation is null
                ? "Sample creature running on a flat test track"
                : "Live Creation training on the test track",
            14,
            UiThemeLookup.Color(this, UiTokens.Color.Muted)));

        var placeholder = new Control
        {
            CustomMinimumSize = new Vector2(0, 220),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        placeholder.Draw += () => DrawArenaPlaceholder(placeholder);
        layout.AddChild(placeholder);

        layout.AddChild(CreateLabel("Progress: reach 50 fitness to unlock one extra core slot", 14, UiThemeLookup.Color(this, UiTokens.Color.Accent)));

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

        stack.AddChild(CreateLabel("SignalFlow", 20, UiThemeLookup.Color(this, UiTokens.Color.Ink)));
        stack.AddChild(CreateSignalCard(0, "1 Sees", "The cores sense nearby contact and body state."));
        stack.AddChild(CreateSignalConnector());
        stack.AddChild(CreateSignalCard(1, "2 Decides", "The neural network turns sensor values into joint targets."));
        stack.AddChild(CreateSignalConnector());
        stack.AddChild(CreateSignalCard(2, "3 Twists", "Motor relations apply the chosen targets to beams."));
        stack.AddChild(CreateSignalConnector());
        stack.AddChild(CreateSignalCard(3, "4 Scores", "Fitness is the distance reached before the trial ends."));
        if (!ReadOnlyControls)
        {
            stack.AddChild(CreateLabel("Tap one stage to expand its explanation.", 14, UiThemeLookup.Color(this, UiTokens.Color.Muted)));
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
        summary.AddChild(CreateLabel(generationText, 18, UiThemeLookup.Color(this, UiTokens.Color.Ink)));
        summary.AddChild(CreateLabel($"Best {best} · mean {mean:0.0} m · {profile} profile", 14, UiThemeLookup.Color(this, UiTokens.Color.Muted)));
        summary.AddChild(CreateSampleStrip());

        if (!ReadOnlyControls)
        {
            row.AddChild(CreateButton("Pause", UiButtonKind.Secondary, "Pause sample training"));
            row.AddChild(CreateButton("Profile", UiButtonKind.Secondary, "Open sample training settings"));
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

        var pause = CreateButton(PauseActionText, UiButtonKind.Secondary, "Pause or resume simulation");
        pause.Pressed += () => EmitSignal(SignalName.PauseRequested);
        _livePauseButton = pause;
        _inputPassthroughExceptions.Add(pause);
        row.AddChild(pause);

        var speed = CreateButton("Speed", UiButtonKind.Secondary, "Cycle simulation speed");
        speed.Pressed += () => EmitSignal(SignalName.SpeedRequested);
        _inputPassthroughExceptions.Add(speed);
        row.AddChild(speed);

        var reset = CreateButton("Reset", UiButtonKind.Secondary, "Restart the active training run");
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
        summary.AddChild(CreateLabel(_presentation?.GenerationText ?? "Generation 0 · try 1 of 8", 18, UiThemeLookup.Color(this, UiTokens.Color.Ink)));
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
        var profileButton = new UiButton
        {
            Kind = UiButtonKind.Secondary,
            Text = $"Training: {profile} · settings",
            TooltipText = "Open profile settings. Choosing a profile restarts the active run.",
            CustomMinimumSize = new Vector2(0, UiSize.Control.Touch),
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
        stack.AddChild(CreateLabel(generationText, 15, UiThemeLookup.Color(this, UiTokens.Color.Ink)));
        stack.AddChild(CreateLabel($"Best {best} · mean {mean:0.0} m", 13, UiThemeLookup.Color(this, UiTokens.Color.Muted)));
        stack.AddChild(CreateLabel(_profileSummary?.Detail ?? "8 candidates · 10s · 10% mutation · uniform genes", 11, UiThemeLookup.Color(this, UiTokens.Color.Muted)));
        stack.AddChild(CreateSampleStrip());
        stack.AddChild(CreateUnlockProgress());

        return panel;
    }

    private void AddSettingsOverlay()
    {
        // Added last, so tree order draws the sheet over the rest of the screen (#464).
        _settingsOverlay = new Control
        {
            Visible = false,
        };
        _settingsOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_settingsOverlay);

        _settingsScrim = new ColorRect
        {
            Color = UiThemeLookup.Color(this, UiTokens.Color.Scrim).WithAlpha(0.18f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        _settingsScrim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _settingsScrim.GuiInput += OnSettingsScrimInput;
        _settingsOverlay.AddChild(_settingsScrim);
        _inputPassthroughExceptions.Add(_settingsScrim);

        _settingsSheet = new UiSheet
        {
            Title = "Training settings",
            CustomMinimumSize = new Vector2(460, 0),
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
        if (@event is InputEventMouseButton { Pressed: true })
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
        stack.AddChild(CreateLabel("Choose a profile. Changing it restarts this training run without changing saved Creation data.", 13, UiThemeLookup.Color(this, UiTokens.Color.Muted)));

        var options = _profileSettings?.Options ?? Array.Empty<TrainingProfileOptionPresentation>();
        var selectedIndex = _profileSettings?.SelectedIndex ?? -1;
        for (var index = 0; index < options.Count; index++)
        {
            stack.AddChild(CreateTrainingProfileOption(index, options[index], index == selectedIndex));
        }

        var done = new UiButton
        {
            Kind = UiButtonKind.Secondary,
            Text = "Done",
            CustomMinimumSize = new Vector2(0, UiSize.Control.Touch),
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
        text.AddChild(CreateLabel(option.Name, 15, selected ? UiThemeLookup.Color(this, UiTokens.Color.Accent) : UiThemeLookup.Color(this, UiTokens.Color.Ink)));
        text.AddChild(CreateLabel(option.Detail, 11, UiThemeLookup.Color(this, UiTokens.Color.Muted)));
        row.AddChild(text);

        var choose = new UiButton
        {
            Kind = selected ? UiButtonKind.Primary : UiButtonKind.Secondary,
            Text = selected ? "Active" : "Restart",
            CustomMinimumSize = new Vector2(112, UiSize.Control.Touch),
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
        stack.AddChild(CreateLabel(title, 13, UiThemeLookup.Color(this, UiTokens.Color.Accent)));
        var bar = CreateSignalBar();
        bar.Value = progress;
        stack.AddChild(bar);
        stack.AddChild(CreateLabel(detail, 12, UiThemeLookup.Color(this, UiTokens.Color.Muted)));
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
                ? UiThemeLookup.Color(this, UiTokens.Color.Accent).WithAlpha(UiThemeLookup.Alpha(this, UiTokens.Alpha.Soft))
                : isCurrent
                    // Marks the current generation; never rely on color
                    // alone (see the LineStrong border below), so this
                    // stays legible in Paper and doesn't depend on the
                    // glow-flavored Halo tint (#134).
                    ? UiThemeLookup.Color(this, UiTokens.Color.Accent)
                    : UiThemeLookup.Color(this, UiTokens.Color.Line);

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
                BorderColor = isCurrent ? UiThemeLookup.Color(this, UiTokens.Color.LineStrong) : color,
                BorderWidthLeft = isCurrent ? 2 : 0,
                BorderWidthTop = isCurrent ? 2 : 0,
                BorderWidthRight = isCurrent ? 2 : 0,
                BorderWidthBottom = isCurrent ? 2 : 0,
            });
            strip.AddChild(cell);
        }

        return strip;
    }

    private UiCard CreateSignalCard(int index, string title, string detail)
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
                var action = new UiButton
                {
                    Kind = UiButtonKind.Secondary,
                    Text = title,
                    TooltipText = "Open BrainFocus for the live network.",
                    CustomMinimumSize = new Vector2(0, UiSize.Control.Touch),
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
                var heading = CreateLabel(title, 16, UiThemeLookup.Color(this, UiTokens.Color.Muted));
                heading.HorizontalAlignment = HorizontalAlignment.Center;
                heading.CustomMinimumSize = new Vector2(0, 20);
                stack.AddChild(heading);
            }
        }
        else
        {
            var action = new UiButton
            {
                Kind = UiButtonKind.Secondary,
                Text = title,
                CustomMinimumSize = new Vector2(0, UiSize.Control.Touch),
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
                _seesStatusLabel = CreateLabel(string.Empty, 14, UiThemeLookup.Color(this, UiTokens.Color.Muted));
                stack.AddChild(CreateThreeBarPreview(_sensorBars));
                stack.AddChild(_seesStatusLabel);
                break;
            case 1:
                _decidesStatusLabel = CreateLabel(string.Empty, 15, UiThemeLookup.Color(this, UiTokens.Color.Accent));
                _decidesStatusLabel.HorizontalAlignment = HorizontalAlignment.Center;
                stack.AddChild(_decidesStatusLabel);
                break;
            case 2:
                _twistsStatusLabel = CreateLabel(string.Empty, 14, UiThemeLookup.Color(this, UiTokens.Color.Muted));
                stack.AddChild(CreateTwoBarPreview(_motorBars));
                stack.AddChild(_twistsStatusLabel);
                break;
            case 3:
                _scoresStatusLabel = CreateLabel(string.Empty, 15, UiThemeLookup.Color(this, UiTokens.Color.Accent));
                _scoresStatusLabel.HorizontalAlignment = HorizontalAlignment.Center;
                stack.AddChild(_scoresStatusLabel);
                break;
        }

        var bodyLabel = CreateLabel(detail, 13, UiThemeLookup.Color(this, UiTokens.Color.Muted));
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
        bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = UiThemeLookup.Color(this, UiTokens.Color.Line) });
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = UiThemeLookup.Color(this, UiTokens.Color.Accent) });
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
        connector.AddThemeColorOverride("font_color", UiThemeLookup.Color(this, UiTokens.Color.Accent));
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
            _signalCards[cardIndex].Kind = selected
                ? UiCard.CardVariant.Selected
                : UiCard.CardVariant.Frame;
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
        var grid = UiThemeLookup.Color(this, UiTokens.Color.Line);
        for (var x = 0f; x < size.X; x += 40f)
        {
            control.DrawLine(new Vector2(x, 0), new Vector2(x, size.Y), grid, 1);
        }

        for (var y = 0f; y < size.Y; y += 40f)
        {
            control.DrawLine(new Vector2(0, y), new Vector2(size.X, y), grid, 1);
        }

        var groundY = size.Y - 44;
        control.DrawLine(new Vector2(0, groundY), new Vector2(size.X, groundY), UiThemeLookup.Color(this, UiTokens.Color.LineStrong), 2);

        var center = new Vector2(size.X * 0.44f, groundY - 72);
        var front = center + new Vector2(86, 20);
        var rear = center + new Vector2(-86, 16);
        control.DrawLine(rear, center, UiThemeLookup.Color(this, UiTokens.Color.Accent), 5, antialiased: false);
        control.DrawLine(center, front, UiThemeLookup.Color(this, UiTokens.Color.Accent), 5, antialiased: false);
        // Purely decorative illustration -- themes without effects drop the glow
        // treatment for flat schematic dots instead of hiding them (#134).
        control.DrawCircle(
            rear,
            UiThemeLookup.EffectsEnabled(this) ? 18 : 8,
            UiThemeLookup.EffectsEnabled(this) ? UiGlow.FromBase(UiThemeLookup.Color(this, UiTokens.Color.Accent), true) : UiThemeLookup.Color(this, UiTokens.Color.Line));
        control.DrawCircle(center, UiThemeLookup.EffectsEnabled(this) ? 24 : 10, UiThemeLookup.EffectsEnabled(this) ? UiThemeLookup.Color(this, UiTokens.Color.Halo) : UiThemeLookup.Color(this, UiTokens.Color.LineStrong));
        control.DrawCircle(
            front,
            UiThemeLookup.EffectsEnabled(this) ? 18 : 8,
            UiThemeLookup.EffectsEnabled(this) ? UiGlow.FromBase(UiThemeLookup.Color(this, UiTokens.Color.Accent), true) : UiThemeLookup.Color(this, UiTokens.Color.Line));
    }

    private UiCard CreatePanel(bool raised)
    {
        return new UiCard
        {
            Kind = raised
                ? UiCard.CardVariant.Raised
                : UiCard.CardVariant.Frame,
        };
    }

    private UiButton CreateButton(string label, UiButtonKind kind, string tooltip)
    {
        return new UiButton
        {
            Kind = kind,
            Text = label,
            TooltipText = tooltip,
            CustomMinimumSize = new Vector2(128, UiSize.Control.Touch),
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
        label.CustomMinimumSize = new Vector2(110, UiSize.Control.Touch);
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
            CornerRadiusTopLeft = (int)UiSize.Radius.Large,
            CornerRadiusTopRight = (int)UiSize.Radius.Large,
            CornerRadiusBottomLeft = (int)UiSize.Radius.Large,
            CornerRadiusBottomRight = (int)UiSize.Radius.Large,
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
