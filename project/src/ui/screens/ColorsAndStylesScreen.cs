using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// Standalone visual inventory for the design foundations. This screen is
/// intentionally independent from the component gallery and can be wired to a
/// scene later without changing the token or component controls.
/// </summary>
public partial class ColorsAndStylesScreen : Control
{
    private static readonly (string Name, string Description, string Sample)[] _textStyles =
    [
        ("title", "700 28/32 Chakra Petch", "Walker-1"),
        ("heading", "600 16/20 Chakra Petch", "Train Walker-1"),
        ("subheading", "600 14/18 Chakra Petch", "Front knee"),
        ("stage", "600 11/14 Chakra Petch, upper", "SENSES"),
        ("body", "400 13/18 Barlow", "Shadows race and the brain keeps learning."),
        ("body-strong", "600 13/18 Barlow", "Left thigh"),
        ("small", "400 12/16 Barlow", "Replays the best brain."),
        ("small-strong", "600 12/16 Barlow", "Front thigh"),
        ("label", "600 12/16 Barlow, upper", "START TRAINING"),
        ("note", "400 11/14 Barlow", "Beams meet here. Nodes have no weight."),
        ("note-strong", "600 11/14 Barlow", "Saved"),
        ("caption", "500 10/13 Barlow", "Left foot"),
        ("overline", "600 10/13 Barlow, upper", "MAX STRENGTH"),
        ("readout-lg", "600 22/24 JetBrains Mono", "12.4 m"),
        ("readout", "500 13/16 JetBrains Mono", "1.0"),
        ("readout-md", "500 12/16 JetBrains Mono", "40 N·m"),
        ("readout-sm", "500 10/13 JetBrains Mono", "100%"),
    ];

    public static IReadOnlyList<string> ColorTokenInventory { get; } =
        ["bg", "panel", "panel-raised", "line", "line-strong", "ink", "muted", "accent",
         "edge", "accent-soft", "accent-glow", "on-accent", "halo", "danger", "scrim", "output"];

    public static IReadOnlyList<string> TextStyleInventory { get; } =
        _textStyles.Select(style => style.Name).ToArray();

    public static IReadOnlyList<string> IconSizeInventory { get; } =
        ["icon-sm", "icon", "icon-lg", "icon-xl"];

    public static IReadOnlyList<string> RadiusInventory { get; } =
        ["radius-sm", "radius-md", "radius-lg", "radius-pill"];

    [Signal]
    public delegate void CloseRequestedEventHandler();

    [Export]
    public bool ShowCloseAction { get; set; }

    private readonly List<Action<UiTokens>> _tokenAppliers = new();
    private readonly List<Action<UiTokens>> _labelAppliers = new();
    private UiTokens _tokens = UiTokens.Neon;
    private ColorRect? _background;
    private ScrollContainer? _scroll;
    private Control? _scrollContent;

    public override void _Ready()
    {
        Name = nameof(ColorsAndStylesScreen);
        UiLayout.ApplyScreen(this);
        BuildLayout();
        ApplyTokens(_tokens);
        Callable.From(ResetScrollPosition).CallDeferred();
    }

    private void ResetScrollPosition()
    {
        if (_scroll is not null)
        {
            _scroll.ScrollVertical = 0;
        }
    }

    private void BuildLayout()
    {
        _background = new ColorRect { MouseFilter = MouseFilterEnum.Ignore };
        _background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_background);

        var frame = new MarginContainer();
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        UiLayout.ApplyMargins(frame, _tokens);
        AddChild(frame);

        var shell = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        shell.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        frame.AddChild(shell);
        shell.AddChild(CreateHeader());

        _scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever,
        };
        shell.AddChild(_scroll);

        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", (int)_tokens.Space5);
        _scroll.AddChild(content);
        _scrollContent = content;
        content.AddChild(CreateColorsSection(UiTokens.Neon, "NEON LAB (DARK)"));
        content.AddChild(CreateColorsSection(UiTokens.Paper, "PAPER (LIGHT)"));
        content.AddChild(CreateSurfacesSection());
        content.AddChild(CreateIconsSection());
        content.AddChild(CreateTextStylesSection());
        content.AddChild(CreateRadiiSection());
        UiNativeScroll.AllowGesturesToBubble(content);
    }

    private Control CreateHeader()
    {
        var header = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, UiLayout.TopBarHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        header.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        var title = CreateLabel("Colors & Styles", _tokens.HeadingText, tokens => tokens.Ink);
        title.AutowrapMode = TextServer.AutowrapMode.Off;
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);

        if (ShowCloseAction)
        {
            var close = Track(new UiSecondaryIconButton
            {
                IconId = UiIconId.Back,
                AccessibleLabel = "Back",
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            });
            close.Pressed += () => EmitSignal(SignalName.CloseRequested);
            header.AddChild(close);
        }

        var switcher = Track(new UiSegmentedSwitch
        {
            Options = ["Neon", "Paper"],
            SelectedIndex = 0,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        });
        switcher.SelectionChanged += index => ApplyTokens(index == 1 ? UiTokens.Paper : UiTokens.Neon);
        header.AddChild(switcher);
        return header;
    }

    private Control CreateColorsSection(UiTokens paletteTokens, string title)
    {
        var content = new GridContainer
        {
            Columns = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("h_separation", UiSpacing.ControlGap(_tokens));
        content.AddThemeConstantOverride("v_separation", UiSpacing.IconLabelGap(_tokens));
        foreach (var token in ColorTokenInventory)
        {
            content.AddChild(CreatePaletteRow(token, ColorFor(paletteTokens, token), paletteTokens));
        }

        return WrapSection(title, content, paletteTokens);
    }

    private Control CreateSurfacesSection()
    {
        var content = CreateFlow();
        content.AddChild(CreateSurfaceSpecimen("frame", UiCard.CardVariant.Frame));
        content.AddChild(CreateSurfaceSpecimen("sel", UiCard.CardVariant.Selected));
        content.AddChild(CreateSurfaceSpecimen("lock", UiCard.CardVariant.Locked));
        content.AddChild(CreateSurfaceSpecimen("warn", UiCard.CardVariant.Warning));
        content.AddChild(CreateSurfaceSpecimen("hint", UiCard.CardVariant.Hint));
        content.AddChild(CreateSurfaceSpecimen("glow", UiCard.CardVariant.Frame, glow: true));
        content.AddChild(CreateSurfaceSpecimen("raised", UiCard.CardVariant.Raised));
        return WrapSection("SURFACES", content);
    }

    private Control CreateIconsSection()
    {
        var icons = new HBoxContainer();
        icons.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        for (var index = 0; index < IconSizeInventory.Count; index++)
        {
            var size = index switch
            {
                0 => _tokens.IconSmall,
                1 => _tokens.Icon,
                2 => _tokens.IconLarge,
                _ => _tokens.IconExtraLarge,
            };
            var item = new VBoxContainer
            {
                CustomMinimumSize = new Vector2(_tokens.ColumnMediumWidth, 0),
            };
            item.AddThemeConstantOverride("separation", (int)_tokens.Space1);
            var iconSize = index switch
            {
                0 => UiIconSize.Small,
                1 => UiIconSize.Standard,
                2 => UiIconSize.Large,
                _ => UiIconSize.ExtraLarge,
            };
            var glyph = UiIcons.Create(UiIconId.Gear, iconSize, _tokens.Accent);
            glyph.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            item.AddChild(glyph);
            var name = CreateLabel(
                IconSizeInventory[index],
                _tokens.ReadoutMediumText,
                tokens => tokens.Ink,
                TextServer.AutowrapMode.Off);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            item.AddChild(name);
            var sizeLabel = CreateLabel(
                $"{size:0}px",
                _tokens.NoteText,
                tokens => tokens.Muted,
                TextServer.AutowrapMode.Off);
            sizeLabel.HorizontalAlignment = HorizontalAlignment.Center;
            item.AddChild(sizeLabel);
            icons.AddChild(item);
        }
        return WrapSection("ICONS", icons);
    }

    private Control CreateRadiiSection()
    {
        var radii = CreateFlow();
        for (var index = 0; index < RadiusInventory.Count; index++)
        {
            var radius = index switch
            {
                0 => _tokens.RadiusSmall,
                1 => _tokens.RadiusMedium,
                2 => _tokens.RadiusLarge,
                _ => _tokens.RadiusPill,
            };
            var specimen = new PanelContainer
            {
                CustomMinimumSize = new Vector2(_tokens.ColumnLargeWidth, _tokens.ControlHeight),
                TooltipText = RadiusInventory[index],
                MouseFilter = MouseFilterEnum.Pass,
            };
            specimen.AddThemeStyleboxOverride(
                "panel",
                _tokens.ControlStyle(_tokens.PanelRaised, _tokens.LineStrong, radius: radius));
            _tokenAppliers.Add(tokens => specimen.AddThemeStyleboxOverride(
                "panel",
                tokens.ControlStyle(tokens.PanelRaised, tokens.LineStrong, radius: radius)));
            var label = CreateLabel(
                $"{RadiusInventory[index]} · {radius:0}",
                _tokens.CaptionText,
                tokens => tokens.Ink,
                TextServer.AutowrapMode.Off);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            specimen.AddChild(label);
            radii.AddChild(specimen);
        }
        return WrapSection("RADIUS", radii);
    }

    private Control CreateTextStylesSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 0);
        foreach (var (name, description, sample) in _textStyles)
        {
            var row = new HBoxContainer
            {
                CustomMinimumSize = new Vector2(0, _tokens.ControlSmall),
            };
            row.AddThemeConstantOverride("separation", (int)_tokens.Space3);
            var nameLabel = CreateLabel(
                name,
                _tokens.ReadoutMediumText,
                tokens => tokens.Ink,
                TextServer.AutowrapMode.Off);
            nameLabel.CustomMinimumSize = new Vector2(_tokens.ColumnLargeWidth, 0);
            row.AddChild(nameLabel);
            var descriptionLabel = CreateLabel(
                description,
                _tokens.NoteText,
                tokens => tokens.Muted,
                TextServer.AutowrapMode.Off);
            descriptionLabel.CustomMinimumSize = new Vector2(_tokens.SidePanelWidth, 0);
            row.AddChild(descriptionLabel);
            var preview = CreateLabel(sample, TextStyleFor(_tokens, name), tokens => tokens.Ink);
            preview.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(preview);
            content.AddChild(row);
        }
        return WrapSection("TEXT STYLES", content);
    }

    private Control CreateSurfaceSpecimen(string label, UiCard.CardVariant variant, bool glow = false)
    {
        var panel = Track(new UiCard
        {
            Kind = variant,
            Glow = glow,
            CustomMinimumSize = new Vector2(_tokens.ColumnLargeWidth, 0),
        });
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", (int)_tokens.Space3);
        margin.AddThemeConstantOverride("margin_right", (int)_tokens.Space3);
        margin.AddThemeConstantOverride("margin_top", (int)_tokens.Space2);
        margin.AddThemeConstantOverride("margin_bottom", (int)_tokens.Space2);
        panel.AddChild(margin);
        margin.AddChild(CreateLabel(label, _tokens.BodyText, tokens => tokens.Ink, TextServer.AutowrapMode.Off));
        return panel;
    }

    private Control CreatePaletteRow(string name, Color color, UiTokens paletteTokens)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", UiSpacing.IconLabelGap(_tokens));
        var swatch = new ColorRect
        {
            Color = color,
            CustomMinimumSize = new Vector2(_tokens.ControlExtraSmall, _tokens.ControlExtraSmall),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        row.AddChild(swatch);
        var label = CreateLabel(
            name,
            _tokens.CaptionText,
            _ => paletteTokens.Ink,
            TextServer.AutowrapMode.Off);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(label);
        return row;
    }

    private HFlowContainer CreateFlow()
    {
        var flow = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        flow.AddThemeConstantOverride("h_separation", UiSpacing.ControlGap(_tokens));
        flow.AddThemeConstantOverride("v_separation", UiSpacing.ControlGap(_tokens));
        return flow;
    }

    private Control WrapSection(string title, Control content, UiTokens? surfaceTokens = null)
    {
        var panel = new UiCard
        {
            Kind = UiCard.CardVariant.Frame,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        if (surfaceTokens is null)
        {
            Track(panel);
        }
        else
        {
            panel.Tokens = surfaceTokens;
        }

        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "top", "right", "bottom" })
        {
            margin.AddThemeConstantOverride($"margin_{side}", (int)_tokens.Space3);
        }
        panel.AddChild(margin);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        margin.AddChild(stack);
        stack.AddChild(CreateLabel(
            title,
            _tokens.HeadingText,
            tokens => surfaceTokens?.Accent ?? tokens.Accent,
            TextServer.AutowrapMode.Off));
        stack.AddChild(content);
        return panel;
    }

    private Label CreateLabel(
        string text,
        UiTokens.TextStyle textStyle,
        Func<UiTokens, Color> colorForTokens,
        TextServer.AutowrapMode autowrap = TextServer.AutowrapMode.WordSmart)
    {
        var label = new Label { Text = text, AutowrapMode = autowrap, MouseFilter = MouseFilterEnum.Ignore };
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
            case UiButton button:
                button.Tokens = tokens;
                break;
            case UiSegmentedSwitch segmentedSwitch:
                segmentedSwitch.Tokens = tokens;
                break;
            case UiCard panel:
                panel.Tokens = tokens;
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

        if (_scrollContent is not null)
        {
            UiNativeScroll.AllowGesturesToBubble(_scrollContent);
        }
    }

    private static UiTokens.TextStyle TextStyleFor(UiTokens tokens, string name) =>
        name switch
        {
            "title" => tokens.TitleText,
            "heading" => tokens.HeadingText,
            "subheading" => tokens.SubheadingText,
            "stage" => tokens.StageText,
            "body" => tokens.BodyText,
            "body-strong" => tokens.BodyStrongText,
            "small" => tokens.SmallText,
            "small-strong" => tokens.SmallStrongText,
            "label" => tokens.LabelText,
            "note" => tokens.NoteText,
            "note-strong" => tokens.NoteStrongText,
            "caption" => tokens.CaptionText,
            "overline" => tokens.OverlineText,
            "readout-lg" => tokens.ReadoutLargeText,
            "readout" => tokens.ReadoutText,
            "readout-md" => tokens.ReadoutMediumText,
            "readout-sm" => tokens.ReadoutSmallText,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };

    private static Color ColorFor(UiTokens tokens, string name) =>
        name switch
        {
            "bg" => tokens.Background,
            "panel" => tokens.Panel,
            "panel-raised" => tokens.PanelRaised,
            "line" => tokens.Line,
            "line-strong" => tokens.LineStrong,
            "ink" => tokens.Ink,
            "muted" => tokens.Muted,
            "accent" => tokens.Accent,
            "edge" => tokens.Edge,
            "accent-soft" => tokens.AccentSoft,
            "accent-glow" => tokens.AccentGlow,
            "on-accent" => tokens.OnAccent,
            "halo" => tokens.Halo,
            "danger" => tokens.Danger,
            "scrim" => tokens.Scrim,
            "output" => tokens.Output,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };

}
