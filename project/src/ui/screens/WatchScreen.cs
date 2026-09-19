using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// Static Watch shell using sample presentation data only. This scene does not
/// bind to simulation, managers, or persistence.
/// </summary>
public partial class WatchScreen : Control
{
    private UiTokens _tokens = UiTokens.Neon;

    [Export]
    public bool ShowTopBar { get; set; } = true;

    [Export]
    public bool Hosted { get; set; }

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

        contentRow.AddChild(CreateArenaPanel());
        contentRow.AddChild(CreateSignalPanel());

        screen.AddChild(CreateTrainingPanel());
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

        layout.AddChild(CreateLabel("Arena preview", 20, _tokens.Ink));
        layout.AddChild(CreateLabel("Sample creature running on a flat test track", 14, _tokens.Muted));

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
        panel.CustomMinimumSize = new Vector2(260, 0);
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

        stack.AddChild(CreateLabel("SignalFlow", 20, _tokens.Ink));
        stack.AddChild(CreateSignalCard("1 Sees", "Sensors"));
        stack.AddChild(CreateSignalCard("2 Decides", "Brain choice"));
        stack.AddChild(CreateSignalCard("3 Twists", "Joint targets"));
        stack.AddChild(CreateSignalCard("4 Scores", "Distance"));
        stack.AddChild(CreateLabel("Tap a stage to expand details in a later slice.", 13, _tokens.Muted));

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

        summary.AddChild(CreateLabel("Generation 5 · try 3 of 8", 18, _tokens.Ink));
        summary.AddChild(CreateLabel("Best 12.8 m · mean 8.4 m · Quick profile", 14, _tokens.Muted));
        summary.AddChild(CreateSampleStrip());

        row.AddChild(CreateButton("Pause", UiActionButton.ActionKind.Secondary, "Pause sample training"));
        row.AddChild(CreateButton("Profile", UiActionButton.ActionKind.Secondary, "Open sample training settings"));

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

        for (var index = 1; index <= 8; index++)
        {
            var cell = new ColorRect
            {
                Color = index < 3
                    ? _tokens.AccentSoft
                    : index == 3
                        ? _tokens.Halo
                        : _tokens.Line,
                CustomMinimumSize = new Vector2(34, 20),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            strip.AddChild(cell);
        }

        return strip;
    }

    private UiPanel CreateSignalCard(string title, string body)
    {
        var card = CreatePanel(raised: true);
        card.CustomMinimumSize = new Vector2(0, 56);

        var margin = CreateMargin(10);
        card.AddChild(margin);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 4);
        margin.AddChild(stack);

        stack.AddChild(CreateLabel(title, 15, _tokens.Accent));
        stack.AddChild(CreateLabel(body, 13, _tokens.Muted));

        return card;
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
        control.DrawCircle(rear, 18, _tokens.AccentGlow);
        control.DrawCircle(center, 24, _tokens.Halo);
        control.DrawCircle(front, 18, _tokens.AccentGlow);
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
}
