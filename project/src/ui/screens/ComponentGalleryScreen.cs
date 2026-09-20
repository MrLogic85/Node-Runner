using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// Standalone component-kit gallery for Phase 1 visual verification. It keeps
/// reusable controls disconnected from game state and lets developers switch
/// tokens live to prove token updates do not require rebuilding the screen.
/// </summary>
public partial class ComponentGalleryScreen : Control
{
    private readonly List<Action<UiTokens>> _tokenAppliers = new();
    private readonly List<Action<UiTokens>> _labelAppliers = new();
    private UiTokens _tokens = UiTokens.Neon;
    private ColorRect? _background;

    public override void _Ready()
    {
        Name = nameof(ComponentGalleryScreen);
        UiLayout.ApplyScreen(this);
        BuildLayout();
        ApplyTokens(_tokens);
    }

    private void BuildLayout()
    {
        _background = new ColorRect
        {
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_background);

        var frame = new MarginContainer();
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        UiLayout.ApplyMargins(frame, _tokens);
        AddChild(frame);

        var shell = new VBoxContainer();
        shell.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        shell.SizeFlagsVertical = SizeFlags.ExpandFill;
        shell.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        frame.AddChild(shell);

        shell.AddChild(CreateHeader());

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        shell.AddChild(scroll);

        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        scroll.AddChild(content);

        content.AddChild(CreateShellSection());
        content.AddChild(CreateActionsSection());
        content.AddChild(CreateToolsSection());
        content.AddChild(CreatePanelsAndReadoutsSection());
        content.AddChild(CreateInputsSection());
        content.AddChild(CreateOverlaysSection());
    }

    private Control CreateHeader()
    {
        var header = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, UiLayout.TopBarHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
        };
        header.AddThemeConstantOverride("separation", (int)_tokens.Space2);

        var title = CreateLabel("Component Gallery", _tokens.HeadingText, tokens => tokens.Ink);
        title.AutowrapMode = TextServer.AutowrapMode.Off;
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);

        var note = CreateLabel("Live token swap:", _tokens.BodyText, tokens => tokens.Muted);
        note.AutowrapMode = TextServer.AutowrapMode.Off;
        note.VerticalAlignment = VerticalAlignment.Center;
        header.AddChild(note);

        var switcher = Track(new UiSegmentedSwitch
        {
            Options = new[] { "Neon", "Paper", "Lite" },
            SelectedIndex = 0,
            CustomMinimumSize = new Vector2(248, _tokens.TouchTarget),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        });
        switcher.SelectionChanged += index =>
        {
            ApplyTokens(index switch
            {
                1 => UiTokens.Paper,
                2 => UiTokens.Neon.WithEffects(false),
                _ => UiTokens.Neon,
            });
        };
        header.AddChild(switcher);

        return header;
    }

    private Control CreateShellSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);

        var topBar = Track(new UiTopBar
        {
            TitleText = "Creation name",
            ShowBack = true,
            ShowOverflow = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });
        topBar.SetActions(
            new UiIconButton { IconText = UiIconGlyphs.Brain, AccessibleLabel = "Brain" },
            new UiIconButton { IconText = UiIconGlyphs.Train, AccessibleLabel = "Train" });
        content.AddChild(topBar);

        var metrics = new HBoxContainer();
        metrics.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        metrics.AddChild(Track(new UiChip { Text = "640 × 360", Kind = UiChip.ChipKind.Accent }));
        metrics.AddChild(Track(new UiChip { Text = "Top 48", Kind = UiChip.ChipKind.Neutral }));
        metrics.AddChild(Track(new UiChip { Text = "Rail 56", Kind = UiChip.ChipKind.Neutral }));
        metrics.AddChild(Track(new UiChip { Text = "Panel 172", Kind = UiChip.ChipKind.Neutral }));
        metrics.AddChild(Track(new UiChip { Text = "Touch 48", Kind = UiChip.ChipKind.Locked }));
        content.AddChild(metrics);

        return WrapSection("640 × 360 app shell", content);
    }

    private Control CreateActionsSection()
    {
        var content = new HBoxContainer();
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        content.AddChild(Track(new UiActionButton
        {
            Kind = UiActionButton.ActionKind.Primary,
            LabelText = "Primary action",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        }));
        content.AddChild(Track(new UiActionButton
        {
            Kind = UiActionButton.ActionKind.Secondary,
            LabelText = "Secondary",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        }));
        content.AddChild(Track(new UiActionButton
        {
            Kind = UiActionButton.ActionKind.Danger,
            LabelText = "Danger",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        }));
        content.AddChild(Track(new UiActionButton
        {
            LabelText = "Locked",
            Locked = true,
            LockReason = "needs creature",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        }));

        var iconRow = new HBoxContainer();
        iconRow.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        iconRow.AddChild(Track(new UiIconButton { IconText = "?", AccessibleLabel = "Help" }));
        iconRow.AddChild(Track(new UiIconButton { IconText = "+", AccessibleLabel = "Add" }));
        iconRow.AddChild(Track(new UiIconButton { IconText = "...", AccessibleLabel = "More" }));
        content.AddChild(iconRow);

        return WrapSection("Actions and icon buttons", content);
    }

    private Control CreateToolsSection()
    {
        var content = new HBoxContainer();
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        content.AddChild(Track(new UiToolButton
        {
            IconText = "↕",
            ToolLabel = "Move",
            Active = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        }));
        content.AddChild(Track(new UiToolButton
        {
            IconText = "─",
            ToolLabel = "Beam",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        }));
        content.AddChild(Track(new UiToolButton
        {
            IconText = "●",
            ToolLabel = "Core",
            Locked = true,
            LockReason = "unlocked later",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        }));
        content.AddChild(Track(new UiSegmentedSwitch
        {
            Options = new[] { "Train", "Simulate" },
            SelectedIndex = 1,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        }));

        return WrapSection("Tool buttons and segmented switch", content);
    }

    private Control CreatePanelsAndReadoutsSection()
    {
        var content = new HBoxContainer();
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);

        content.AddChild(CreatePanelExample("Normal", UiPanel.PanelState.Normal, raised: false));
        content.AddChild(CreatePanelExample("Focused", UiPanel.PanelState.Focused, raised: true));
        content.AddChild(CreatePanelExample("Danger", UiPanel.PanelState.Danger, raised: true));

        var readouts = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        readouts.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        readouts.AddChild(Track(new UiReadout
        {
            Caption = "Generation",
            ValueText = "12",
            Emphasis = true,
        }));
        readouts.AddChild(Track(new UiReadout
        {
            Caption = "Mean fitness",
            ValueText = "8.4 m",
        }));
        content.AddChild(readouts);

        return WrapSection("Panels and readouts", content);
    }

    private Control CreateInputsSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        content.AddChild(Track(new UiTokenSlider
        {
            LabelText = "Run length",
            MinValue = 1,
            MaxValue = 60,
            Value = 20,
        }));
        content.AddChild(Track(new UiTokenSlider
        {
            LabelText = "Shadows",
            MinValue = 0,
            MaxValue = 8,
            Value = 3,
        }));

        var chips = new HBoxContainer();
        chips.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        chips.AddChild(Track(new UiChip { Text = "Brain 1 × 4", Kind = UiChip.ChipKind.Accent }));
        chips.AddChild(Track(new UiChip { Text = "Spring locked", Kind = UiChip.ChipKind.Locked }));
        chips.AddChild(Track(new UiChip { Text = "Invalid", Kind = UiChip.ChipKind.Danger }));
        content.AddChild(chips);

        return WrapSection("Sliders and chips", content);
    }

    private Control CreateOverlaysSection()
    {
        var content = new HBoxContainer();
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);

        var menu = Track(new UiOverflowMenu
        {
            Visible = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });
        menu.SetActions(("open", "Open creation", false), ("duplicate", "Duplicate", false), ("delete", "Delete creation", true));
        menu.CallDeferred(CanvasItem.MethodName.Show);
        content.AddChild(menu);

        var sheet = Track(new UiSheet
        {
            Title = "Settings sheet",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });
        sheet.SetBody(CreateLabel("Compact modal surface with token-backed border and title.", _tokens.BodyText, tokens => tokens.Muted));
        content.AddChild(sheet);

        var toast = Track(new UiToast
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });
        toast.ShowMessage("Saved creation", "Undo", 60);
        content.AddChild(toast);

        return WrapSection("Overflow, sheet, and toast", content);
    }

    private Control CreatePanelExample(string title, UiPanel.PanelState state, bool raised)
    {
        var panel = Track(new UiPanel
        {
            State = state,
            Raised = raised,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", (int)_tokens.Space3);
        margin.AddThemeConstantOverride("margin_top", (int)_tokens.Space3);
        margin.AddThemeConstantOverride("margin_right", (int)_tokens.Space3);
        margin.AddThemeConstantOverride("margin_bottom", (int)_tokens.Space3);
        panel.AddChild(margin);
        margin.AddChild(CreateLabel(
            title,
            _tokens.BodyText,
            state == UiPanel.PanelState.Danger
                ? tokens => tokens.Danger
                : tokens => tokens.Ink));
        return panel;
    }

    private Control WrapSection(string title, Control content)
    {
        var panel = Track(new UiPanel
        {
            Raised = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", (int)_tokens.Space3);
        margin.AddThemeConstantOverride("margin_top", (int)_tokens.Space3);
        margin.AddThemeConstantOverride("margin_right", (int)_tokens.Space3);
        margin.AddThemeConstantOverride("margin_bottom", (int)_tokens.Space3);
        panel.AddChild(margin);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        margin.AddChild(stack);
        stack.AddChild(CreateLabel(title, _tokens.HeadingText, tokens => tokens.Accent));
        stack.AddChild(content);
        return panel;
    }

    private Label CreateLabel(string text, UiTokens.TextStyle textStyle, Func<UiTokens, Color> colorForTokens)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _tokens.ApplyTextStyle(label, textStyle);
        label.AddThemeColorOverride("font_color", colorForTokens(_tokens));
        _labelAppliers.Add(tokens =>
        {
            tokens.ApplyTextStyle(label, textStyle);
            label.AddThemeColorOverride("font_color", colorForTokens(tokens));
        });
        return label;
    }

    private T Track<T>(T control)
        where T : Control
    {
        SetTokens(control, _tokens);
        _tokenAppliers.Add(tokens => SetTokens(control, tokens));
        return control;
    }

    private static void SetTokens(Control control, UiTokens tokens)
    {
        switch (control)
        {
            case UiActionButton button:
                button.Tokens = tokens;
                break;
            case UiIconButton button:
                button.Tokens = tokens;
                break;
            case UiOverflowMenu menu:
                menu.Tokens = tokens;
                break;
            case UiTopBar topBar:
                topBar.Tokens = tokens;
                break;
            case UiReadout readout:
                readout.Tokens = tokens;
                break;
            case UiSegmentedSwitch segmentedSwitch:
                segmentedSwitch.Tokens = tokens;
                break;
            case UiSheet sheet:
                sheet.Tokens = tokens;
                break;
            case UiToast toast:
                toast.Tokens = tokens;
                break;
            case UiToolButton toolButton:
                toolButton.Tokens = tokens;
                break;
            case UiPanel panel:
                panel.Tokens = tokens;
                break;
            case UiChip chip:
                chip.Tokens = tokens;
                break;
            case UiTokenSlider slider:
                slider.Tokens = tokens;
                break;
        }
    }

    private void ApplyTokens(UiTokens tokens)
    {
        _tokens = tokens;
        if (_background is not null)
        {
            _background.Color = tokens.Background;
        }

        foreach (var apply in _tokenAppliers)
        {
            apply(tokens);
        }

        foreach (var apply in _labelAppliers)
        {
            apply(tokens);
        }
    }
}
