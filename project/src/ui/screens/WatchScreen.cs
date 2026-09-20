using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// Watch shell bound to presentation data only. This scene does not bind to
/// simulation, managers, or persistence.
/// </summary>
public partial class WatchScreen : Control
{
    private UiTokens _tokens = UiTokens.Neon;
    private readonly List<UiPanel> _signalCards = new();
    private readonly List<Label> _signalBodies = new();
    private readonly List<ProgressBar> _sensorBars = new();
    private readonly List<ProgressBar> _motorBars = new();
    private readonly List<Control> _inputPassthroughExceptions = new();
    private int _selectedSignalIndex = -1;
    private TrainingPresentationViewModel? _presentation;
    private SignalFlowPresentationViewModel? _signalFlow;
    private Label? _seesStatusLabel;
    private Label? _decidesStatusLabel;
    private Label? _twistsStatusLabel;
    private Label? _scoresStatusLabel;
    private bool _inputPassthrough;

    [Signal]
    public delegate void BrainFocusRequestedEventHandler();

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

    public override void _Ready()
    {
        Name = nameof(WatchScreen);
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

    private void RebuildLayout()
    {
        _signalCards.Clear();
        _signalBodies.Clear();
        _sensorBars.Clear();
        _motorBars.Clear();
        _inputPassthroughExceptions.Clear();
        _seesStatusLabel = null;
        _decidesStatusLabel = null;
        _twistsStatusLabel = null;
        _scoresStatusLabel = null;
        _selectedSignalIndex = -1;
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        BuildLayout();
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

        var safeFrame = CreateMargin(24);
        AddChild(safeFrame);

        var screen = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        screen.AddThemeConstantOverride("separation", 14);
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
        contentRow.AddThemeConstantOverride("separation", 14);
        screen.AddChild(contentRow);

        contentRow.AddChild(CreateArenaPanel());
        contentRow.AddChild(CreateSignalPanel());

        if (!ReadOnlyControls)
        {
            screen.AddChild(CreateTrainingPanel());
        }

        ApplyInputPassthrough(this);
    }

    private Control CreateTopBar()
    {
        var topBar = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, _tokens.TouchTarget),
        };
        topBar.AddThemeConstantOverride("separation", 12);

        topBar.AddChild(CreateLabel("NODE RUNNER", 22, _tokens.Ink, expand: true));
        topBar.AddChild(CreatePill("Training", _tokens.AccentSoft, _tokens.Accent));
        topBar.AddChild(CreateModeButton("Watch", true));
        topBar.AddChild(CreateModeButton("Build", false));
        topBar.AddChild(CreateButton("Menu", UiActionButton.ActionKind.Secondary, "Overflow: Start over, settings, restore example"));

        return topBar;
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
        panel.CustomMinimumSize = new Vector2(336, 0);
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;

        var margin = CreateMargin(16);
        panel.AddChild(margin);

        var stack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        stack.AddThemeConstantOverride("separation", 10);
        margin.AddChild(stack);

        if (ReadOnlyControls)
        {
            stack.AddChild(CreateLiveTrainingSummary());
        }

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
            stack.AddChild(CreateLabel("Tap one stage to expand its explanation.", 13, _tokens.Muted));
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

    private Control CreateLiveTrainingSummary()
    {
        var panel = CreatePanel(raised: true);
        var margin = CreateMargin(12);
        panel.AddChild(margin);

        var stack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        stack.AddThemeConstantOverride("separation", 6);
        margin.AddChild(stack);

        var generationText = _presentation?.GenerationText ?? "Generation 5 · try 3 of 8";
        var best = _presentation is null || double.IsNegativeInfinity(_presentation.BestFitness)
            ? "—"
            : $"{_presentation.BestFitness:0.0} m";
        var mean = _presentation?.MeanFitness ?? 8.4;
        var profile = _presentation?.Profile ?? "Quick";
        stack.AddChild(CreateLabel("Training", 18, _tokens.Ink));
        stack.AddChild(CreateLabel(generationText, 15, _tokens.Ink));
        stack.AddChild(CreateLabel($"Best {best} · mean {mean:0.0} m", 13, _tokens.Muted));
        stack.AddChild(CreateLabel($"{profile} profile", 13, _tokens.Accent));
        stack.AddChild(CreateSampleStrip());

        return panel;
    }

    private Control CreateSampleStrip()
    {
        var strip = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 24),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        strip.AddThemeConstantOverride("separation", 6);

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
                CustomMinimumSize = new Vector2(34, 20),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
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
        card.CustomMinimumSize = new Vector2(312, 86);
        _signalCards.Add(card);

        var margin = CreateMargin(10);
        card.AddChild(margin);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 4);
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
                var heading = CreateLabel(title, 13, _tokens.Muted);
                heading.HorizontalAlignment = HorizontalAlignment.Center;
                heading.CustomMinimumSize = new Vector2(0, 24);
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
                _seesStatusLabel = CreateLabel(string.Empty, 12, _tokens.Muted);
                stack.AddChild(CreateThreeBarPreview(_sensorBars));
                stack.AddChild(_seesStatusLabel);
                break;
            case 1:
                _decidesStatusLabel = CreateLabel(string.Empty, 13, _tokens.Accent);
                _decidesStatusLabel.HorizontalAlignment = HorizontalAlignment.Center;
                stack.AddChild(_decidesStatusLabel);
                break;
            case 2:
                _twistsStatusLabel = CreateLabel(string.Empty, 12, _tokens.Muted);
                stack.AddChild(CreateTwoBarPreview(_motorBars));
                stack.AddChild(_twistsStatusLabel);
                break;
            case 3:
                _scoresStatusLabel = CreateLabel(string.Empty, 13, _tokens.Accent);
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
            CustomMinimumSize = new Vector2(0, 20),
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
            CustomMinimumSize = new Vector2(0, 20),
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
            CustomMinimumSize = new Vector2(0, 12),
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
            CustomMinimumSize = new Vector2(0, 12),
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
            _signalCards[cardIndex].State = selected ? UiPanel.PanelState.Focused : UiPanel.PanelState.Normal;
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
            Raised = raised,
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
            CornerRadiusTopLeft = (int)_tokens.Radius,
            CornerRadiusTopRight = (int)_tokens.Radius,
            CornerRadiusBottomLeft = (int)_tokens.Radius,
            CornerRadiusBottomRight = (int)_tokens.Radius,
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
