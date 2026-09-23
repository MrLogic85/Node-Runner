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
    private const string _sharedCardPanelSection =
        "Panel and card · shared composition · c_card / c_panel";

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

    private readonly List<Action<UiTokens>> _tokenAppliers = new();
    private readonly List<Action<UiTokens>> _labelAppliers = new();
    private UiTokens _tokens = UiTokens.Neon;
    private ColorRect? _background;
    private ScrollContainer? _scroll;
    private int _scrollTouchIndex = -1;
    private Vector2 _pendingTouchDrag;
    private bool _isTouchScrolling;

    public readonly record struct GalleryComponentSpec(
        UiComponentContracts.CanonicalComponent Component,
        string EntryName,
        string Section,
        bool SharedComposition = false);

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
        new(UiComponentContracts.CanonicalComponent.CBtn, "c_btn", "Actions · c_btn / c_ib / c_hold"),
        new(UiComponentContracts.CanonicalComponent.CIb, "c_ib", "Actions · c_btn / c_ib / c_hold"),
        new(UiComponentContracts.CanonicalComponent.CHold, "c_hold", "Actions · c_btn / c_ib / c_hold"),
        new(UiComponentContracts.CanonicalComponent.CSlider, "c_slider", "Slider · c_slider / c_range"),
        new(UiComponentContracts.CanonicalComponent.CRange, "c_range", "Slider · c_slider / c_range"),
        new(UiComponentContracts.CanonicalComponent.CToggle, "c_toggle", "Choices and tray rows · c_toggle / c_check / c_pick / c_row / c_tabs"),
        new(UiComponentContracts.CanonicalComponent.CCheck, "c_check", "Choices and tray rows · c_toggle / c_check / c_pick / c_row / c_tabs"),
        new(UiComponentContracts.CanonicalComponent.CSeg, "c_seg", "Segmented"),
        new(UiComponentContracts.CanonicalComponent.CPick, "c_pick", "Choices and tray rows · c_toggle / c_check / c_pick / c_row / c_tabs"),
        new(UiComponentContracts.CanonicalComponent.CMenu, "c_menu", "Overflow menu · c_menu"),
        new(UiComponentContracts.CanonicalComponent.CChip, "c_chip", "Chips · c_chip"),
        new(UiComponentContracts.CanonicalComponent.CProg, "c_prog", "Progress · c_prog / c_ring"),
        new(UiComponentContracts.CanonicalComponent.CTextfield, "c_textfield", "Text and values · c_textfield / c_name / c_value / c_readonly / c_power / c_meter / c_panel_head / c_info_row"),
        new(UiComponentContracts.CanonicalComponent.CName, "c_name", "Text and values · c_textfield / c_name / c_value / c_readonly / c_power / c_meter / c_panel_head / c_info_row"),
        new(UiComponentContracts.CanonicalComponent.CValue, "c_value", "Text and values · c_textfield / c_name / c_value / c_readonly / c_power / c_meter / c_panel_head / c_info_row"),
        new(UiComponentContracts.CanonicalComponent.CReadonly, "c_readonly", "Text and values · c_textfield / c_name / c_value / c_readonly / c_power / c_meter / c_panel_head / c_info_row"),
        new(UiComponentContracts.CanonicalComponent.CPower, "c_power", "Text and values · c_textfield / c_name / c_value / c_readonly / c_power / c_meter / c_panel_head / c_info_row"),
        new(UiComponentContracts.CanonicalComponent.CMeter, "c_meter", "Text and values · c_textfield / c_name / c_value / c_readonly / c_power / c_meter / c_panel_head / c_info_row"),
        new(UiComponentContracts.CanonicalComponent.CRow, "c_row", "Choices and tray rows · c_toggle / c_check / c_pick / c_row / c_tabs"),
        new(UiComponentContracts.CanonicalComponent.CTabs, "c_tabs", "Parts tray tabs"),
        new(UiComponentContracts.CanonicalComponent.CPanelHead, "c_panel_head", "Text and values · c_textfield / c_name / c_value / c_readonly / c_power / c_meter / c_panel_head / c_info_row"),
        new(UiComponentContracts.CanonicalComponent.CInfoRow, "c_info_row", "Text and values · c_textfield / c_name / c_value / c_readonly / c_power / c_meter / c_panel_head / c_info_row"),
        new(UiComponentContracts.CanonicalComponent.CCard, "c_card", _sharedCardPanelSection, SharedComposition: true),
        new(UiComponentContracts.CanonicalComponent.CPanel, "c_panel", _sharedCardPanelSection, SharedComposition: true),
        new(UiComponentContracts.CanonicalComponent.CRing, "c_ring", "Progress · c_prog / c_ring"),
    ];

    public override void _Ready()
    {
        Name = nameof(ComponentGalleryScreen);
        UiLayout.ApplyScreen(this);
        BuildLayout();
        ApplyTokens(_tokens);
        Callable.From(ResetScrollPosition).CallDeferred();
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (_scroll is null || !IsVisibleInTree())
        {
            return;
        }

        if (inputEvent is InputEventScreenTouch touch)
        {
            HandleScrollTouch(touch);
            return;
        }

        if (inputEvent is not InputEventScreenDrag drag || drag.Index != _scrollTouchIndex)
        {
            return;
        }

        _pendingTouchDrag += drag.Relative;
        if (!_isTouchScrolling)
        {
            if (_pendingTouchDrag.Length() < UiGalleryScroll.TouchDeadzone)
            {
                return;
            }

            if (Mathf.Abs(_pendingTouchDrag.Y) <= Mathf.Abs(_pendingTouchDrag.X))
            {
                _scrollTouchIndex = -1;
                return;
            }

            _isTouchScrolling = true;
            // Until #244 restores native scrolling, deliver its native press cancellation.
            _scroll.PropagateNotification((int)NotificationScrollBegin);
        }

        _scroll.ScrollVertical = UiGalleryScroll.ApplyVerticalDrag(
            _scroll.ScrollVertical,
            _pendingTouchDrag.Y);
        _pendingTouchDrag = Vector2.Zero;
        GetViewport().SetInputAsHandled();
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
        };
        shell.AddChild(_scroll);

        var contentFrame = new MarginContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        contentFrame.AddThemeConstantOverride("margin_left", UiGlow.ButtonExtent);
        contentFrame.AddThemeConstantOverride("margin_top", UiGlow.ButtonExtent);
        contentFrame.AddThemeConstantOverride("margin_right", UiGlow.ButtonExtent);
        contentFrame.AddThemeConstantOverride("margin_bottom", UiGlow.ButtonExtent);
        _scroll.AddChild(contentFrame);

        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        contentFrame.AddChild(content);

        content.AddChild(CreateSectionDescription(
            "Buttons",
            "four semantic kinds, three layouts, compact geometry, hold progress, and badges"));
        content.AddChild(CreateActionsSection());
        content.AddChild(CreateChoicesSection());
        content.AddChild(CreateSegmentedSection());
        content.AddChild(CreateTrayTabsSection());
        content.AddChild(CreateSliderSection());
        content.AddChild(CreateMenuSection());
        content.AddChild(CreatePickerSection());
        content.AddChild(CreatePanelsSection());
        content.AddChild(CreateInputsSection());
        content.AddChild(CreateChoiceAndRowsSection());
        content.AddChild(CreateTextAndValueSection());
        content.AddChild(CreateProgressAndStatusSection());
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

        return header;
    }

    private void HandleScrollTouch(InputEventScreenTouch touch)
    {
        if (touch.Pressed)
        {
            if (_scroll!.GetGlobalRect().HasPoint(touch.Position))
            {
                _scrollTouchIndex = touch.Index;
                _pendingTouchDrag = Vector2.Zero;
                _isTouchScrolling = false;
            }

            return;
        }

        if (touch.Index != _scrollTouchIndex)
        {
            return;
        }

        if (_isTouchScrolling)
        {
            _scroll!.PropagateNotification((int)NotificationScrollEnd);
            GetViewport().SetInputAsHandled();
        }

        _scrollTouchIndex = -1;
        _pendingTouchDrag = Vector2.Zero;
        _isTouchScrolling = false;
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

    private Control CreatePanelsSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", UiSpacing.StackGap(_tokens));
        var panels = CreateFlow();
        content.AddChild(panels);

        panels.AddChild(CreatePanelExample("Rest", UiSurfaceContracts.FrameVariant.Frame));
        panels.AddChild(CreatePanelExample("Selected", UiSurfaceContracts.FrameVariant.Sel));
        panels.AddChild(CreatePanelExample("Locked", UiSurfaceContracts.FrameVariant.Lock));
        panels.AddChild(CreatePanelExample("Warning", UiSurfaceContracts.FrameVariant.Warn));
        panels.AddChild(CreatePanelExample("Hint", UiSurfaceContracts.FrameVariant.Hint));
        panels.AddChild(CreatePanelExample("Raised", UiSurfaceContracts.FrameVariant.Raised));
        panels.AddChild(Track(new UiPanel
        {
            Variant = UiSurfaceContracts.FrameVariant.Frame,
            Size = UiSurfaceContracts.FrameSize.Tight,
        }));
        ((UiPanel)panels.GetChild(panels.GetChildCount() - 1)).AddChild(CreateLabel("Tight", _tokens.BodyText, tokens => tokens.Ink, TextServer.AutowrapMode.Off));
        panels.AddChild(Track(new UiPanel
        {
            Variant = UiSurfaceContracts.FrameVariant.Frame,
            Size = UiSurfaceContracts.FrameSize.Flush,
        }));
        ((UiPanel)panels.GetChild(panels.GetChildCount() - 1)).AddChild(CreateLabel("Flush", _tokens.BodyText, tokens => tokens.Ink, TextServer.AutowrapMode.Off));

        return WrapSection(_sharedCardPanelSection, content);
    }

    private Control CreateInputsSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        var chips = CreateFlow();
        chips.AddChild(Track(new UiChip { Text = "Brain 1 × 4", Kind = UiChip.ChipKind.Accent }));
        chips.AddChild(Track(new UiChip { Text = "Spring locked", Kind = UiChip.ChipKind.Locked }));
        chips.AddChild(Track(new UiChip { Text = "Warn", Kind = UiChip.ChipKind.Warning }));
        chips.AddChild(Track(new UiChip { Text = "Danger", Kind = UiChip.ChipKind.Danger }));
        chips.AddChild(Track(new UiChip { Text = "Bad", Kind = UiChip.ChipKind.Bad }));
        chips.AddChild(Track(new UiChip { Text = "OK", Kind = UiChip.ChipKind.Ok }));
        content.AddChild(chips);

        return WrapSection("Chips · c_chip", content);
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
                Thumbs = [0.22],
            },
            "Plain",
            new UiSlider
            {
                LabelText = "Shadows",
                ReadoutText = "8",
                Thumbs = [0.22],
                Enabled = false,
            }));
        section.AddChild(CreateSliderPair(
            "Two steps (the ends) and a marker with a name",
            new UiSlider
            {
                LabelText = "Neurons",
                ReadoutText = "24",
                Thumbs = [0.24],
                StepLabels = ["1", "100"],
                MarkerPosition = 0.03,
                MarkerText = "default 4",
            },
            "Two steps and marker",
            new UiSlider
            {
                LabelText = "Neurons",
                ReadoutText = "24",
                Thumbs = [0.24],
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
                Thumbs = [0.5],
                StepLabels = ["50%", "100%", "200%", "400%"],
                MarkerPosition = 1d / 3d,
                MarkerText = "default 100%",
            },
            "Four steps and marker",
            new UiSlider
            {
                LabelText = "UI size",
                ReadoutText = "150%",
                Thumbs = [0.5],
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
                Thumbs = [0.18, 0.72],
                MarkerPosition = 0.44,
                MarkerText = "now 35°",
            },
            "Two thumbs and a marker",
            new UiSlider
            {
                LabelText = "Angle limits",
                ReadoutText = "-20° to 110°",
                Thumbs = [0.18, 0.72],
                MarkerPosition = 0.44,
                MarkerText = "now 35°",
                Enabled = false,
            }));
        return section;
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

    private Control CreateChoiceAndRowsSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", UiSpacing.StackGap(_tokens));

        var rows = new HFlowContainer();
        rows.AddThemeConstantOverride("h_separation", UiSpacing.ControlGap(_tokens));
        rows.AddThemeConstantOverride("v_separation", UiSpacing.ControlGap(_tokens));
        rows.AddChild(Track(new UiPartRow { PartIconId = UiPartIconId.Servo, PartName = "Servo", Count = "1", State = UiComponentContracts.SemanticState.Selected, SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        rows.AddChild(Track(new UiPartRow { PartIconId = UiPartIconId.Spring, PartName = "Spring", Count = "0", State = UiComponentContracts.SemanticState.Disabled, SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        rows.AddChild(Track(new UiPartRow { PartIconId = UiPartIconId.LineOfSight, PartName = "LOS sensor", Count = "2", State = UiComponentContracts.SemanticState.Locked, SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        content.AddChild(rows);

        return WrapSection("Tray rows · c_row", content);
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

    private Control CreateTextAndValueSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", UiSpacing.StackGap(_tokens));

        var fields = new HFlowContainer();
        fields.AddThemeConstantOverride("h_separation", UiSpacing.ControlGap(_tokens));
        fields.AddThemeConstantOverride("v_separation", UiSpacing.ControlGap(_tokens));
        fields.AddChild(Track(new UiTextField { TextValue = "Runner", State = UiComponentContracts.ValidationState.Rest, SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        fields.AddChild(Track(new UiTextField { TextValue = "Runner", State = UiComponentContracts.ValidationState.Editing, SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        fields.AddChild(Track(new UiTextField { TextValue = "", State = UiComponentContracts.ValidationState.Invalid, SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        fields.AddChild(Track(new UiNameField { TextValue = "Core", State = UiComponentContracts.ValidationState.Rest, SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        fields.AddChild(Track(new UiNameField { TextValue = "Left foot", State = UiComponentContracts.ValidationState.Editing, SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        content.AddChild(fields);

        var values = new HFlowContainer();
        values.AddThemeConstantOverride("h_separation", UiSpacing.ControlGap(_tokens));
        values.AddThemeConstantOverride("v_separation", UiSpacing.ControlGap(_tokens));
        values.AddChild(Track(new UiValueRow { LabelText = "Step size", ValueText = "12°", SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        values.AddChild(Track(new UiReadonlyValue { LabelText = "Length", ValueText = "56", Reason = "trained", SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        values.AddChild(Track(new UiPowerRow { TextValue = "Draws up to 0.6", Output = false }));
        values.AddChild(Track(new UiPowerRow { TextValue = "Makes 1.0 · stores rest", Output = true }));
        values.AddChild(Track(new UiMeterRow { LabelText = "Battery", ValueText = "20 / 20 units", Percent = 78, SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        content.AddChild(values);

        content.AddChild(Track(new UiPanelHeader { Title = "Part settings" }));
        content.AddChild(Track(new UiInfoRow { Title = "Rotate handle", Help = "Drag the stem to rotate selected parts." }));
        return WrapSection("Text and values · c_textfield / c_name / c_value / c_readonly / c_power / c_meter / c_panel_head / c_info_row", content);
    }

    private Control CreateProgressAndStatusSection()
    {
        var content = new HFlowContainer();
        content.AddThemeConstantOverride("h_separation", UiSpacing.ControlGap(_tokens));
        content.AddThemeConstantOverride("v_separation", UiSpacing.ControlGap(_tokens));
        content.AddChild(Track(new UiProgressBar { Percent = 37, SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        content.AddChild(Track(new UiProgressBar { Percent = 84, Bad = true, SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        content.AddChild(Track(new UiProgressRing { Percent = 62 }));
        content.AddChild(Track(new UiProgressRing { Percent = 100, Done = true }));
        return WrapSection("Progress · c_prog / c_ring", content);
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

    private Control CreatePanelExample(string title, UiSurfaceContracts.FrameVariant variant)
    {
        // These are compact one-word badges, not paragraph specimens: they must
        // size to their natural content width so HFlowContainer can wrap rows
        // instead of squeezing every panel into an equal, too-narrow share and
        // forcing the label to wrap character-by-character.
        var panel = Track(new UiPanel
        {
            Variant = variant,
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
            variant == UiSurfaceContracts.FrameVariant.Warn
                ? tokens => tokens.Danger
                : tokens => tokens.Ink,
            TextServer.AutowrapMode.Off));
        return panel;
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

    private Control WrapSection(string? title, Control content)
    {
        var panel = Track(new UiPanel
        {
            Variant = UiSurfaceContracts.FrameVariant.Frame,
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
        if (title is not null)
        {
            stack.AddChild(CreateLabel(title, _tokens.HeadingText, tokens => tokens.Accent));
        }

        stack.AddChild(content);
        return panel;
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
            case UiButton button:
                button.Tokens = tokens;
                break;
            case UiOverflowMenu menu:
                menu.Tokens = tokens;
                break;
            case UiSegmentedSwitch segmentedSwitch:
                segmentedSwitch.Tokens = tokens;
                break;
            case UiPanel panel:
                panel.Tokens = tokens;
                break;
            case UiChip chip:
                chip.Tokens = tokens;
                break;
            case UiSlider slider:
                slider.Tokens = tokens;
                break;
            case UiToggleRow toggleRow:
                toggleRow.Tokens = tokens;
                break;
            case UiCheckRow checkRow:
                checkRow.Tokens = tokens;
                break;
            case UiPicker picker:
                picker.Tokens = tokens;
                break;
            case UiProgressBar progressBar:
                progressBar.Tokens = tokens;
                break;
            case UiTextField textField:
                textField.Tokens = tokens;
                break;
            case UiValueRow valueRow:
                valueRow.Tokens = tokens;
                break;
            case UiReadonlyValue readonlyValue:
                readonlyValue.Tokens = tokens;
                break;
            case UiPowerRow powerRow:
                powerRow.Tokens = tokens;
                break;
            case UiMeterRow meterRow:
                meterRow.Tokens = tokens;
                break;
            case UiPartRow partRow:
                partRow.Tokens = tokens;
                break;
            case UiIconTabs iconTabs:
                iconTabs.Tokens = tokens;
                break;
            case UiPanelHeader panelHeader:
                panelHeader.Tokens = tokens;
                break;
            case UiInfoRow infoRow:
                infoRow.Tokens = tokens;
                break;
            case UiProgressRing progressRing:
                progressRing.Tokens = tokens;
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
