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
    private const string _rowInteractiveTitle =
        "Row — interactive selected, hold, disabled, compact, and badge";

    private const string _iconStandardTitle = "Icon — representative kinds and states";

    private const string _iconHoldTitle = "Hold — works the same as on a text button";

    private const string _iconBadgeTitle = "Badge — a halo counter, same colour whatever the kind";

    private const string _stackTitle = "Stack — play, tool, destructive, flat, and badge";

    [Signal]
    public delegate void CloseRequestedEventHandler();

    [Export]
    public bool ShowCloseAction { get; set; }

    [Export]
    public bool ShowDebugBounds
    {
        get => _showDebugBounds;
        set
        {
            _showDebugBounds = value;
            if (_boundsOverlay is not null)
            {
                _boundsOverlay.Visible = value;
                _boundsOverlay.SetProcess(value);
            }

            RefreshToolbarMenu();
        }
    }

    private bool _showDebugBounds;
    private UiBoundsDebugOverlay? _boundsOverlay;
    private UiButton? _toolbarMore;
    private UiOverflowMenu? _toolbarMenu;
    private Button? _toolbarDismiss;
    private readonly List<Action<UiTokens>> _tokenAppliers = new();
    private readonly List<Action<UiTokens>> _labelAppliers = new();
    private UiTokens _tokens = UiTokens.Neon;
    private ColorRect? _background;
    private ScrollContainer? _scroll;
    private Control? _scrollContent;

    public readonly record struct GalleryComponentSpec(
        UiComponentContracts.CanonicalComponent Component,
        string Section);

    public enum GallerySection
    {
        Actions,
        TextInput,
        Choices,
        Segmented,
        PartsTrayTabs,
        SelectionHandles,
        Number,
        Slider,
        Progress,
        Cards,
        OverflowMenu,
        Picker,
        PartRows,
        Panel,
        StageCard,
    }

    /// <summary>
    /// The single declarative source for every rendered button specimen in
    /// <see cref="CreateActionsSection"/>. <see cref="RowGroup"/> ties each
    /// specimen to the visual row it is rendered in, so the inventory and the
    /// live gallery can never drift apart.
    /// </summary>
    public readonly record struct ButtonGallerySpec(
        UiButtonKind Kind,
        UiButtonContentLayout Layout,
        string RowGroup,
        string Label,
        UiIconId? Icon = null,
        bool Selected = false,
        bool Enabled = true,
        bool Compact = false,
        bool Hold = false,
        string? BadgeText = null,
        bool ToggleOnActivate = false)
    {
        public bool Badge => BadgeText is not null;
    }

    public static IReadOnlyList<ButtonGallerySpec> ButtonSpecimenInventory { get; } =
    [
        new(UiButtonKind.Primary, UiButtonContentLayout.Row, _rowInteractiveTitle, "Start training", UiIconId.Play),
        new(UiButtonKind.Primary, UiButtonContentLayout.Row, _rowInteractiveTitle, "Start training", UiIconId.Play, Selected: true, ToggleOnActivate: true),
        new(UiButtonKind.Secondary, UiButtonContentLayout.Row, _rowInteractiveTitle, "Cancel"),
        new(UiButtonKind.Tertiary, UiButtonContentLayout.Row, _rowInteractiveTitle, "Hold to delete", Hold: true),
        new(UiButtonKind.Tertiary, UiButtonContentLayout.Row, _rowInteractiveTitle, "Delete", UiIconId.Trash, Enabled: false),
        new(UiButtonKind.Flat, UiButtonContentLayout.Row, _rowInteractiveTitle, "Skip"),
        new(UiButtonKind.Flat, UiButtonContentLayout.Row, _rowInteractiveTitle, "Skip", Selected: true, ToggleOnActivate: true),
        new(UiButtonKind.Secondary, UiButtonContentLayout.Row, _rowInteractiveTitle, "Cancel", Compact: true),
        new(UiButtonKind.Primary, UiButtonContentLayout.Row, _rowInteractiveTitle, "Start", UiIconId.Play, Compact: true),
        new(UiButtonKind.Tertiary, UiButtonContentLayout.Row, _rowInteractiveTitle, "Delete", UiIconId.Trash, Compact: true),
        new(UiButtonKind.Flat, UiButtonContentLayout.Row, _rowInteractiveTitle, "Skip", Compact: true),
        new(UiButtonKind.Secondary, UiButtonContentLayout.Row, _rowInteractiveTitle, "Creations", UiIconId.Model, BadgeText: "3"),
        new(UiButtonKind.Primary, UiButtonContentLayout.Row, _rowInteractiveTitle, "Hold to start training", Selected: true, Hold: true),
        new(UiButtonKind.Secondary, UiButtonContentLayout.Icon, _iconStandardTitle, string.Empty, UiIconId.Back),
        new(UiButtonKind.Secondary, UiButtonContentLayout.Icon, _iconStandardTitle, string.Empty, UiIconId.Back, Selected: true),
        new(UiButtonKind.Primary, UiButtonContentLayout.Icon, _iconStandardTitle, string.Empty, UiIconId.Gear),
        new(UiButtonKind.Tertiary, UiButtonContentLayout.Icon, _iconStandardTitle, string.Empty, UiIconId.Trash, Enabled: false),
        new(UiButtonKind.Flat, UiButtonContentLayout.Icon, _iconStandardTitle, string.Empty, UiIconId.More),
        new(UiButtonKind.Secondary, UiButtonContentLayout.Icon, _iconStandardTitle, string.Empty, UiIconId.Back, Compact: true),
        new(UiButtonKind.Primary, UiButtonContentLayout.Icon, _iconStandardTitle, string.Empty, UiIconId.Gear, Compact: true),
        new(UiButtonKind.Tertiary, UiButtonContentLayout.Icon, _iconStandardTitle, string.Empty, UiIconId.Trash, Compact: true),
        new(UiButtonKind.Flat, UiButtonContentLayout.Icon, _iconStandardTitle, string.Empty, UiIconId.More, Compact: true),
        new(UiButtonKind.Secondary, UiButtonContentLayout.Icon, _iconStandardTitle, string.Empty, UiIconId.Model, Compact: true, BadgeText: "3"),
        new(UiButtonKind.Primary, UiButtonContentLayout.Icon, _iconHoldTitle, string.Empty, UiIconId.Play, Hold: true),
        new(UiButtonKind.Tertiary, UiButtonContentLayout.Icon, _iconHoldTitle, string.Empty, UiIconId.Trash, Hold: true),
        new(UiButtonKind.Secondary, UiButtonContentLayout.Icon, _iconBadgeTitle, string.Empty, UiIconId.Model, BadgeText: "3"),
        new(UiButtonKind.Primary, UiButtonContentLayout.Icon, _iconBadgeTitle, string.Empty, UiIconId.Lock, BadgeText: "1"),
        new(UiButtonKind.Tertiary, UiButtonContentLayout.Icon, _iconBadgeTitle, string.Empty, UiIconId.Trash, BadgeText: "2"),
        new(UiButtonKind.Primary, UiButtonContentLayout.Stack, _stackTitle, string.Empty, UiIconId.Play),
        new(UiButtonKind.Primary, UiButtonContentLayout.Stack, _stackTitle, string.Empty, UiIconId.Play, Selected: true),
        new(UiButtonKind.Primary, UiButtonContentLayout.Stack, _stackTitle, string.Empty, UiIconId.Play, Hold: true),
        new(UiButtonKind.Secondary, UiButtonContentLayout.Stack, _stackTitle, "Move", UiIconId.Move),
        new(UiButtonKind.Secondary, UiButtonContentLayout.Stack, _stackTitle, "Move", UiIconId.Move, Selected: true),
        new(UiButtonKind.Secondary, UiButtonContentLayout.Stack, _stackTitle, "Beam", UiIconId.Beam, Enabled: false),
        new(UiButtonKind.Tertiary, UiButtonContentLayout.Stack, _stackTitle, "Delete", UiIconId.Trash, Hold: true),
        new(UiButtonKind.Flat, UiButtonContentLayout.Stack, _stackTitle, "More", UiIconId.More),
        new(UiButtonKind.Flat, UiButtonContentLayout.Stack, _stackTitle, "More", UiIconId.More, Selected: true),
        new(UiButtonKind.Primary, UiButtonContentLayout.Stack, _stackTitle, string.Empty, UiIconId.Play, BadgeText: "1"),
    ];

    public static IReadOnlyList<GalleryComponentSpec> CanonicalInventory { get; } =
    [
        new(UiComponentContracts.CanonicalComponent.Button, "Actions"),
        new(UiComponentContracts.CanonicalComponent.IconButton, "Actions"),
        new(UiComponentContracts.CanonicalComponent.HoldButton, "Actions"),
        new(UiComponentContracts.CanonicalComponent.TextField, "Text input"),
        new(UiComponentContracts.CanonicalComponent.NameField, "Text input"),
        new(UiComponentContracts.CanonicalComponent.Note, "Panel"),
        new(UiComponentContracts.CanonicalComponent.Slider, "Slider and range"),
        new(UiComponentContracts.CanonicalComponent.Range, "Slider and range"),
        new(UiComponentContracts.CanonicalComponent.ProgressBar, "Slider and range"),
        new(UiComponentContracts.CanonicalComponent.ProgressRing, "Progress"),
        new(UiComponentContracts.CanonicalComponent.Card, "Cards"),
        new(UiComponentContracts.CanonicalComponent.Toggle, "Choices and tray rows"),
        new(UiComponentContracts.CanonicalComponent.Checkbox, "Choices and tray rows"),
        new(UiComponentContracts.CanonicalComponent.Segmented, "Segmented"),
        new(UiComponentContracts.CanonicalComponent.Picker, "Choices and tray rows"),
        new(UiComponentContracts.CanonicalComponent.PartRow, "Part rows"),
        new(UiComponentContracts.CanonicalComponent.Panel, "Panel"),
        new(UiComponentContracts.CanonicalComponent.StageCard, "Stage card"),
        new(UiComponentContracts.CanonicalComponent.OverflowMenu, "Overflow menu"),
        new(UiComponentContracts.CanonicalComponent.IconTabs, "Parts tray tabs"),
        new(UiComponentContracts.CanonicalComponent.SelectionHandle, "Selection handles"),
        new(UiComponentContracts.CanonicalComponent.Number, "Number"),
    ];

    public static IReadOnlyList<GallerySection> RenderedSectionOrder { get; } =
    [
        GallerySection.Actions,
        GallerySection.TextInput,
        GallerySection.Choices,
        GallerySection.Segmented,
        GallerySection.PartsTrayTabs,
        GallerySection.SelectionHandles,
        GallerySection.Number,
        GallerySection.Slider,
        GallerySection.Progress,
        GallerySection.Cards,
        GallerySection.OverflowMenu,
        GallerySection.Picker,
        GallerySection.PartRows,
        GallerySection.Panel,
        GallerySection.StageCard,
    ];

    public override void _Ready()
    {
        Name = nameof(ComponentGalleryScreen);
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

        _scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever,
        };
        shell.AddChild(_scroll);

        var contentFrame = new MarginContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        contentFrame.AddThemeConstantOverride("margin_left", UiGlow.Extent);
        contentFrame.AddThemeConstantOverride("margin_top", UiGlow.Extent);
        contentFrame.AddThemeConstantOverride("margin_right", UiGlow.Extent);
        contentFrame.AddThemeConstantOverride("margin_bottom", UiGlow.Extent);
        _scroll.AddChild(contentFrame);

        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        contentFrame.AddChild(content);
        _scrollContent = contentFrame;

        foreach (var section in RenderedSectionOrder)
        {
            AddRenderedSection(content, section);
        }

        UiNativeScroll.AllowGesturesToBubble(contentFrame);
        _boundsOverlay = new UiBoundsDebugOverlay
        {
            RootPath = frame.GetPath(),
        };
        AddChild(_boundsOverlay);
        ShowDebugBounds = ShowDebugBounds || ProjectSettings.GetSetting("ui/component_gallery_debug_bounds", false).AsBool();
        CreateToolbarMenu();
    }

    private void AddRenderedSection(VBoxContainer content, GallerySection section)
    {
        switch (section)
        {
            case GallerySection.Actions:
                content.AddChild(CreateSectionDescription(
                    "Buttons",
                    "four semantic kinds, three layouts, compact geometry, hold progress, and badges"));
                content.AddChild(CreateActionsSection());
                break;
            case GallerySection.TextInput:
                content.AddChild(CreateTextInputSection());
                break;
            case GallerySection.Choices:
                content.AddChild(CreateChoicesSection());
                break;
            case GallerySection.Segmented:
                content.AddChild(CreateSegmentedSection());
                break;
            case GallerySection.PartsTrayTabs:
                content.AddChild(CreateTrayTabsSection());
                break;
            case GallerySection.SelectionHandles:
                content.AddChild(CreateSelectionHandlesSection());
                break;
            case GallerySection.Number:
                content.AddChild(CreateNumberSection());
                break;
            case GallerySection.Slider:
                content.AddChild(CreateSliderSection());
                break;
            case GallerySection.Progress:
                content.AddChild(CreateProgressSection());
                break;
            case GallerySection.Cards:
                content.AddChild(CreateCardSection());
                break;
            case GallerySection.OverflowMenu:
                content.AddChild(CreateMenuSection());
                break;
            case GallerySection.Picker:
                content.AddChild(CreatePickerSection());
                break;
            case GallerySection.PartRows:
                content.AddChild(CreatePartRowSection());
                break;
            case GallerySection.Panel:
                content.AddChild(CreatePanelSection());
                break;
            case GallerySection.StageCard:
                content.AddChild(CreateStageCardSection());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(section), section, null);
        }
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

        if (ShowCloseAction)
        {
            var close = Track(new UiSecondaryIconButton
            {
                IconId = UiIconId.Back,
                AccessibleLabel = "Back to Creations",
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            });
            close.Pressed += () => EmitSignal(SignalName.CloseRequested);
            header.AddChild(close);
        }

        var switcher = Track(new UiSegmentedSwitch
        {
            Options = new[] { "Neon", "Paper", "Effects lite" },
            SelectedIndex = 0,
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

        _toolbarMore = Track(new UiButton
        {
            Style = UiButtonStyle.Flat,
            ContentLayout = UiButtonContentLayout.Icon,
            IconId = UiIconId.More,
            TooltipText = "Gallery options",
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        });
        _toolbarMore.Activated += ToggleToolbarMenu;
        _toolbarMore.ItemRectChanged += () => Callable.From(PositionToolbarMenu).CallDeferred();
        header.AddChild(_toolbarMore);

        return header;
    }

    private void CreateToolbarMenu()
    {
        _toolbarDismiss = new Button
        {
            Flat = true,
            FocusMode = FocusModeEnum.None,
            MouseFilter = MouseFilterEnum.Stop,
            Visible = false,
        };
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" })
        {
            _toolbarDismiss.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        }

        AddChild(_toolbarDismiss);
        _toolbarDismiss.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _toolbarDismiss.Pressed += CloseToolbarMenu;
        _toolbarMenu = Track(new UiOverflowMenu
        {
            ShowSelectedCheck = true,
        });
        _toolbarMenu.ActionSelected += actionId =>
        {
            if (actionId == "debug-bounds")
            {
                ShowDebugBounds = !ShowDebugBounds;
            }

            CloseToolbarMenu();
        };
        _toolbarMenu.Resized += PositionToolbarMenu;
        AddChild(_toolbarMenu);
        RefreshToolbarMenu();
        VisibilityChanged += () =>
        {
            if (!IsVisibleInTree())
            {
                CloseToolbarMenu();
            }
        };
    }

    private void RefreshToolbarMenu()
    {
        _toolbarMenu?.SetActions(new UiOverflowMenu.MenuAction(
            "debug-bounds",
            "Debug bounds",
            State: ShowDebugBounds
                ? UiComponentContracts.SemanticState.Selected
                : UiComponentContracts.SemanticState.Neutral));
    }

    private void ToggleToolbarMenu()
    {
        if (_toolbarMenu is null || _toolbarDismiss is null)
        {
            return;
        }

        if (_toolbarMenu.Visible)
        {
            CloseToolbarMenu();
            return;
        }

        _toolbarDismiss.Show();
        _toolbarMenu.Show();
        PositionToolbarMenu();
        Callable.From(PositionToolbarMenu).CallDeferred();
    }

    private void PositionToolbarMenu()
    {
        if (_toolbarMenu is null || _toolbarMore is null || !_toolbarMenu.Visible)
        {
            return;
        }

        var transform = GetGlobalTransform().AffineInverse() * _toolbarMore.GetGlobalTransform();
        var bottomRight = transform * _toolbarMore.Size;
        _toolbarMenu.Position = new Vector2(
            Mathf.Max(0, bottomRight.X - _toolbarMenu.Size.X),
            bottomRight.Y + _tokens.Space1);
    }

    private void CloseToolbarMenu()
    {
        _toolbarMenu?.Hide();
        _toolbarDismiss?.Hide();
    }

    public override void _UnhandledKeyInput(InputEvent inputEvent)
    {
        if (_toolbarMenu?.Visible == true && inputEvent.IsActionPressed("ui_cancel"))
        {
            CloseToolbarMenu();
            GetViewport().SetInputAsHandled();
        }
    }

    private Control CreateActionsSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", UiSpacing.StackGap(_tokens));
        AddButtonRow(content, _rowInteractiveTitle, SpecimensFor(_rowInteractiveTitle));

        content.AddChild(CreateSectionDescription(
            "Buttons with an icon",
            "standard and compact frames, plus hold, disabled, and badge states"));
        AddButtonRow(content, _iconStandardTitle, SpecimensFor(_iconStandardTitle));
        AddButtonRow(content, _iconHoldTitle, SpecimensFor(_iconHoldTitle));
        AddButtonRow(content, _iconBadgeTitle, SpecimensFor(_iconBadgeTitle));

        content.AddChild(CreateSectionDescription(
            "Stacked buttons",
            "touch-sized with the icon over a label, or no label for play"));
        AddStackedButtonRow(content, _stackTitle, SpecimensFor(_stackTitle));

        return content;
    }

    /// <summary>
    /// Renders the live buttons for a single gallery row directly from
    /// <see cref="ButtonSpecimenInventory"/>, so the inventory is the only
    /// declarative source of what actually appears in the gallery.
    /// </summary>
    private List<UiButton> SpecimensFor(string rowGroup) =>
        ButtonSpecimenInventory
            .Where(spec => spec.RowGroup == rowGroup)
            .Select(CreateButtonFromSpec)
            .ToList();

    private UiButton CreateButtonFromSpec(ButtonGallerySpec spec) =>
        CreateButton(
            spec.Kind,
            spec.Label,
            spec.Icon,
            spec.Layout,
            selected: spec.Selected,
            enabled: spec.Enabled,
            compact: spec.Compact,
            hold: spec.Hold,
            badge: spec.BadgeText,
            toggleOnActivate: spec.ToggleOnActivate);

    private void AddButtonRow(VBoxContainer content, string title, IReadOnlyList<UiButton> buttons)
    {
        content.AddChild(CreateLabel(title, _tokens.NoteText, tokens => tokens.Muted));
        var flow = CreateFlow();
        foreach (var button in buttons)
        {
            flow.AddChild(button);
        }

        content.AddChild(flow);
    }

    private void AddStackedButtonRow(VBoxContainer content, string title, IReadOnlyList<UiButton> buttons)
    {
        content.AddChild(CreateSectionDescription(title, string.Empty));
        var flow = CreateFlow();
        foreach (var button in buttons)
        {
            var state = button.Enabled
                ? button.HoldDurationSeconds > 0
                    ? "hold"
                    : button.On ? "selected" : "normal"
                : "disabled";
            flow.AddChild(CreateStackedButtonSpecimen(button, state));
        }

        content.AddChild(flow);
    }

    private UiButton CreateButton(
        UiButtonKind kind,
        string label,
        UiIconId? icon = null,
        UiButtonContentLayout layout = UiButtonContentLayout.Row,
        bool selected = false,
        bool enabled = true,
        bool compact = false,
        bool hold = false,
        string? badge = null,
        bool toggleOnActivate = false)
    {
        var button = Track(new UiButton
        {
            Kind = kind,
            LabelText = label,
            IconId = icon,
            ContentLayout = layout,
            On = selected,
            Enabled = enabled,
            Compact = compact,
            HoldDurationSeconds = hold ? UiComponentContracts.HoldCompletionSeconds : 0,
            Progress = hold ? 0.45f : -1,
            BadgeText = badge ?? string.Empty,
        });
        if (toggleOnActivate)
        {
            button.Activated += () => button.On = !button.On;
        }

        return button;
    }

    private Control CreateStackedButtonSpecimen(UiButton button, string state)
    {
        var stack = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            CustomMinimumSize = new Vector2(_tokens.ColumnMediumWidth, 0),
        };
        stack.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        stack.AddChild(button);
        var label = CreateLabel(state, _tokens.NoteText, tokens => tokens.Muted, TextServer.AutowrapMode.Off);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(label);
        return stack;
    }

    private Control CreateSegmentedSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        content.AddChild(CreateSectionDescription(
            "Segmented", "tap to choose one option; text and icons stay in place"));
        var examples = CreateFlow();
        examples.AddChild(Track(new UiSegmentedSwitch
        {
            Options = new[] { "Train", "Simulate" },
            IconIds = new[] { UiIconId.Play, UiIconId.Eye },
            FullWidth = false,
        }));
        examples.AddChild(Track(new UiSegmentedSwitch
        {
            Options = new[] { "1", "2", "3" },
            SelectedIndex = 1,
            FullWidth = false,
        }));
        examples.AddChild(Track(new UiSegmentedSwitch
        {
            Options = new[] { "Distance", "Speed", "Elevation" },
            FullWidth = false,
        }));
        content.AddChild(examples);

        return content;
    }

    private Control CreateSliderSection()
    {
        var section = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        section.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        section.AddChild(CreateSliderColumns(
            CreateSliderSectionDescription(
                "Slider",
                "the only slider, configured: one thumb or two, two or more even steps, a named marker"),
            CreateSliderSectionDescription(
                "Slider, disabled",
                "the same four with enabled=False: dimmed; the filled part stays solid but grey")));
        section.AddChild(CreateSliderPair(
            "Plain",
            new UiSlider
            {
                LabelText = "Shadows",
                ReadoutText = "8",
                Value = UiSliderValue.Thumb(0.22),
            },
            "Plain",
            new UiSlider
            {
                LabelText = "Shadows",
                ReadoutText = "8",
                Value = UiSliderValue.Thumb(0.22),
                Enabled = false,
            }));
        section.AddChild(CreateSliderPair(
            "Two steps (the ends) and a marker with a name",
            new UiSlider
            {
                LabelText = "Neurons",
                ReadoutText = "24",
                Value = UiSliderValue.Thumb(0.24),
                StepLabels = ["1", "100"],
                MarkerPosition = 0.03,
                MarkerText = "default 4",
            },
            "Two steps and marker",
            new UiSlider
            {
                LabelText = "Neurons",
                ReadoutText = "24",
                Value = UiSliderValue.Thumb(0.24),
                StepLabels = ["1", "100"],
                MarkerPosition = 0.03,
                MarkerText = "default 4",
                Enabled = false,
            }));
        section.AddChild(CreateSliderPair(
            "Four steps, always evenly placed; a marker on a step",
            new UiSlider
            {
                LabelText = "UI size",
                ReadoutText = "150%",
                Value = UiSliderValue.Thumb(0.5),
                StepLabels = ["50%", "100%", "200%", "400%"],
                MarkerPosition = 1d / 3d,
                MarkerText = "default 100%",
            },
            "Four steps and marker",
            new UiSlider
            {
                LabelText = "UI size",
                ReadoutText = "150%",
                Value = UiSliderValue.Thumb(0.5),
                StepLabels = ["50%", "100%", "200%", "400%"],
                MarkerPosition = 1d / 3d,
                MarkerText = "default 100%",
                Enabled = false,
            }));
        section.AddChild(CreateSliderPair(
            "Two thumbs: a range, and a marker",
            new UiSlider
            {
                LabelText = "Angle limits",
                ReadoutText = "-20° to 110°",
                Value = UiSliderValue.Thumbs(0.18, 0.72),
                MarkerPosition = 0.44,
                MarkerText = "now 35°",
            },
            "Two thumbs and a marker",
            new UiSlider
            {
                LabelText = "Angle limits",
                ReadoutText = "-20° to 110°",
                Value = UiSliderValue.Thumbs(0.18, 0.72),
                MarkerPosition = 0.44,
                MarkerText = "now 35°",
                Enabled = false,
            }));
        section.AddChild(CreateSliderPair(
            "Linear progress with label/value: the same slider track with no thumb",
            new UiSlider
            {
                LabelText = "Loading",
                ReadoutText = "62%",
                Value = UiSliderValue.Progress(0.62),
            },
            "Linear progress, disabled",
            new UiSlider
            {
                LabelText = "Power",
                ReadoutText = "18%",
                Value = UiSliderValue.Progress(0.18),
                Enabled = false,
            }));
        section.AddChild(CreateSliderPair(
            "Bare progress bar: no slider value row",
            new UiSlider
            {
                LabelText = string.Empty,
                ReadoutText = string.Empty,
                Value = UiSliderValue.Progress(0.78),
            },
            "Bare progress, disabled",
            new UiSlider
            {
                LabelText = string.Empty,
                ReadoutText = string.Empty,
                Value = UiSliderValue.Progress(0.35),
                Enabled = false,
            }));
        return section;
    }

    private Control CreateProgressSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        content.AddChild(CreateSectionDescription(
            "Progress",
            "rings are their own component; linear progress is shown with sliders above"));

        var rings = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
        };
        rings.AddThemeConstantOverride("separation", (int)_tokens.Space3);
        rings.AddChild(Track(new UiProgressRing { Progress = 0.72f }));
        rings.AddChild(Track(new UiProgressRing { Progress = 1 }));
        content.AddChild(rings);
        return content;
    }

    private Control CreateNumberSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        content.AddChild(CreateSectionDescription(
            "Number",
            "the ringed step number alone, used before stage headers and chain chips"));

        var numbers = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
        };
        numbers.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        foreach (var text in new[] { "1", "2", "3", "4" })
        {
            numbers.AddChild(Track(new UiNumber { Text = text }));
        }

        content.AddChild(numbers);
        return content;
    }

    private Control CreateCardSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        content.AddChild(CreateSectionDescription(
            "Cards",
            "one frame surface with variants; menu, dialog, pick, and stage card are owned elsewhere"));

        var variants = new GridContainer
        {
            Columns = 3,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        variants.AddThemeConstantOverride("h_separation", UiSpacing.ControlGap(_tokens));
        variants.AddThemeConstantOverride("v_separation", UiSpacing.ControlGap(_tokens));
        variants.AddChild(CreateCardSpecimen("Frame", "Default card", UiCard.CardVariant.Frame));
        variants.AddChild(CreateCardSpecimen("Selected", "Accent border and glow", UiCard.CardVariant.Selected));
        variants.AddChild(CreateCardSpecimen("Locked", "Dashed locked frame", UiCard.CardVariant.Locked));
        variants.AddChild(CreateCardSpecimen("Warning", "Danger border with words", UiCard.CardVariant.Warning));
        variants.AddChild(CreateCardSpecimen("Hint", "Halo border for guidance", UiCard.CardVariant.Hint));
        variants.AddChild(CreateCardSpecimen("Frame + glow", "Frame with glow enabled", UiCard.CardVariant.Frame, glow: true));
        variants.AddChild(CreateCardSpecimen("Frame + glow disabled", "Disabled display treatment", UiCard.CardVariant.Frame, glow: true, disabled: true));
        variants.AddChild(CreateCardSpecimen("Raised", "Raised surface variant", UiCard.CardVariant.Raised));
        content.AddChild(variants);

        var sizes = new HFlowContainer();
        sizes.AddThemeConstantOverride("h_separation", UiSpacing.ControlGap(_tokens));
        sizes.AddThemeConstantOverride("v_separation", UiSpacing.ControlGap(_tokens));
        sizes.AddChild(CreateCardSpecimen("Default", "space-3 padding", UiCard.CardVariant.Frame, UiCard.CardSize.Default));
        sizes.AddChild(CreateCardSpecimen("Snug", "smaller padding", UiCard.CardVariant.Frame, UiCard.CardSize.Snug));
        sizes.AddChild(CreateCardSpecimen("Tight", "compact padding", UiCard.CardVariant.Frame, UiCard.CardSize.Tight));
        sizes.AddChild(CreateCardSpecimen("Roomy", "larger padding", UiCard.CardVariant.Frame, UiCard.CardSize.Roomy));
        sizes.AddChild(CreateCardSpecimen("Flush", "no padding", UiCard.CardVariant.Frame, UiCard.CardSize.Flush));
        content.AddChild(sizes);
        return content;
    }

    private Control CreateCardSpecimen(
        string title,
        string description,
        UiCard.CardVariant variant,
        UiCard.CardSize size = UiCard.CardSize.Default,
        bool glow = false,
        bool disabled = false)
    {
        var card = Track(new UiCard
        {
            Kind = variant,
            SizeVariant = size,
            Glow = glow,
            CustomMinimumSize = new Vector2(148, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });
        if (disabled)
        {
            card.Modulate = UiTokens.MultiplyAlpha(Colors.White, 0.5f);
        }

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        stack.AddChild(CreateLabel(title, _tokens.SubheadingText, tokens => tokens.Ink, TextServer.AutowrapMode.Off));
        stack.AddChild(CreateLabel(description, _tokens.NoteText, tokens => tokens.Muted));
        card.AddChild(stack);
        return card;
    }

    private Control CreateSliderSectionDescription(string title, string description)
    {
        var heading = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        heading.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        var titleLabel = CreateLabel(
            title,
            _tokens.OverlineText,
            tokens => tokens.Ink,
            TextServer.AutowrapMode.Off);
        titleLabel.VerticalAlignment = VerticalAlignment.Top;
        heading.AddChild(titleLabel);
        var detail = CreateLabel(description, _tokens.NoteText, tokens => tokens.Muted);
        detail.CustomMinimumSize = Vector2.Zero;
        detail.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        detail.VerticalAlignment = VerticalAlignment.Top;
        heading.AddChild(detail);
        return heading;
    }

    private Control CreateSliderPair(
        string enabledCaption,
        UiSlider enabledSlider,
        string disabledCaption,
        UiSlider disabledSlider)
    {
        var pair = new VBoxContainer();
        pair.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        pair.AddChild(CreateSliderColumns(
            CreateLabel(enabledCaption, _tokens.NoteText, tokens => tokens.Muted),
            CreateLabel(disabledCaption, _tokens.NoteText, tokens => tokens.Muted)));
        enabledSlider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        disabledSlider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        pair.AddChild(CreateSliderColumns(Track(enabledSlider), Track(disabledSlider)));
        return pair;
    }

    private GridContainer CreateSliderColumns(Control enabled, Control disabled)
    {
        var columns = new GridContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Columns = 2,
        };
        columns.AddThemeConstantOverride("h_separation", (int)_tokens.Space5);
        enabled.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        disabled.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        columns.AddChild(enabled);
        columns.AddChild(disabled);
        return columns;
    }

    private Control CreateTrayTabsSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        content.AddChild(CreateSectionDescription("Parts tray tabs", "four tabs, one open at a time"));
        var tabs = Track(new UiIconTabs
        {
            ActiveIndex = 1,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
        });
        tabs.SetTabs(
            new UiIconTabs.TabItem("between", UiPartIconId.Spring, "Between two nodes"),
            new UiIconTabs.TabItem("joint", UiPartIconId.Servo, "On a joint"),
            new UiIconTabs.TabItem("sensors", UiPartIconId.LineOfSight, "Sensors"),
            new UiIconTabs.TabItem("blocks", UiPartIconId.Battery, "Blocks"));
        content.AddChild(tabs);
        return content;
    }

    private Control CreateSelectionHandlesSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        content.AddChild(CreateSectionDescription(
            "Selection handles",
            "move, rotate, scale, on the canvas around a selection"));
        var handles = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
        };
        handles.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        handles.AddChild(Track(new UiSelectionHandle { Type = UiSelectionHandle.HandleType.Drag }));
        handles.AddChild(Track(new UiSelectionHandle { Type = UiSelectionHandle.HandleType.Rotate }));
        handles.AddChild(Track(new UiSelectionHandle { Type = UiSelectionHandle.HandleType.Scale }));
        content.AddChild(handles);
        return content;
    }

    private Control CreateChoicesSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        content.AddChild(CreateSectionDescription("Toggle and checkbox", "tap a row to change its state; dimmed rows are disabled"));
        var columns = new GridContainer { Columns = 2 };
        columns.AddThemeConstantOverride("h_separation", (int)_tokens.Space5);
        columns.AddThemeConstantOverride("v_separation", (int)_tokens.Space2);
        foreach (var disabled in new[] { false, true })
        {
            foreach (var on in new[] { true, false })
            {
                columns.AddChild(Track(new UiToggleRow
                {
                    LabelText = "Sounds",
                    Subtext = disabled ? "Unavailable during this preview" : on ? "Taps, unlocks and results" : "",
                    On = on,
                    Disabled = disabled,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                }));
                columns.AddChild(Track(new UiCheckRow
                {
                    LabelText = "Run until power is out",
                    Subtext = disabled ? "No powered parts" : on ? "Ends when the battery does" : "",
                    Checked = on,
                    Disabled = disabled,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                }));
            }
        }

        content.AddChild(columns);
        return content;
    }

    private Control CreateTextInputSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        content.AddChild(CreateSectionDescription(
            "Text input",
            "standard and compact, rest, editing, and error"));

        var fields = new HFlowContainer();
        fields.AddThemeConstantOverride("h_separation", UiSpacing.ControlGap(_tokens));
        fields.AddThemeConstantOverride("v_separation", UiSpacing.ControlGap(_tokens));
        fields.AddChild(Track(new UiTextField
        {
            LabelText = "Creation name",
            TextValue = "Runner",
            State = UiTextField.TextInputState.Rest,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        }));
        fields.AddChild(Track(new UiTextField
        {
            LabelText = "Creation name",
            TextValue = "",
            ErrorText = "A creation needs a name",
            ValidateValue = static value => !string.IsNullOrWhiteSpace(value),
            State = UiTextField.TextInputState.Error,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        }));
        fields.AddChild(Track(new UiTextField
        {
            LabelText = "Part name",
            TextValue = "Left foot",
            InputSize = UiTextField.TextInputSize.Compact,
            State = UiTextField.TextInputState.Editing,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        }));
        content.AddChild(fields);
        return content;
    }

    private Control CreatePartRowSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        content.AddChild(CreateSectionDescription(
            "Part Row",
            "a build part in the tray: glyph, name, count, and four states"));

        var rows = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(_tokens.SidePanelWidth, 0),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
        };
        rows.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        rows.AddChild(Track(new UiPartRow
        {
            PartIconId = UiPartIconId.Servo,
            PartName = "Servo",
            CountText = "3 left",
            State = UiPartRow.PartRowState.Rest,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        }));
        rows.AddChild(Track(new UiPartRow
        {
            PartIconId = UiPartIconId.Servo,
            PartName = "Servo",
            CountText = "3 left",
            State = UiPartRow.PartRowState.Selected,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        }));
        rows.AddChild(Track(new UiPartRow
        {
            PartIconId = UiPartIconId.Stepper,
            PartName = "Stepper",
            CountText = "Locked",
            State = UiPartRow.PartRowState.Locked,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        }));
        rows.AddChild(Track(new UiPartRow
        {
            PartIconId = UiPartIconId.Wheel,
            PartName = "Wheel",
            CountText = "0 left",
            State = UiPartRow.PartRowState.NoneLeft,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        }));
        content.AddChild(rows);
        return content;
    }

    private Control CreatePanelSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        content.AddChild(CreateSectionDescription(
            "Panel",
            "a part settings panel: header, padless rows sharing one gap, then Delete"));

        var panel = Track(new UiInspectorPanel
        {
            Title = "Servo",
            GlyphIconId = UiIconId.Speed,
            DeleteText = "Delete",
            CustomMinimumSize = new Vector2(_tokens.SidePanelWidth, 0),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
        });
        panel.SetRows(
            new UiNameField
            {
                TextValue = "Front knee",
            },
            new UiSlider
            {
                LabelText = "Max strength",
                ReadoutText = "40 N·m",
                Value = UiSliderValue.Thumb(0.62),
            },
            new UiValueRow
            {
                LabelText = "Weighs",
                ValueText = "1.2 kg",
            },
            new UiPicker
            {
                LabelText = "Fixed part",
                SelectedId = "front-thigh",
                Options =
                [
                    new("front-thigh", "Front thigh", UiIconId.Beam),
                    new("front-shin", "Front shin", UiIconId.Beam),
                ],
            },
            new UiValueRow
            {
                LabelText = "Power",
                ValueText = "Draws up to 0.6",
                IconId = UiIconId.Bolt,
            },
            new UiNoteRow
            {
                Text = "Sits on an empty joint. Put a motor on top to drive it.",
            });
        content.AddChild(panel);
        return content;
    }

    private Control CreateStageCardSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        content.AddChild(CreateSectionDescription(
            "Stage card",
            "one signal-flow stage: number, stage name, optional note, then stage content"));

        var cards = CreateFlow();
        cards.AddChild(CreateStageCardSpecimen("1", "Senses", "Left foot", "9 readings · top first"));
        cards.AddChild(CreateStageCardSpecimen("2", "Thinks", "Brain", "24 neurons · 8 outputs", selected: true));
        cards.AddChild(CreateStageCardSpecimen("3", "Moves", "Servo", string.Empty, collapsed: true));
        content.AddChild(cards);
        return content;
    }

    private UiStageCard CreateStageCardSpecimen(
        string number,
        string title,
        string note,
        string body,
        bool selected = false,
        bool collapsed = false)
    {
        var card = Track(new UiStageCard
        {
            NumberText = number,
            Title = title,
            Note = note,
            Selected = selected,
            Collapsed = collapsed,
            CustomMinimumSize = new Vector2(_tokens.TileWidth, 0),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
        });
        if (!string.IsNullOrWhiteSpace(body))
        {
            card.SetBody(new Label
            {
                Text = body,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = MouseFilterEnum.Ignore,
            });
        }

        return card;
    }

    private Control CreateMenuSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        content.AddChild(CreateSectionDescription("Menu", "the overflow (three dots) list"));

        var menu = Track(new UiOverflowMenu
        {
            CloseOnSelect = false,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            WidthMode = UiOverflowMenu.MenuWidthMode.WrapContent,
            Visible = true,
        });
        menu.SetActions(
            new UiOverflowMenu.MenuAction("brain-setup", "Brain setup", UiIconId.Model, UiComponentContracts.SemanticState.Neutral),
            new UiOverflowMenu.MenuAction("settings", "Settings", UiIconId.Gear, UiComponentContracts.SemanticState.Selected),
            new UiOverflowMenu.MenuAction("delete", "Delete creation", UiIconId.Trash, UiComponentContracts.SemanticState.Danger));
        menu.CallDeferred(CanvasItem.MethodName.Show);
        content.AddChild(menu);

        return content;
    }

    private Control CreatePickerSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        content.AddChild(CreateSectionDescription(
            "Picker",
            "collapsed, expanded, locked, and disabled"));

        var examples = CreateFlow();
        examples.AddChild(CreatePickerExample(
            "Interactive",
            new UiPicker
            {
                LabelText = "Fixed part",
                SelectedId = "left-thigh",
                Options =
                [
                    new("left-thigh", "Left thigh", UiIconId.Beam),
                    new("left-shin", "Left shin", UiIconId.Beam, Note: "swaps"),
                    new("tail", "Tail"),
                ],
            }));
        examples.AddChild(CreatePickerExample(
            "Locked",
            new UiPicker
            {
                LabelText = "Target part",
                State = UiPicker.PickerState.Locked,
                SelectedId = "wheel-1",
                Options = [new("wheel-1", "Wheel 1", UiIconId.Beam)],
            }));
        examples.AddChild(CreatePickerExample(
            "Disabled",
            new UiPicker
            {
                LabelText = "Target part",
                SelectedId = "wheel-1",
                Options = [new("wheel-1", "Wheel 1", UiIconId.Beam)],
                Disabled = true,
                BelowText = "Can't reassign while training",
            }));
        content.AddChild(examples);

        return content;
    }

    private Control CreatePickerExample(string caption, UiPicker picker)
    {
        var stack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
        };
        stack.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        stack.AddChild(CreateLabel(caption, _tokens.NoteText, tokens => tokens.Muted, TextServer.AutowrapMode.Off));
        stack.AddChild(Track(picker));
        return stack;
    }

    private HFlowContainer CreateFlow()
    {
        var flow = new HFlowContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        flow.AddThemeConstantOverride("h_separation", UiSpacing.ControlGap(_tokens));
        flow.AddThemeConstantOverride("v_separation", UiSpacing.ControlGap(_tokens));
        return flow;
    }

    private Control CreateSectionDescription(string title, string description)
    {
        var heading = new HBoxContainer();
        heading.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        heading.AddChild(CreateLabel(
            title,
            _tokens.OverlineText,
            tokens => tokens.Ink,
            TextServer.AutowrapMode.Off));
        heading.AddChild(CreateLabel(
            description,
            _tokens.NoteText,
            tokens => tokens.Muted,
            TextServer.AutowrapMode.Off));
        return heading;
    }

    private Label CreateLabel(
        string text,
        UiTokens.TextStyle textStyle,
        Func<UiTokens, Color> colorForTokens,
        TextServer.AutowrapMode autowrap = TextServer.AutowrapMode.WordSmart)
    {
        var label = new Label
        {
            Text = text,
            AutowrapMode = autowrap,
            MouseFilter = MouseFilterEnum.Ignore,
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
        UiTokenApplier.Apply(control, tokens);
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
}
