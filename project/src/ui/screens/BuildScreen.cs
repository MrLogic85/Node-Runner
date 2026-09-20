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
    private const string _hostedInputPassthroughMeta = "HostedInputPassthrough";
    private const int _topBarHeight = 64;
    private const int _modeSwitchHeight = 52;
    private const int _toolRailWidth = 84;
    private const int _toolButtonHeight = 76;
    private UiTokens _tokens = UiTokens.Neon;
    private ConstructionPresentationViewModel? _presentation;
    private bool _isSubscribedToPresentation;

    [Export]
    public bool ShowTopBar { get; set; } = true;

    [Export]
    public bool Hosted { get; set; }

    [Export]
    public bool ShowCanvasPreview { get; set; } = true;

    [Signal]
    public delegate void TrainingRequestedEventHandler();

    [Signal]
    public delegate void RebuildRequestedEventHandler();

    [Signal]
    public delegate void SimulateRequestedEventHandler();

    [Signal]
    public delegate void ToolRequestedEventHandler(ConstructionTool tool);

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
        MouseFilter = MouseFilterEnum.Ignore;
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
        if (Hosted)
        {
            ApplyHostedInputPassthrough(this);
        }
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
        if (!Hosted)
        {
            AddChild(new ColorRect
            {
                Color = _tokens.Background,
                MouseFilter = MouseFilterEnum.Ignore,
                AnchorRight = 1,
                AnchorBottom = 1,
            });
        }

        var safeFrame = MarkHostedInputPassthrough(CreateMargin(12));
        AddChild(safeFrame);

        var screenParent = safeFrame;
        if (!Hosted)
        {
            var frame = CreatePanel(raised: false);
            frame.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            frame.SizeFlagsVertical = SizeFlags.ExpandFill;
            safeFrame.AddChild(frame);

            var screenMargin = MarkHostedInputPassthrough(CreateMargin(0));
            frame.AddChild(screenMargin);
            screenParent = screenMargin;
        }

        var screen = MarkHostedInputPassthrough(new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        });
        screen.AddThemeConstantOverride("separation", 0);
        screenParent.AddChild(screen);

        if (ShowTopBar)
        {
            screen.AddChild(CreateTopBar());
        }

        var contentRow = MarkHostedInputPassthrough(new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        });
        contentRow.AddThemeConstantOverride("separation", 0);
        screen.AddChild(contentRow);

        contentRow.AddChild(CreateToolRail());
        contentRow.AddChild(CreateBuildCanvasPanel());
        contentRow.AddChild(CreateBrainPanel());
    }

    private Control CreateTopBar()
    {
        var topBarPanel = CreatePanel(raised: true);
        topBarPanel.CustomMinimumSize = new Vector2(0, _topBarHeight);
        topBarPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 4);
        margin.AddThemeConstantOverride("margin_top", 0);
        margin.AddThemeConstantOverride("margin_right", 4);
        margin.AddThemeConstantOverride("margin_bottom", 0);
        topBarPanel.AddChild(margin);

        var topBar = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, _topBarHeight),
        };
        topBar.AddThemeConstantOverride("separation", 8);
        margin.AddChild(topBar);

        topBar.AddChild(new UiIconButton
        {
            Tokens = _tokens,
            IconText = "‹",
            AccessibleLabel = "Back",
        });

        var titleStack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        titleStack.AddThemeConstantOverride("separation", 0);
        topBar.AddChild(titleStack);
        titleStack.AddChild(CreateLabel("Building", 22, _tokens.Ink, expand: true));
        titleStack.AddChild(CreateLabel("✓ Saved", 14, _tokens.Muted));

        topBar.AddChild(CreateModeSwitch());
        topBar.AddChild(new UiIconButton
        {
            Tokens = _tokens,
            IconText = "⋯",
            AccessibleLabel = "More build actions",
        });

        return topBarPanel;
    }

    private Control CreateToolRail()
    {
        var presentation = Presentation;
        var panel = CreatePanel(raised: true);
        panel.CustomMinimumSize = new Vector2(_toolRailWidth, 0);
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;

        var margin = CreateMargin(0);
        panel.AddChild(margin);

        var rail = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        rail.AddThemeConstantOverride("separation", 2);
        margin.AddChild(rail);

        rail.AddChild(CreateBuildToolButton(
            ConstructionTool.Place,
            presentation?.PlaceToolText ?? "Move",
            ToolButtonKind(ConstructionTool.Place),
            presentation is null ? "Move sample nodes" : ConstructionPresentationViewModel.ToolHint(ConstructionTool.Place),
            locked: false));
        rail.AddChild(CreateBuildToolButton(
            ConstructionTool.Beam,
            presentation?.BeamToolText ?? "Beam",
            ToolButtonKind(ConstructionTool.Beam),
            presentation is null
                ? "Connect two sample nodes"
                : presentation.LockTopologyTools
                    ? presentation.MoveOnlyLockReason
                    : ConstructionPresentationViewModel.ToolHint(ConstructionTool.Beam),
            presentation?.LockTopologyTools ?? false));
        rail.AddChild(CreateBuildToolButton(
            ConstructionTool.Core,
            CompactCoreToolText(presentation?.CoreToolText) ?? "Core",
            ToolButtonKind(ConstructionTool.Core),
            presentation?.CoreToolTooltip ?? "Attach sample core",
            presentation?.LockTopologyTools ?? false));
        rail.AddChild(CreateBuildToolButton(
            ConstructionTool.Delete,
            presentation?.DeleteToolText ?? "Delete",
            ToolButtonKind(ConstructionTool.Delete),
            presentation is null
                ? "Remove sample part"
                : presentation.LockTopologyTools
                    ? presentation.MoveOnlyLockReason
                    : ConstructionPresentationViewModel.ToolHint(ConstructionTool.Delete),
            presentation?.LockTopologyTools ?? false));
        rail.AddChild(CreateSpacer());

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

    private UiActionButton.ActionKind ToolButtonKind(ConstructionTool tool) =>
        Presentation?.ActiveTool == tool ? UiActionButton.ActionKind.Primary : UiActionButton.ActionKind.Secondary;

    private Button CreateBuildToolButton(ConstructionTool tool, string label, UiActionButton.ActionKind kind, string tooltip, bool locked)
    {
        var active = kind == UiActionButton.ActionKind.Primary;
        var button = new Button
        {
            Text = label.ToUpperInvariant(),
            TooltipText = tooltip,
            Disabled = locked,
            CustomMinimumSize = new Vector2(_toolRailWidth, _toolButtonHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        button.AddThemeFontSizeOverride("font_size", 15);
        button.AddThemeColorOverride("font_color", locked ? _tokens.Muted : _tokens.Ink);
        button.AddThemeColorOverride("font_hover_color", _tokens.Ink);
        button.AddThemeColorOverride("font_pressed_color", _tokens.Ink);
        button.AddThemeColorOverride("font_disabled_color", _tokens.Muted);
        button.AddThemeStyleboxOverride("normal", CreateToolStyle(active, locked));
        button.AddThemeStyleboxOverride("hover", CreateToolStyle(true, locked));
        button.AddThemeStyleboxOverride("pressed", CreateToolStyle(true, locked));
        button.AddThemeStyleboxOverride("focus", CreateToolStyle(true, locked, 3));
        button.AddThemeStyleboxOverride("disabled", CreateToolStyle(false, locked, opacity: 0.5f));
        if (locked)
        {
            return button;
        }

        button.Pressed += () => EmitSignal(SignalName.ToolRequested, (int)tool);
        return button;
    }

    private Control CreateBuildCanvasPanel()
    {
        var panel = MarkHostedInputPassthrough(ShowCanvasPreview ? CreatePanel(raised: true) : new Control());
        panel.MouseFilter = MouseFilterEnum.Ignore;
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;

        var margin = MarkHostedInputPassthrough(CreateMargin(0));
        margin.MouseFilter = MouseFilterEnum.Ignore;
        panel.AddChild(margin);

        var layout = MarkHostedInputPassthrough(new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        });
        margin.AddChild(layout);

        var placeholder = MarkHostedInputPassthrough(new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        });
        placeholder.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        placeholder.Draw += () =>
        {
            DrawBuildGrid(placeholder);
            if (ShowCanvasPreview)
            {
                DrawBuildPreview(placeholder);
            }
        };
        layout.AddChild(placeholder);

        return panel;
    }

    private Control CreateBrainPanel()
    {
        var buildPanel = Presentation?.BuildPanel ?? ConstructionBuildPanelPresentation.Sample;
        var panel = CreatePanel(raised: true);
        panel.CustomMinimumSize = new Vector2(320, 0);
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;

        var margin = CreateMargin(18);
        panel.AddChild(margin);

        var stack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        stack.AddThemeConstantOverride("separation", 10);
        margin.AddChild(stack);

        stack.AddChild(CreateLabel(ConstructionBuildPanelPresentation.Title, 20, _tokens.Muted));
        stack.AddChild(CreateBrainPreview(buildPanel));
        stack.AddChild(CreateLabel(BuildBrainCountLine(buildPanel), 17, _tokens.Muted));
        stack.AddChild(CreateValidationLine(buildPanel));
        stack.AddChild(CreateSpacer());
        if (Presentation?.ShowRebuildAction == true)
        {
            var rebuild = CreateButton(Presentation.RebuildActionText, UiActionButton.ActionKind.Danger, Presentation.RebuildConfirmationBody);
            rebuild.Pressed += () => EmitSignal(SignalName.RebuildRequested);
            stack.AddChild(rebuild);
        }
        else
        {
            var startTraining = CreateButton(
                "Save + train",
                UiActionButton.ActionKind.Primary,
                buildPanel.DisabledReason ?? "Save this body and start training in Simulate");
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
        }

        return panel;
    }

    private Control CreateBrainPreview(ConstructionBuildPanelPresentation buildPanel)
    {
        var preview = new Control
        {
            CustomMinimumSize = new Vector2(0, 72),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        preview.Draw += () => DrawBrainPreview(preview, buildPanel);
        return preview;
    }

    private static string BuildBrainCountLine(ConstructionBuildPanelPresentation buildPanel)
    {
        var motorWord = buildPanel.OutputCount == 1 ? "motor" : "motors";
        return $"{buildPanel.InputCount} senses · {buildPanel.OutputCount} {motorWord}";
    }

    private Control CreateValidationLine(ConstructionBuildPanelPresentation buildPanel)
    {
        var text = buildPanel.CanStartTraining
            ? "Ready to train"
            : ShortValidationText(buildPanel.DisabledReason ?? buildPanel.ValidationLine);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var icon = CreateLabel(buildPanel.CanStartTraining ? "✓" : "⚠", 20, buildPanel.CanStartTraining ? _tokens.Accent : _tokens.Danger);
        icon.CustomMinimumSize = new Vector2(28, 0);
        row.AddChild(icon);
        row.AddChild(CreateLabel(text, 18, buildPanel.CanStartTraining ? _tokens.Accent : _tokens.Danger, expand: true));
        return row;
    }

    private static string ShortValidationText(string text) =>
        text.StartsWith("Add nodes and beams", StringComparison.Ordinal)
            ? "Add nodes + beams"
            : text.Contains("has no beams attached", StringComparison.Ordinal)
                ? "1 node not connected"
                : text;

    private void DrawBuildGrid(Control control)
    {
        var size = control.Size;
        for (var x = 48f; x < size.X; x += 48f)
        {
            control.DrawLine(new Vector2(x, 0), new Vector2(x, size.Y), _tokens.Line, 1);
        }

        for (var y = 48f; y < size.Y; y += 48f)
        {
            control.DrawLine(new Vector2(0, y), new Vector2(size.X, y), _tokens.Line, 1);
        }

        DrawCornerMarks(control, size);
    }

    private void DrawBuildPreview(Control control)
    {
        var size = control.Size;
        var leftHip = new Vector2(size.X * 0.28f, size.Y * 0.42f);
        var top = new Vector2(size.X * 0.36f, size.Y * 0.26f);
        var rightHip = new Vector2(size.X * 0.44f, size.Y * 0.42f);
        var leftKnee = new Vector2(size.X * 0.24f, size.Y * 0.58f);
        var leftFoot = new Vector2(size.X * 0.20f, size.Y * 0.76f);
        var rightKnee = new Vector2(size.X * 0.50f, size.Y * 0.60f);
        var rightFoot = new Vector2(size.X * 0.58f, size.Y * 0.76f);
        var brokenA = new Vector2(size.X * 0.72f, size.Y * 0.66f);
        var brokenB = new Vector2(size.X * 0.84f, size.Y * 0.86f);

        DrawTriangleFill(control, leftHip, top, rightHip);
        DrawBeam(control, leftHip, top, _tokens.Edge);
        DrawBeam(control, top, rightHip, _tokens.Edge);
        DrawBeam(control, leftHip, rightHip, _tokens.Edge);
        DrawBeam(control, leftHip, leftKnee, _tokens.Edge);
        DrawBeam(control, leftKnee, leftFoot, _tokens.Edge);
        DrawBeam(control, rightHip, rightKnee, _tokens.Edge);
        DrawBeam(control, rightKnee, rightFoot, _tokens.Edge);
        control.DrawDashedLine(brokenA, brokenB, _tokens.Danger, 4, 7, antialiased: true);

        DrawMotorArc(control, leftHip, clockwise: false);
        DrawMotorArc(control, rightHip, clockwise: true);
        DrawMotorArc(control, leftKnee, clockwise: false);
        DrawMotorArc(control, rightKnee, clockwise: true);

        DrawNode(control, leftHip, hasCore: false);
        DrawNode(control, top, hasCore: true);
        DrawNode(control, rightHip, hasCore: false);
        DrawNode(control, leftKnee, hasCore: false);
        DrawNode(control, leftFoot, hasCore: true, selected: true);
        DrawNode(control, rightKnee, hasCore: false);
        DrawNode(control, rightFoot, hasCore: true);
        DrawInvalidNode(control, brokenA);
        DrawInvalidNode(control, brokenB);

        DrawTag(control, "Rigid: no joints", top + new Vector2(-70, -42), _tokens.Halo);
        DrawTag(control, "⚠ Not connected", brokenA + new Vector2(-48, -46), _tokens.Danger);
    }

    private void DrawBrainPreview(Control control, ConstructionBuildPanelPresentation buildPanel)
    {
        var size = control.Size;
        if (buildPanel.InputCount == 0 || buildPanel.OutputCount == 0)
        {
            control.DrawString(
                ThemeDB.FallbackFont,
                new Vector2(8, size.Y * 0.58f),
                "Build anatomy to preview brain",
                HorizontalAlignment.Left,
                -1,
                17,
                _tokens.Muted);
            return;
        }

        var inputX = size.X * 0.08f;
        var hiddenX = size.X * 0.52f;
        var outputX = size.X * 0.92f;
        var inputPreviewCount = Mathf.Clamp(buildPanel.InputCount, 1, 6);
        var outputPreviewCount = Mathf.Clamp(buildPanel.OutputCount, 1, 6);
        var hiddenPreviewCount = Mathf.Clamp((inputPreviewCount + outputPreviewCount) / 2 + 1, 3, 6);
        var inputs = Enumerable.Range(0, inputPreviewCount).Select(index => PreviewPoint(inputX, size.Y, inputPreviewCount, index)).ToArray();
        var hidden = Enumerable.Range(0, hiddenPreviewCount).Select(index => PreviewPoint(hiddenX, size.Y, hiddenPreviewCount, index)).ToArray();
        var outputs = Enumerable.Range(0, outputPreviewCount).Select(index => PreviewPoint(outputX, size.Y, outputPreviewCount, index)).ToArray();
        foreach (var from in inputs)
        {
            foreach (var to in hidden)
            {
                control.DrawLine(from, to, _tokens.Accent with { A = 0.45f }, 1.2f, antialiased: true);
            }
        }

        foreach (var from in hidden)
        {
            foreach (var to in outputs)
            {
                control.DrawLine(from, to, _tokens.Accent with { A = 0.55f }, 1.2f, antialiased: true);
            }
        }

        foreach (var point in inputs.Concat(hidden).Concat(outputs))
        {
            control.DrawCircle(point, 5, _tokens.PanelRaised);
            control.DrawArc(point, 5, 0, Mathf.Tau, 18, _tokens.LineStrong, 1.5f, antialiased: true);
        }
    }

    private static Vector2 PreviewPoint(float x, float height, int count, int index)
    {
        var spacing = height / (count + 1);
        return new Vector2(x, spacing * (index + 1));
    }

    private void DrawCornerMarks(Control control, Vector2 size)
    {
        control.DrawLine(new Vector2(14, 14), new Vector2(42, 14), _tokens.Accent, 3);
        control.DrawLine(new Vector2(14, 14), new Vector2(14, 42), _tokens.Accent, 3);
        control.DrawLine(new Vector2(size.X - 42, 14), new Vector2(size.X - 14, 14), _tokens.Accent, 3);
        control.DrawLine(new Vector2(size.X - 14, 14), new Vector2(size.X - 14, 42), _tokens.Accent, 3);
    }

    private void DrawTriangleFill(Control control, Vector2 a, Vector2 b, Vector2 c)
    {
        control.DrawColoredPolygon([a, b, c], _tokens.Muted with { A = 0.10f });
    }

    private void DrawBeam(Control control, Vector2 start, Vector2 end, Color color)
    {
        control.DrawLine(start, end, color, 5, antialiased: true);
    }

    private void DrawNode(Control control, Vector2 position, bool hasCore, bool selected = false)
    {
        if (selected)
        {
            control.DrawCircle(position, 28, _tokens.Halo);
            control.DrawCircle(position, 23, _tokens.Background);
        }

        if (_tokens.EffectsEnabled)
        {
            control.DrawCircle(position, 22, _tokens.AccentGlow);
        }

        control.DrawCircle(position, 12, _tokens.Panel);
        control.DrawArc(position, 12, 0, Mathf.Tau, 24, _tokens.LineStrong, 3, antialiased: true);
        if (hasCore)
        {
            var half = new Vector2(12, 12);
            var points = new[]
            {
                position + new Vector2(0, -half.Y),
                position + new Vector2(half.X, 0),
                position + new Vector2(0, half.Y),
                position + new Vector2(-half.X, 0),
            };
            control.DrawPolyline(points.Append(points[0]).ToArray(), _tokens.Accent, 3, antialiased: true);
        }
    }

    private void DrawInvalidNode(Control control, Vector2 position)
    {
        control.DrawCircle(position, 12, _tokens.Panel);
        control.DrawArc(position, 12, 0, Mathf.Tau, 24, _tokens.Danger, 3, antialiased: true);
    }

    private void DrawMotorArc(Control control, Vector2 center, bool clockwise)
    {
        var start = clockwise ? -0.35f : 0.8f;
        var end = clockwise ? 1.0f : 2.1f;
        control.DrawArc(center, 28, start, end, 20, _tokens.Accent, 3, antialiased: true);
    }

    private void DrawTag(Control control, string text, Vector2 position, Color borderColor)
    {
        var width = Mathf.Max(126, text.Length * 10);
        control.DrawRect(new Rect2(position, new Vector2(width, 30)), _tokens.PanelRaised);
        control.DrawRect(new Rect2(position, new Vector2(width, 30)), borderColor, filled: false, width: 2);
        control.DrawString(ThemeDB.FallbackFont, position + new Vector2(12, 21), text, HorizontalAlignment.Left, -1, 16, _tokens.Ink);
    }

    private UiPanel CreatePanel(bool raised)
    {
        return new UiPanel
        {
            Tokens = _tokens,
            Raised = raised,
        };
    }

    private Control CreateModeSwitch()
    {
        var frame = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, _modeSwitchHeight),
        };
        frame.AddThemeConstantOverride("separation", 0);
        frame.AddChild(CreateModeSegment("▶  Simulate", active: false, first: true, last: false));
        frame.AddChild(CreateModeSegment("✎  Build", active: true, first: false, last: true));
        return frame;
    }

    private Button CreateModeSegment(string label, bool active, bool first, bool last)
    {
        var button = new Button
        {
            Text = label.ToUpperInvariant(),
            CustomMinimumSize = new Vector2(148, _modeSwitchHeight),
            Disabled = active,
        };
        button.AddThemeFontSizeOverride("font_size", 16);
        button.AddThemeColorOverride("font_color", _tokens.Ink);
        button.AddThemeColorOverride("font_disabled_color", _tokens.Ink);
        button.AddThemeColorOverride("font_hover_color", _tokens.Ink);
        button.AddThemeStyleboxOverride("normal", CreateSegmentStyle(active, first, last));
        button.AddThemeStyleboxOverride("hover", CreateSegmentStyle(true, first, last));
        button.AddThemeStyleboxOverride("pressed", CreateSegmentStyle(true, first, last));
        button.AddThemeStyleboxOverride("disabled", CreateSegmentStyle(active, first, last));
        if (!active)
        {
            button.Pressed += () => EmitSignal(SignalName.SimulateRequested);
        }
        return button;
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

    private StyleBoxFlat CreateSegmentStyle(bool active, bool first, bool last)
    {
        var radius = (int)_tokens.Radius;
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

    private StyleBoxFlat CreateToolStyle(bool active, bool locked, int borderWidth = 1, float opacity = 1)
    {
        var radius = (int)_tokens.Radius;
        var border = active ? _tokens.Accent : _tokens.LineStrong;
        var alpha = opacity * (locked ? 0.5f : 1f);
        return new StyleBoxFlat
        {
            BgColor = active
                ? new Color(_tokens.AccentSoft.R, _tokens.AccentSoft.G, _tokens.AccentSoft.B, _tokens.AccentSoft.A * alpha)
                : new Color(_tokens.PanelRaised.R, _tokens.PanelRaised.G, _tokens.PanelRaised.B, _tokens.PanelRaised.A * alpha),
            BorderColor = new Color(border.R, border.G, border.B, border.A * alpha),
            BorderWidthLeft = active ? 2 : borderWidth,
            BorderWidthTop = active ? 2 : borderWidth,
            BorderWidthRight = active ? 2 : borderWidth,
            BorderWidthBottom = active ? 2 : borderWidth,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = 2,
            ContentMarginRight = 2,
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

    private static T MarkHostedInputPassthrough<T>(T control) where T : Control
    {
        control.SetMeta(_hostedInputPassthroughMeta, true);
        return control;
    }

    private Label CreateLabel(string text, int fontSize, Color color, bool expand = false)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = expand ? SizeFlags.ExpandFill : SizeFlags.Fill,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private void ApplyHostedInputPassthrough(Node node)
    {
        if (node is Control control)
        {
            control.MouseFilter = control == this || control.HasMeta(_hostedInputPassthroughMeta)
                ? MouseFilterEnum.Ignore
                : MouseFilterEnum.Stop;
        }

        foreach (var child in node.GetChildren())
        {
            ApplyHostedInputPassthrough(child);
        }
    }
}
