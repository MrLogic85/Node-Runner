using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// Build shell using sample presentation data by default, or injected App
/// presentation state when hosted by the live game. This scene does not bind
/// to simulation, managers, or persistence.
/// </summary>
public partial class BuildScreen : Control
{
    private UiTokens _tokens = UiTokens.Neon;
    private ConstructionPresentationViewModel? _presentation;
    private bool _isSubscribedToPresentation;

    [Export]
    public bool ShowTopBar { get; set; } = true;

    [Export]
    public bool Hosted { get; set; }

    [Signal]
    public delegate void TrainingRequestedEventHandler();

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

    public ConstructionPresentationViewModel? Presentation
    {
        get => _presentation;
        set
        {
            UnsubscribeFromPresentation();
            _presentation = value;

            if (IsInsideTree())
            {
                SubscribeToPresentation();
                RebuildLayout();
            }
        }
    }

    public override void _EnterTree()
    {
        SubscribeToPresentation();
    }

    public override void _Ready()
    {
        Name = nameof(BuildScreen);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (!Hosted)
        {
            Size = GetViewportRect().Size;
        }
        RebuildLayout();
    }

    public override void _ExitTree()
    {
        UnsubscribeFromPresentation();
    }

    private void RebuildLayout()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        BuildLayout();
    }

    private void OnPresentationChanged(object? sender, EventArgs eventArgs)
    {
        if (IsInsideTree())
        {
            RebuildLayout();
        }
    }

    private void SubscribeToPresentation()
    {
        if (_presentation is null || _isSubscribedToPresentation)
        {
            return;
        }

        _presentation.PresentationChanged += OnPresentationChanged;
        _isSubscribedToPresentation = true;
    }

    private void UnsubscribeFromPresentation()
    {
        if (_presentation is null || !_isSubscribedToPresentation)
        {
            return;
        }

        _presentation.PresentationChanged -= OnPresentationChanged;
        _isSubscribedToPresentation = false;
    }

    private void BuildLayout()
    {
        AddChild(new ColorRect
        {
            Color = _tokens.Background,
            MouseFilter = MouseFilterEnum.Ignore,
            AnchorRight = 1,
            AnchorBottom = 1,
        });

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

        contentRow.AddChild(CreateToolRail());
        contentRow.AddChild(CreateBuildCanvasPanel());
        contentRow.AddChild(CreateBrainPanel());
    }

    private Control CreateTopBar()
    {
        var topBar = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, _tokens.TouchTarget),
        };
        topBar.AddThemeConstantOverride("separation", 12);

        topBar.AddChild(CreateLabel("Build a creature", 22, _tokens.Ink, expand: true));
        topBar.AddChild(CreateModeButton("Simulate", false));
        topBar.AddChild(CreateModeButton("Build", true));
        topBar.AddChild(CreateButton("Saved", UiActionButton.ActionKind.Secondary, "Sample autosave status"));

        return topBar;
    }

    private Control CreateToolRail()
    {
        var presentation = Presentation;
        var panel = CreatePanel(raised: false);
        panel.CustomMinimumSize = new Vector2(132, 0);
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;

        var margin = CreateMargin(12);
        panel.AddChild(margin);

        var rail = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        rail.AddThemeConstantOverride("separation", 10);
        margin.AddChild(rail);

        rail.AddChild(CreateLabel("Tools", 18, _tokens.Ink));
        rail.AddChild(CreateToolButton(
            presentation?.PlaceToolText ?? "Move",
            UiActionButton.ActionKind.Primary,
            presentation is null ? "Move sample nodes" : ConstructionPresentationViewModel.ToolHint(ConstructionTool.Place),
            locked: false));
        rail.AddChild(CreateToolButton(
            presentation?.BeamToolText ?? "Beam",
            UiActionButton.ActionKind.Secondary,
            presentation is null
                ? "Connect two sample nodes"
                : presentation.LockTopologyTools
                    ? presentation.MoveOnlyLockReason
                    : ConstructionPresentationViewModel.ToolHint(ConstructionTool.Beam),
            presentation?.LockTopologyTools ?? false));
        rail.AddChild(CreateToolButton(
            CompactCoreToolText(presentation?.CoreToolText) ?? "Core",
            UiActionButton.ActionKind.Secondary,
            presentation?.CoreToolTooltip ?? "Attach sample core",
            presentation?.LockTopologyTools ?? false));
        if (presentation is not null)
        {
            rail.AddChild(CreateLabel(presentation.CoreToolTooltip, 12, _tokens.Muted));
        }

        rail.AddChild(CreateToolButton(
            presentation?.DeleteToolText ?? "Delete",
            UiActionButton.ActionKind.Danger,
            presentation is null
                ? "Remove sample part"
                : presentation.LockTopologyTools
                    ? presentation.MoveOnlyLockReason
                    : ConstructionPresentationViewModel.ToolHint(ConstructionTool.Delete),
            presentation?.LockTopologyTools ?? false));
        rail.AddChild(CreateSpacer());
        rail.AddChild(CreateLabel(presentation?.InspectorValues ?? "Tap empty space to place a node.", 13, _tokens.Muted));

        return panel;
    }

    private static string? CompactCoreToolText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var hintStart = text.IndexOf(" (", StringComparison.Ordinal);
        return hintStart < 0 ? text : text[..hintStart];
    }

    private UiActionButton CreateToolButton(string label, UiActionButton.ActionKind kind, string tooltip, bool locked)
    {
        var button = CreateButton(label, kind, tooltip);
        button.Locked = locked;
        if (locked)
        {
            button.LockReason = tooltip;
            button.ShowLockReasonInText = false;
        }

        return button;
    }

    private Control CreateBuildCanvasPanel()
    {
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

        layout.AddChild(CreateLabel("Build canvas", 20, _tokens.Ink));
        layout.AddChild(CreateLabel("Sample anatomy only · no simulation state connected", 14, _tokens.Muted));

        var placeholder = new Control
        {
            CustomMinimumSize = new Vector2(0, 360),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        placeholder.Draw += () => DrawBuildPlaceholder(placeholder);
        layout.AddChild(placeholder);

        layout.AddChild(CreateLabel("Hint: closed triangles are rigid and do not twist.", 14, _tokens.Accent));

        return panel;
    }

    private Control CreateBrainPanel()
    {
        var buildPanel = Presentation?.BuildPanel ?? ConstructionBuildPanelPresentation.Sample;
        var panel = CreatePanel(raised: false);
        panel.CustomMinimumSize = new Vector2(280, 0);
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;

        var margin = CreateMargin(16);
        panel.AddChild(margin);

        var stack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        stack.AddThemeConstantOverride("separation", 12);
        margin.AddChild(stack);

        stack.AddChild(CreateLabel(ConstructionBuildPanelPresentation.Title, 20, _tokens.Ink));
        stack.AddChild(CreateInfoCard("Inputs", buildPanel.InputSummary));
        stack.AddChild(CreateInfoCard("Motor relations", buildPanel.MotorRelationSummary));
        stack.AddChild(CreateInfoCard("Validation", buildPanel.ValidationLine));
        stack.AddChild(CreateInfoCard("Teaching note", ConstructionBuildPanelPresentation.TeachingNote));
        stack.AddChild(CreateSpacer());
        var startTraining = CreateButton(
            "Start training",
            UiActionButton.ActionKind.Primary,
            buildPanel.DisabledReason ?? "Start training with this anatomy");
        startTraining.Locked = !buildPanel.CanStartTraining;
        if (buildPanel.DisabledReason is not null)
        {
            startTraining.ShowLockReasonInText = false;
            startTraining.LockReason = buildPanel.DisabledReason;
        }
        if (buildPanel.CanStartTraining)
        {
            startTraining.Pressed += () => EmitSignal(SignalName.TrainingRequested);
        }
        stack.AddChild(startTraining);

        return panel;
    }

    private UiPanel CreateInfoCard(string title, string body)
    {
        var card = CreatePanel(raised: true);
        card.CustomMinimumSize = new Vector2(0, 78);

        var margin = CreateMargin(10);
        card.AddChild(margin);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 4);
        margin.AddChild(stack);

        stack.AddChild(CreateLabel(title, 15, _tokens.Accent));
        stack.AddChild(CreateLabel(body, 13, _tokens.Muted));

        return card;
    }

    private void DrawBuildPlaceholder(Control control)
    {
        var size = control.Size;
        for (var x = 0f; x < size.X; x += 36f)
        {
            control.DrawLine(new Vector2(x, 0), new Vector2(x, size.Y), _tokens.Line, 1);
        }

        for (var y = 0f; y < size.Y; y += 36f)
        {
            control.DrawLine(new Vector2(0, y), new Vector2(size.X, y), _tokens.Line, 1);
        }

        var a = new Vector2(size.X * 0.34f, size.Y * 0.48f);
        var b = new Vector2(size.X * 0.52f, size.Y * 0.32f);
        var c = new Vector2(size.X * 0.68f, size.Y * 0.52f);
        var d = new Vector2(size.X * 0.48f, size.Y * 0.65f);

        DrawBeam(control, a, b, _tokens.Accent);
        DrawBeam(control, b, c, _tokens.Accent);
        DrawBeam(control, c, d, _tokens.Accent);
        DrawBeam(control, d, a, _tokens.Accent);
        DrawBeam(control, a, c, _tokens.LineStrong);

        DrawNode(control, a, hasCore: true);
        DrawNode(control, b, hasCore: false);
        DrawNode(control, c, hasCore: true);
        DrawNode(control, d, hasCore: false);

        control.DrawArc(b, 44, 0.35f, 1.55f, 24, _tokens.EffectsEnabled ? _tokens.Halo : _tokens.Accent, 3, antialiased: true);
        control.DrawArc(d, 44, 3.75f, 4.95f, 24, _tokens.EffectsEnabled ? _tokens.Halo : _tokens.Accent, 3, antialiased: true);
    }

    private void DrawBeam(Control control, Vector2 start, Vector2 end, Color color)
    {
        control.DrawLine(start, end, color, 5, antialiased: true);
    }

    private void DrawNode(Control control, Vector2 position, bool hasCore)
    {
        if (_tokens.EffectsEnabled)
        {
            control.DrawCircle(position, 25, _tokens.AccentGlow);
        }

        control.DrawCircle(position, 17, _tokens.AccentSoft);
        control.DrawCircle(position, 17, _tokens.LineStrong);
        if (hasCore)
        {
            // The core marker must survive effects-lite mode (it's the only
            // signal a node has a core, not decoration) -- it just loses its
            // glow tint and renders as a plain schematic dot instead (#134).
            control.DrawCircle(position, 7, _tokens.EffectsEnabled ? _tokens.Halo : _tokens.OnAccent);
        }
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
            CustomMinimumSize = new Vector2(0, _tokens.TouchTarget),
        };
    }

    private Control CreateSpacer()
    {
        return new Control
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
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
}
