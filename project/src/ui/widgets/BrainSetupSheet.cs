using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The Build screen's Brain setup sheet: hidden layers and neurons per layer, with a live
/// preview. Still built in code; #344 rewrites it with the component library.
/// </summary>
public partial class BrainSetupSheet : Control
{
    private ConstructionPresentationViewModel? _presentation;

    [Signal]
    public delegate void BrainShapeChangedEventHandler(int hiddenLayers, int neuronsPerLayer);

    public bool IsOpen => Visible;

    public ConstructionPresentationViewModel? Presentation
    {
        get => _presentation;
        set
        {
            if (_presentation is not null)
            {
                _presentation.PresentationChanged -= OnPresentationChanged;
            }

            _presentation = value;
            if (_presentation is not null && IsInsideTree())
            {
                _presentation.PresentationChanged += OnPresentationChanged;
            }
        }
    }

    public override void _EnterTree()
    {
        if (_presentation is not null)
        {
            _presentation.PresentationChanged += OnPresentationChanged;
        }
    }

    public override void _ExitTree()
    {
        if (_presentation is not null)
        {
            _presentation.PresentationChanged -= OnPresentationChanged;
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged && IsNodeReady() && Visible)
        {
            UiThemeRefresh.Guarded(this, Rebuild);
        }
    }

    public void Open()
    {
        Show();
        Rebuild();
    }

    public void Close()
    {
        Hide();
        Clear();
    }

    private void OnPresentationChanged(object? sender, EventArgs eventArgs)
    {
        if (Visible)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        Clear();
        AddChild(CreateBrainSetupOverlay());
    }

    private void Clear()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }
    }

    private Control CreateBrainSetupOverlay()
    {
        var overlay = new Control
        {
            Name = "BrainSetupOverlay",
            MouseFilter = MouseFilterEnum.Stop,
            AnchorRight = 1,
            AnchorBottom = 1,
        };
        overlay.AddChild(new ColorRect
        {
            Color = UiThemeLookup.Color(this, UiTokens.Color.Scrim).WithAlpha(0.45f),
            AnchorRight = 1,
            AnchorBottom = 1,
            MouseFilter = MouseFilterEnum.Stop,
        });

        var sheet = CreatePanel(raised: true);
        sheet.Position = new Vector2(76, 52);
        sheet.CustomMinimumSize = new Vector2(488, 260);
        overlay.AddChild(sheet);

        var margin = CreateMargin((int)UiSize.Space.S3);
        sheet.AddChild(margin);

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", (int)UiSize.Space.S2);
        margin.AddChild(layout);
        layout.AddChild(CreateBrainSetupTopBar());

        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", (int)UiSize.Space.S3);
        layout.AddChild(body);

        var controls = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(270, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        controls.AddThemeConstantOverride("separation", (int)UiSize.Space.S2);
        body.AddChild(controls);
        controls.AddChild(CreateLayerChooser());
        controls.AddChild(CreateNeuronControl());

        body.AddChild(CreateBrainPreviewPanel());
        return overlay;
    }

    private Control CreateBrainSetupTopBar()
    {
        var buildPanel = Presentation?.BuildPanel ?? ConstructionBuildPanelPresentation.Sample;
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", (int)UiSize.Space.S2);
        row.AddChild(CreateLabel("Brain setup", 16, UiThemeLookup.Color(this, UiTokens.Color.Ink), expand: true));
        var recommended = new Button
        {
            Text = "Use recommended",
            CustomMinimumSize = new Vector2(124, 32),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            TooltipText = "Reset layers and neurons",
        };
        UiThemeLookup.ApplyTypography(recommended, UiTokens.Typography.Caption);
        recommended.AddThemeColorOverride("font_color", UiThemeLookup.Color(this, UiTokens.Color.Ink));
        recommended.AddThemeStyleboxOverride("normal", UiThemeLookup.CreateStyleBox(UiThemeLookup.Color(this, UiTokens.Color.PanelRaised), UiThemeLookup.Color(this, UiTokens.Color.Edge), radius: (int)UiSize.Radius.Small));
        recommended.AddThemeStyleboxOverride(
            "hover",
            UiThemeLookup.CreateStyleBox(UiThemeLookup.Color(this, UiTokens.Color.Accent).WithAlpha(UiThemeLookup.Alpha(this, UiTokens.Alpha.Soft)),
                UiThemeLookup.Color(this, UiTokens.Color.Accent),
                radius: (int)UiSize.Radius.Small));
        recommended.Pressed += () => EmitBrainShape(BrainShapeDef.DefaultHiddenLayers, RecommendedNeurons(buildPanel));
        row.AddChild(recommended);
        var close = new Button
        {
            Text = string.Empty,
            CustomMinimumSize = new Vector2(36, 36),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            TooltipText = "Close brain setup",
        };
        UiThemeLookup.ApplyTypography(close, UiTokens.Typography.Heading);
        close.AddThemeColorOverride("font_color", UiThemeLookup.Color(this, UiTokens.Color.Accent));
        UiIcons.Apply(close, UiIconId.Close, UiIconSize.Standard, UiThemeLookup.Color(this, UiTokens.Color.Accent));
        close.AddThemeStyleboxOverride("normal", UiThemeLookup.CreateStyleBox(UiThemeLookup.Color(this, UiTokens.Color.PanelRaised), UiThemeLookup.Color(this, UiTokens.Color.Edge), radius: (int)UiSize.Radius.Small));
        close.AddThemeStyleboxOverride(
            "hover",
            UiThemeLookup.CreateStyleBox(UiThemeLookup.Color(this, UiTokens.Color.Accent).WithAlpha(UiThemeLookup.Alpha(this, UiTokens.Alpha.Soft)),
                UiThemeLookup.Color(this, UiTokens.Color.Accent),
                radius: (int)UiSize.Radius.Small));
        close.Pressed += Close;
        row.AddChild(close);
        return row;
    }

    private Control CreateLayerChooser()
    {
        var shape = Presentation?.BrainShape ?? BrainShapeDef.Default;
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", (int)UiSize.Space.S1);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", (int)UiSize.Space.S1);
        stack.AddChild(row);
        row.AddChild(CreateLayerButton(1, "simple", shape.HiddenLayers == 1));
        row.AddChild(CreateLayerButton(2, "navigation", shape.HiddenLayers == 2));
        row.AddChild(CreateLayerButton(3, "experiment", shape.HiddenLayers == 3));
        var help = shape.HiddenLayers switch
        {
            1 => "Recommended for simple tasks",
            2 => "Recommended for harder navigation",
            _ => "! Not recommended: slow to learn. For experiments.",
        };
        stack.AddChild(CreateLabel(help, 12, shape.HiddenLayers == 3 ? UiThemeLookup.Color(this, UiTokens.Color.Halo) : UiThemeLookup.Color(this, UiTokens.Color.Muted), expand: true));
        return stack;
    }

    private Control CreateLayerButton(int layers, string caption, bool active)
    {
        var shape = Presentation?.BrainShape ?? BrainShapeDef.Default;
        var button = new Button
        {
            Text = $"{layers}\n{caption}".ToUpperInvariant(),
            CustomMinimumSize = new Vector2(82, 42),
        };
        UiThemeLookup.ApplyTypography(button, UiTokens.Typography.Caption);
        button.AddThemeColorOverride("font_color", active ? UiThemeLookup.Color(this, UiTokens.Color.OnAccent) : UiThemeLookup.Color(this, UiTokens.Color.Ink));
        button.AddThemeStyleboxOverride("normal", UiThemeLookup.CreateStyleBox(active ? UiThemeLookup.Color(this, UiTokens.Color.Accent) : UiThemeLookup.Color(this, UiTokens.Color.PanelRaised), active ? UiThemeLookup.Color(this, UiTokens.Color.Accent) : UiThemeLookup.Color(this, UiTokens.Color.Edge)));
        button.AddThemeStyleboxOverride(
            "hover",
            UiThemeLookup.CreateStyleBox(UiThemeLookup.Color(this, UiTokens.Color.Accent).WithAlpha(UiThemeLookup.Alpha(this, UiTokens.Alpha.Soft)),
                UiThemeLookup.Color(this, UiTokens.Color.Accent)));
        button.Pressed += () => EmitBrainShape(layers, shape.NeuronsPerLayer);
        return button;
    }

    private Control CreateNeuronControl()
    {
        var shape = Presentation?.BrainShape ?? BrainShapeDef.Default;
        var buildPanel = Presentation?.BuildPanel ?? ConstructionBuildPanelPresentation.Sample;
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", (int)UiSize.Space.S1);
        stack.AddChild(CreateLabel($"Neurons per layer · tick = default {RecommendedNeurons(buildPanel)}", 12, UiThemeLookup.Color(this, UiTokens.Color.Muted)));
        stack.AddChild(CreateLabel(shape.HiddenLayers == 1 ? "Layer 1 shares this value" : $"Layers 1-{shape.HiddenLayers} share this value", 10, UiThemeLookup.Color(this, UiTokens.Color.Muted)));

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", (int)UiSize.Space.S1);
        stack.AddChild(row);
        row.AddChild(CreateStepper("−", -1, "Decrease neurons"));
        var minimumNeurons = BrainShapeDef.MinimumNeuronsPerLayer;
        var maximumNeurons = BrainShapeDef.MaximumNeuronsPerLayer;
        var slider = new UiSlider
        {
            LabelText = "Neurons",
            ReadoutText = shape.NeuronsPerLayer.ToString(),
            Value = UiSliderValue.Thumb(
                (shape.NeuronsPerLayer - minimumNeurons) /
                (double)(maximumNeurons - minimumNeurons)),
            StepLabels = [minimumNeurons.ToString(), maximumNeurons.ToString()],
            MarkerPosition =
                (RecommendedNeurons(buildPanel) - minimumNeurons) /
                (double)(maximumNeurons - minimumNeurons),
            MarkerText = $"default {RecommendedNeurons(buildPanel)}",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        slider.ThumbChangeCommitted += (_, position) =>
        {
            var neurons = (int)Math.Round(minimumNeurons + (position * (maximumNeurons - minimumNeurons)));
            EmitBrainShape(shape.HiddenLayers, neurons);
        };
        row.AddChild(slider);
        row.AddChild(CreateStepper("+", 1, "Increase neurons"));
        return stack;
    }

    private UiButton CreateStepper(string label, int delta, string accessibleLabel)
    {
        var shape = Presentation?.BrainShape ?? BrainShapeDef.Default;
        var button = new UiButton
        {
            Text = label,
            TooltipText = accessibleLabel,
            ContentLayout = UiButtonContentLayout.RowCompact,
        };
        button.Activated += () => EmitBrainShape(
            shape.HiddenLayers,
            Mathf.Clamp(shape.NeuronsPerLayer + delta, BrainShapeDef.MinimumNeuronsPerLayer, BrainShapeDef.MaximumNeuronsPerLayer));
        return button;
    }

    private Control CreateBrainPreviewPanel()
    {
        var panel = CreatePanel(raised: false);
        panel.CustomMinimumSize = new Vector2(170, 176);
        var margin = CreateMargin((int)UiSize.Space.S2);
        panel.AddChild(margin);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", (int)UiSize.Space.S2);
        margin.AddChild(stack);
        stack.AddChild(CreateLabel("Live preview", 14, UiThemeLookup.Color(this, UiTokens.Color.Ink)));
        stack.AddChild(CreateBrainSetupPreview());
        var connections = ConnectionCount();
        stack.AddChild(CreateLabel($"{connections:0} connections", 14, UiThemeLookup.Color(this, UiTokens.Color.Accent)));
        return panel;
    }

    private Control CreateBrainSetupPreview()
    {
        var preview = new Control
        {
            CustomMinimumSize = new Vector2(0, 96),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        preview.Draw += () => DrawBrainSetupPreview(preview);
        return preview;
    }

    private void DrawBrainSetupPreview(Control control)
    {
        var buildPanel = Presentation?.BuildPanel ?? ConstructionBuildPanelPresentation.Sample;
        var shape = Presentation?.BrainShape ?? BrainShapeDef.Default;
        if (buildPanel.InputCount <= 0 || buildPanel.OutputCount <= 0)
        {
            control.DrawString(
                ThemeDB.FallbackFont,
                new Vector2(12, control.Size.Y / 2),
                "Add anatomy to preview brain",
                HorizontalAlignment.Left,
                control.Size.X - 24,
                10,
                UiThemeLookup.Color(this, UiTokens.Color.Muted));
            return;
        }

        var layers = new[] { buildPanel.InputCount }.Concat(Enumerable.Repeat(shape.NeuronsPerLayer, shape.HiddenLayers)).Concat([buildPanel.OutputCount]).ToArray();
        var spacingX = control.Size.X / (layers.Length + 1);
        var font = ThemeDB.FallbackFont;
        var nodeColumns = new List<List<Vector2>>();
        for (var layer = 0; layer < layers.Length; layer++)
        {
            var count = layers[layer];
            var shown = Math.Min(6, Math.Max(1, count));
            var x = spacingX * (layer + 1);
            var column = new List<Vector2>();
            var header = layer == 0
                ? "Senses"
                : layer == layers.Length - 1
                    ? "Motors"
                    : $"H{layer}";
            control.DrawString(font, new Vector2(x - 24, 10), header, HorizontalAlignment.Center, 48, 8, UiThemeLookup.Color(this, UiTokens.Color.Muted));
            for (var i = 0; i < shown; i++)
            {
                var y = 14 + ((control.Size.Y - 34) / (shown + 1) * (i + 1));
                var point = new Vector2(x, y);
                column.Add(point);
                var color = layer == 0 || layer == layers.Length - 1 ? UiThemeLookup.Color(this, UiTokens.Color.Accent) : UiThemeLookup.Color(this, UiTokens.Color.LineStrong);
                control.DrawArc(point, 4, 0, Mathf.Tau, 18, color, 1.5f, antialiased: false);
            }

            var label = count > 6 ? $"+{count - 6} more" : $"{count}";
            control.DrawString(font, new Vector2(x - 22, control.Size.Y - 3), label, HorizontalAlignment.Center, 44, 10, UiThemeLookup.Color(this, UiTokens.Color.Muted));
            nodeColumns.Add(column);
        }

        for (var layer = 0; layer < nodeColumns.Count - 1; layer++)
        {
            foreach (var from in nodeColumns[layer])
            {
                foreach (var to in nodeColumns[layer + 1])
                {
                    control.DrawLine(from, to, new Color(UiThemeLookup.Color(this, UiTokens.Color.Edge), 0.35f), 0.5f, antialiased: false);
                }
            }
        }
    }

    private void EmitBrainShape(int hiddenLayers, int neuronsPerLayer)
    {
        EmitSignal(SignalName.BrainShapeChanged, hiddenLayers, neuronsPerLayer);
    }

    private int RecommendedNeurons(ConstructionBuildPanelPresentation buildPanel) =>
        Mathf.Clamp((int)Math.Ceiling((buildPanel.InputCount + buildPanel.OutputCount) / 2.0), BrainShapeDef.MinimumNeuronsPerLayer, BrainShapeDef.MaximumNeuronsPerLayer);

    private int ConnectionCount()
    {
        var buildPanel = Presentation?.BuildPanel ?? ConstructionBuildPanelPresentation.Sample;
        var shape = Presentation?.BrainShape ?? BrainShapeDef.Default;
        var layers = new[] { buildPanel.InputCount }.Concat(Enumerable.Repeat(shape.NeuronsPerLayer, shape.HiddenLayers)).Concat([buildPanel.OutputCount]).ToArray();
        var total = 0;
        for (var i = 0; i < layers.Length - 1; i++)
        {
            total += layers[i] * layers[i + 1];
        }

        return total;
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
            SizeFlagsHorizontal = expand ? SizeFlags.ExpandFill : SizeFlags.Fill,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }
}
