using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Screens;

/// <summary>
/// Visual inventory of the design foundations. The page frame, toolbar and menu are
/// authored in its scene like the other gallery pages; the inventory itself is built in code.
/// </summary>
public partial class ColorsAndStylesScreen : GalleryScreen
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
         "edge", "on-accent", "halo", "danger", "scrim", "output"];

    public static IReadOnlyList<string> TextStyleInventory { get; } =
        _textStyles.Select(style => style.Name).ToArray();

    public static IReadOnlyList<string> IconSizeInventory { get; } =
        ["icon-sm", "icon", "icon-lg", "icon-xl"];

    public static IReadOnlyList<string> RadiusInventory { get; } =
        ["radius-sm", "radius-md", "radius-lg", "radius-pill"];

    private ScrollContainer? _scroll;
    private Control? _scrollContent;
    private readonly List<TextureRect> _iconSpecimens = [];
    private readonly List<(PanelContainer Panel, float Radius)> _radiusSpecimens = [];

    protected override GalleryPage Page => GalleryPage.ColorsAndStyles;

    public override void _Ready()
    {
        base._Ready();
        BuildLayout();
        ApplyThemeColors();
        Callable.From(ResetScrollPosition).CallDeferred();
    }

    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationThemeChanged && IsNodeReady())
        {
            ApplyThemeColors();
        }
    }

    /// <summary>Text restyles through its variation; only drawn fills are re-resolved here.</summary>
    private void ApplyThemeColors()
    {
        foreach (var glyph in _iconSpecimens)
        {
            glyph.SelfModulate = UiThemeLookup.Color(this, UiTokens.Color.Accent);
        }

        foreach (var (panel, radius) in _radiusSpecimens)
        {
            panel.AddThemeStyleboxOverride(
                "panel",
                UiThemeLookup.CreateStyleBox(UiThemeLookup.Color(this, UiTokens.Color.PanelRaised), UiThemeLookup.Color(this, UiTokens.Color.LineStrong), radius: radius));
        }
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
        _scroll = GetNode<ScrollContainer>("%Scroll");
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", (int)UiSize.Space.S5);
        GetNode<MarginContainer>("%ContentFrame").AddChild(content);
        _scrollContent = content;
        content.AddChild(CreateColorsSection(UiThemes.Neon, "NEON LAB (DARK)"));
        content.AddChild(CreateColorsSection(UiThemes.Paper, "PAPER (LIGHT)"));
        content.AddChild(CreateSurfacesSection());
        content.AddChild(CreateIconsSection());
        content.AddChild(CreateTextStylesSection());
        content.AddChild(CreateRadiiSection());
        UiNativeScroll.AllowGesturesToBubble(content);
    }

    private Control CreateColorsSection(Godot.Theme theme, string title)
    {
        var themeScope = new Control { Theme = theme };
        var content = new GridContainer
        {
            Columns = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("h_separation", UiSpacing.ControlGap);
        content.AddThemeConstantOverride("v_separation", UiSpacing.IconLabelGap);
        foreach (var token in ColorTokenInventory)
        {
            content.AddChild(CreatePaletteRow(token, themeScope));
        }

        return WrapSection(title, content, theme);
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
        icons.AddThemeConstantOverride("separation", (int)UiSize.Space.S2);
        for (var index = 0; index < IconSizeInventory.Count; index++)
        {
            var size = index switch
            {
                0 => UiSize.Icon.Small,
                1 => UiSize.Icon.Default,
                2 => UiSize.Icon.Large,
                _ => UiSize.Icon.ExtraLarge,
            };
            var item = new VBoxContainer
            {
                CustomMinimumSize = new Vector2(UiLayout.ColumnMediumWidth, 0),
            };
            item.AddThemeConstantOverride("separation", (int)UiSize.Space.S1);
            var iconSize = index switch
            {
                0 => UiIconSize.Small,
                1 => UiIconSize.Standard,
                2 => UiIconSize.Large,
                _ => UiIconSize.ExtraLarge,
            };
            var glyph = UiIcons.Create(UiIconId.Gear, iconSize, Colors.White);
            _iconSpecimens.Add(glyph);
            glyph.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            item.AddChild(glyph);
            var name = CreateLabel(
                IconSizeInventory[index],
                UiTokens.Typography.ReadoutMedium,
                UiTokens.Color.Ink,
                TextServer.AutowrapMode.Off);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            item.AddChild(name);
            var sizeLabel = CreateLabel(
                $"{size:0}px",
                UiTokens.Typography.Note,
                UiTokens.Color.Muted,
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
                0 => UiSize.Radius.Small,
                1 => UiSize.Radius.Medium,
                2 => UiSize.Radius.Large,
                _ => UiSize.Radius.Pill,
            };
            var specimen = new PanelContainer
            {
                CustomMinimumSize = new Vector2(UiLayout.ColumnLargeWidth, UiSize.Control.Default),
                TooltipText = RadiusInventory[index],
                MouseFilter = MouseFilterEnum.Pass,
            };
            _radiusSpecimens.Add((specimen, radius));
            var label = CreateLabel(
                $"{RadiusInventory[index]} · {radius:0}",
                UiTokens.Typography.Caption,
                UiTokens.Color.Ink,
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
                CustomMinimumSize = new Vector2(0, UiSize.Control.Small),
            };
            row.AddThemeConstantOverride("separation", (int)UiSize.Space.S3);
            var nameLabel = CreateLabel(
                name,
                UiTokens.Typography.ReadoutMedium,
                UiTokens.Color.Ink,
                TextServer.AutowrapMode.Off);
            nameLabel.CustomMinimumSize = new Vector2(UiLayout.ColumnLargeWidth, 0);
            row.AddChild(nameLabel);
            var descriptionLabel = CreateLabel(
                description,
                UiTokens.Typography.Note,
                UiTokens.Color.Muted,
                TextServer.AutowrapMode.Off);
            descriptionLabel.CustomMinimumSize = new Vector2(UiLayout.SidePanelWidth, 0);
            row.AddChild(descriptionLabel);
            var preview = CreateLabel(sample, TextStyleFor(name), UiTokens.Color.Ink);
            preview.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(preview);
            content.AddChild(row);
        }
        return WrapSection("TEXT STYLES", content);
    }

    private Control CreateSurfaceSpecimen(string label, UiCard.CardVariant variant, bool glow = false)
    {
        var panel = new UiCard
        {
            Kind = variant,
            Glow = glow,
            CustomMinimumSize = new Vector2(UiLayout.ColumnLargeWidth, 0),
        };
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", (int)UiSize.Space.S3);
        margin.AddThemeConstantOverride("margin_right", (int)UiSize.Space.S3);
        margin.AddThemeConstantOverride("margin_top", (int)UiSize.Space.S2);
        margin.AddThemeConstantOverride("margin_bottom", (int)UiSize.Space.S2);
        panel.AddChild(margin);
        margin.AddChild(CreateLabel(label, UiTokens.Typography.Body, UiTokens.Color.Ink, TextServer.AutowrapMode.Off));
        return panel;
    }

    private Control CreatePaletteRow(string name, Control themeScope)
    {
        var color = ColorFor(themeScope, name);
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", UiSpacing.IconLabelGap);
        var swatch = new ColorRect
        {
            Color = color,
            CustomMinimumSize = new Vector2(UiSize.Control.ExtraSmall, UiSize.Control.ExtraSmall),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        row.AddChild(swatch);
        var label = CreateLabel(
            name,
            UiTokens.Typography.Caption,
            UiTokens.Color.Ink,
            TextServer.AutowrapMode.Off);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(label);
        return row;
    }

    private HFlowContainer CreateFlow()
    {
        var flow = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        flow.AddThemeConstantOverride("h_separation", UiSpacing.ControlGap);
        flow.AddThemeConstantOverride("v_separation", UiSpacing.ControlGap);
        return flow;
    }

    private Control WrapSection(string title, Control content, Godot.Theme? surfaceTheme = null)
    {
        var panel = new UiCard
        {
            Kind = UiCard.CardVariant.Frame,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        if (surfaceTheme is not null)
        {
            panel.Theme = surfaceTheme;
        }

        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "top", "right", "bottom" })
        {
            margin.AddThemeConstantOverride($"margin_{side}", (int)UiSize.Space.S3);
        }
        panel.AddChild(margin);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", (int)UiSize.Space.S2);
        margin.AddChild(stack);
        stack.AddChild(CreateLabel(
            title,
            UiTokens.Typography.Heading,
            UiTokens.Color.Accent,
            TextServer.AutowrapMode.Off));
        stack.AddChild(content);
        return panel;
    }

    private Label CreateLabel(
        string text,
        UiTokens.Typography textStyle,
        UiTokens.Color colorToken,
        TextServer.AutowrapMode autowrap = TextServer.AutowrapMode.WordSmart)
    {
        var label = new Label { Text = text, AutowrapMode = autowrap, MouseFilter = MouseFilterEnum.Ignore };
        UiThemeLookup.ApplyTextStyle(label, textStyle, colorToken);
        return label;
    }

    protected override void OnThemeApplied()
    {
        if (_scrollContent is not null)
        {
            UiNativeScroll.AllowGesturesToBubble(_scrollContent);
        }
    }

    private static UiTokens.Typography TextStyleFor(string name) =>
        name switch
        {
            "title" => UiTokens.Typography.Title,
            "heading" => UiTokens.Typography.Heading,
            "subheading" => UiTokens.Typography.Subheading,
            "stage" => UiTokens.Typography.Stage,
            "body" => UiTokens.Typography.Body,
            "body-strong" => UiTokens.Typography.BodyStrong,
            "small" => UiTokens.Typography.Small,
            "small-strong" => UiTokens.Typography.SmallStrong,
            "label" => UiTokens.Typography.Label,
            "note" => UiTokens.Typography.Note,
            "note-strong" => UiTokens.Typography.NoteStrong,
            "caption" => UiTokens.Typography.Caption,
            "overline" => UiTokens.Typography.Overline,
            "readout-lg" => UiTokens.Typography.ReadoutLarge,
            "readout" => UiTokens.Typography.Readout,
            "readout-md" => UiTokens.Typography.ReadoutMedium,
            "readout-sm" => UiTokens.Typography.ReadoutSmall,
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };

    private static Color ColorFor(Control owner, string name) =>
        name switch
        {
            "bg" => UiThemeLookup.Color(owner, UiTokens.Color.Background),
            "panel" => UiThemeLookup.Color(owner, UiTokens.Color.Panel),
            "panel-raised" => UiThemeLookup.Color(owner, UiTokens.Color.PanelRaised),
            "line" => UiThemeLookup.Color(owner, UiTokens.Color.Line),
            "line-strong" => UiThemeLookup.Color(owner, UiTokens.Color.LineStrong),
            "ink" => UiThemeLookup.Color(owner, UiTokens.Color.Ink),
            "muted" => UiThemeLookup.Color(owner, UiTokens.Color.Muted),
            "accent" => UiThemeLookup.Color(owner, UiTokens.Color.Accent),
            "edge" => UiThemeLookup.Color(owner, UiTokens.Color.Edge),
            "on-accent" => UiThemeLookup.Color(owner, UiTokens.Color.OnAccent),
            "halo" => UiThemeLookup.Color(owner, UiTokens.Color.Halo),
            "danger" => UiThemeLookup.Color(owner, UiTokens.Color.Danger),
            "scrim" => UiThemeLookup.Color(owner, UiTokens.Color.Scrim),
            "output" => UiThemeLookup.Color(owner, UiTokens.Color.Output),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
        };

}
