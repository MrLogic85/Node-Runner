using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// Static Build shell using sample presentation data only. This scene does not
/// bind to simulation, managers, or persistence.
/// </summary>
public partial class BuildScreen : Control
{
    private UiTokens _tokens = UiTokens.Neon;

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

    private void RebuildLayout()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        BuildLayout();
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
        topBar.AddChild(CreateModeButton("Watch", false));
        topBar.AddChild(CreateModeButton("Build", true));
        topBar.AddChild(CreateButton("Saved", UiActionButton.ActionKind.Secondary, "Sample autosave status"));

        return topBar;
    }

    private Control CreateToolRail()
    {
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
        rail.AddChild(CreateButton("Move", UiActionButton.ActionKind.Primary, "Move sample nodes"));
        rail.AddChild(CreateButton("Beam", UiActionButton.ActionKind.Secondary, "Connect two sample nodes"));
        rail.AddChild(CreateButton("Core", UiActionButton.ActionKind.Secondary, "Attach sample core"));
        rail.AddChild(CreateButton("Delete", UiActionButton.ActionKind.Danger, "Remove sample part"));
        rail.AddChild(CreateSpacer());
        rail.AddChild(CreateLabel("Tap empty space to place a node.", 13, _tokens.Muted));

        return panel;
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

        stack.AddChild(CreateLabel("Brain it will get", 20, _tokens.Ink));
        stack.AddChild(CreateInfoCard("Inputs", "2 cores · 6 sensor values"));
        stack.AddChild(CreateInfoCard("Motor relations", "3 joints can twist"));
        stack.AddChild(CreateInfoCard("Validation", "Ready: at least one core and one motor relation"));
        stack.AddChild(CreateInfoCard("Teaching note", "Sees sensor values, decides joint targets, twists beams, then scores distance."));
        stack.AddChild(CreateSpacer());
        var startTraining = CreateButton("Start training", UiActionButton.ActionKind.Primary, "Sample route to Watch");
        startTraining.Pressed += () => EmitSignal(SignalName.TrainingRequested);
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

        control.DrawArc(b, 44, 0.35f, 1.55f, 24, _tokens.Halo, 3, antialiased: true);
        control.DrawArc(d, 44, 3.75f, 4.95f, 24, _tokens.Halo, 3, antialiased: true);
    }

    private void DrawBeam(Control control, Vector2 start, Vector2 end, Color color)
    {
        control.DrawLine(start, end, color, 5, antialiased: true);
    }

    private void DrawNode(Control control, Vector2 position, bool hasCore)
    {
        control.DrawCircle(position, 25, _tokens.AccentGlow);
        control.DrawCircle(position, 17, _tokens.AccentSoft);
        control.DrawCircle(position, 17, _tokens.LineStrong);
        if (hasCore)
        {
            control.DrawCircle(position, 7, _tokens.Halo);
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
