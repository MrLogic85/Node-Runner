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

    public static IReadOnlyList<GalleryComponentSpec> CanonicalInventory { get; } =
    [
        new(UiComponentContracts.CanonicalComponent.CBtn, "c_btn", "Actions · c_btn / c_ib / c_hold / c_step"),
        new(UiComponentContracts.CanonicalComponent.CIb, "c_ib", "Actions · c_btn / c_ib / c_hold / c_step"),
        new(UiComponentContracts.CanonicalComponent.CHold, "c_hold", "Actions · c_btn / c_ib / c_hold / c_step"),
        new(UiComponentContracts.CanonicalComponent.CStep, "c_step", "Actions · c_btn / c_ib / c_hold / c_step"),
        new(UiComponentContracts.CanonicalComponent.CSlider, "c_slider", "Numeric inputs · c_slider / c_range / c_chip"),
        new(UiComponentContracts.CanonicalComponent.CRange, "c_range", "Numeric inputs · c_slider / c_range / c_chip"),
        new(UiComponentContracts.CanonicalComponent.CToggle, "c_toggle", "Choices and tray rows · c_toggle / c_check / c_pick / c_row / c_tabs"),
        new(UiComponentContracts.CanonicalComponent.CCheck, "c_check", "Choices and tray rows · c_toggle / c_check / c_pick / c_row / c_tabs"),
        new(UiComponentContracts.CanonicalComponent.CSeg, "c_seg", "Tool buttons and mode switch · c_seg"),
        new(UiComponentContracts.CanonicalComponent.CPick, "c_pick", "Choices and tray rows · c_toggle / c_check / c_pick / c_row / c_tabs"),
        new(UiComponentContracts.CanonicalComponent.CMenu, "c_menu", "Overflow menu · c_menu"),
        new(UiComponentContracts.CanonicalComponent.CChip, "c_chip", "Numeric inputs · c_slider / c_range / c_chip"),
        new(UiComponentContracts.CanonicalComponent.CProg, "c_prog", "Progress · c_prog / c_ring"),
        new(UiComponentContracts.CanonicalComponent.CTextfield, "c_textfield", "Text and values · c_textfield / c_name / c_value / c_readonly / c_power / c_meter / c_panel_head / c_info_row"),
        new(UiComponentContracts.CanonicalComponent.CName, "c_name", "Text and values · c_textfield / c_name / c_value / c_readonly / c_power / c_meter / c_panel_head / c_info_row"),
        new(UiComponentContracts.CanonicalComponent.CValue, "c_value", "Text and values · c_textfield / c_name / c_value / c_readonly / c_power / c_meter / c_panel_head / c_info_row"),
        new(UiComponentContracts.CanonicalComponent.CReadonly, "c_readonly", "Text and values · c_textfield / c_name / c_value / c_readonly / c_power / c_meter / c_panel_head / c_info_row"),
        new(UiComponentContracts.CanonicalComponent.CPower, "c_power", "Text and values · c_textfield / c_name / c_value / c_readonly / c_power / c_meter / c_panel_head / c_info_row"),
        new(UiComponentContracts.CanonicalComponent.CMeter, "c_meter", "Text and values · c_textfield / c_name / c_value / c_readonly / c_power / c_meter / c_panel_head / c_info_row"),
        new(UiComponentContracts.CanonicalComponent.CRow, "c_row", "Choices and tray rows · c_toggle / c_check / c_pick / c_row / c_tabs"),
        new(UiComponentContracts.CanonicalComponent.CTabs, "c_tabs", "Choices and tray rows · c_toggle / c_check / c_pick / c_row / c_tabs"),
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

        var content = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", (int)_tokens.Space2);
        _scroll.AddChild(content);

        content.AddChild(CreateSectionDescription(
            "Buttons",
            "one primary per screen; destructive ones are outlined and named"));
        content.AddChild(CreateActionsSection());
        content.AddChild(CreateSegmentedSection());
        content.AddChild(CreatePanelsSection());
        content.AddChild(CreateInputsSection());
        content.AddChild(CreateChoiceAndRowsSection());
        content.AddChild(CreateTextAndValueSection());
        content.AddChild(CreateProgressAndStatusSection());
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
            Options = new[] { "Neon", "Paper" },
            SelectedIndex = 0,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        });
        switcher.SelectionChanged += index =>
        {
            ApplyTokens(index switch
            {
                1 => UiTokens.Paper,
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
        var actions = new HFlowContainer();
        actions.AddThemeConstantOverride("h_separation", UiSpacing.ControlGap(_tokens));
        actions.AddThemeConstantOverride("v_separation", UiSpacing.ControlGap(_tokens));
        content.AddChild(actions);
        actions.AddChild(Track(new UiPrimaryButton
        {
            LabelText = "Start training",
            IconId = UiIconId.Play,
        }));
        actions.AddChild(Track(new UiSecondaryButton
        {
            LabelText = "Cancel",
        }));
        actions.AddChild(Track(new UiTertiaryButton
        {
            LabelText = "Delete",
            IconId = UiIconId.Trash,
        }));
        actions.AddChild(Track(new UiSecondaryButton
        {
            LabelText = "Start training",
            Enabled = false,
        }));
        var unlock = Track(new UiTertiaryButton
        {
            LabelText = "Hold to unlock",
            HoldDurationSeconds = UiComponentContracts.HoldCompletionSeconds,
        });
        unlock.Activated += () => unlock.LabelText = "Unlocked";
        actions.AddChild(unlock);

        content.AddChild(CreateSectionDescription(
            "Buttons with an icon",
            "the same button in another layout: icon only"));
        var iconActions = CreateFlow();
        iconActions.AddChild(Track(new UiSecondaryIconButton
        {
            IconId = UiIconId.Back,
            AccessibleLabel = "Back",
        }));
        iconActions.AddChild(Track(new UiSecondaryIconButton
        {
            IconId = UiIconId.More,
            AccessibleLabel = "More",
        }));
        iconActions.AddChild(Track(new UiSecondaryIconButton
        {
            IconId = UiIconId.Pause,
            AccessibleLabel = "Pause",
        }));
        iconActions.AddChild(Track(new UiSecondaryIconButton
        {
            IconId = UiIconId.Speed,
            AccessibleLabel = "Fast forward",
        }));
        iconActions.AddChild(Track(new UiSecondaryIconButton
        {
            IconId = UiIconId.Lock,
            AccessibleLabel = "Lock",
            On = true,
            HoldDurationSeconds = UiComponentContracts.HoldCompletionSeconds,
        }));
        iconActions.AddChild(Track(new UiSecondaryIconButton
        {
            IconId = UiIconId.Unlock,
            AccessibleLabel = "Unlock",
        }));
        iconActions.AddChild(Track(new UiSecondaryIconButton
        {
            IconId = UiIconId.Chart,
            AccessibleLabel = "Chart",
        }));
        iconActions.AddChild(Track(new UiTertiaryIconButton
        {
            IconId = UiIconId.Trash,
            AccessibleLabel = "Delete",
            HoldDurationSeconds = UiComponentContracts.HoldCompletionSeconds,
        }));
        iconActions.AddChild(Track(new UiSecondaryIconButton
        {
            IconId = UiIconId.Close,
            AccessibleLabel = "Close",
            Enabled = false,
        }));
        iconActions.AddChild(Track(new UiPrimaryIconButton
        {
            IconId = UiIconId.Play,
            AccessibleLabel = "Play",
            ButtonSize = UiIconButtonSize.Large,
            IconSize = UiIconSize.ExtraLarge,
        }));
        content.AddChild(iconActions);

        content.AddChild(CreateSectionDescription(
            "Stacked buttons",
            "the same button with the icon over a small label"));
        var stackedActions = CreateFlow();
        stackedActions.AddChild(CreateStackedButtonSpecimen(Track(new UiSecondaryButton
        {
            LabelText = "Move",
            IconId = UiIconId.Move,
            ContentLayout = UiButtonContentLayout.Stack,
        }), "rest"));
        stackedActions.AddChild(CreateStackedButtonSpecimen(Track(new UiSecondaryButton
        {
            LabelText = "Move",
            IconId = UiIconId.Move,
            ContentLayout = UiButtonContentLayout.Stack,
            On = true,
        }), "on (chosen)"));
        stackedActions.AddChild(CreateStackedButtonSpecimen(Track(new UiSecondaryButton
        {
            LabelText = "Beam",
            IconId = UiIconId.Beam,
            ContentLayout = UiButtonContentLayout.Stack,
            Enabled = false,
        }), "off (disabled)"));
        content.AddChild(stackedActions);

        return content;
    }

    private Control CreateStackedButtonSpecimen(UiButton button, string state)
    {
        var stack = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
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
        var content = CreateFlow();
        content.AddChild(Track(new UiSegmentedSwitch
        {
            Options = new[] { "Train", "Simulate" },
            SelectedIndex = 1,
            Icons = [],
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        }));

        return WrapSection("Segmented · c_seg", content);
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
        content.AddChild(Track(new UiTokenSlider
        {
            LabelText = "Run length",
            MinValue = 1,
            MaxValue = 60,
            Value = 20,
            DefaultMarker = 30,
            ShowSteppers = true,
        }));
        content.AddChild(Track(new UiTokenSlider
        {
            LabelText = "Shadows",
            MinValue = 0,
            MaxValue = 8,
            Value = 3,
            Compact = true,
        }));
        content.AddChild(Track(new UiTokenSlider
        {
            LabelText = "Locked slider",
            MinValue = 0,
            MaxValue = 100,
            Value = 50,
            Locked = true,
        }));
        content.AddChild(Track(new UiTokenSlider
        {
            LabelText = "Disabled slider",
            MinValue = 0,
            MaxValue = 100,
            Value = 18,
            Disabled = true,
            Compact = true,
        }));
        content.AddChild(Track(new UiRangeSlider
        {
            LabelText = "Angle limits",
            Low = -40,
            High = 70,
            Mark = 12,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        }));

        var chips = CreateFlow();
        chips.AddChild(Track(new UiChip { Text = "Brain 1 × 4", Kind = UiChip.ChipKind.Accent }));
        chips.AddChild(Track(new UiChip { Text = "Spring locked", Kind = UiChip.ChipKind.Locked }));
        chips.AddChild(Track(new UiChip { Text = "Warn", Kind = UiChip.ChipKind.Warning }));
        chips.AddChild(Track(new UiChip { Text = "Danger", Kind = UiChip.ChipKind.Danger }));
        chips.AddChild(Track(new UiChip { Text = "Bad", Kind = UiChip.ChipKind.Bad }));
        chips.AddChild(Track(new UiChip { Text = "OK", Kind = UiChip.ChipKind.Ok }));
        content.AddChild(chips);

        return WrapSection("Numeric inputs · c_slider / c_range / c_chip", content);
    }

    private Control CreateChoiceAndRowsSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", UiSpacing.StackGap(_tokens));

        var toggles = new HFlowContainer();
        toggles.AddThemeConstantOverride("h_separation", UiSpacing.ControlGap(_tokens));
        toggles.AddThemeConstantOverride("v_separation", UiSpacing.ControlGap(_tokens));
        toggles.AddChild(Track(new UiToggleRow { LabelText = "Sounds", Subtext = "On", On = true, SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        toggles.AddChild(Track(new UiToggleRow { LabelText = "Dense toggle", Subtext = "Off", On = false, Dense = true, SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        toggles.AddChild(Track(new UiToggleRow { LabelText = "Locked sound", Subtext = "Disabled", Disabled = true, SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        toggles.AddChild(Track(new UiCheckRow { Checked = true, SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        toggles.AddChild(Track(new UiCheckRow { LabelText = "Needs battery", Subtext = "Disabled reason", Disabled = true, SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        content.AddChild(toggles);

        var pickers = new HFlowContainer();
        pickers.AddThemeConstantOverride("h_separation", UiSpacing.ControlGap(_tokens));
        pickers.AddThemeConstantOverride("v_separation", UiSpacing.ControlGap(_tokens));
        pickers.AddChild(Track(new UiPicker { LabelText = "Fixed part", ValueText = "Beam A", SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        pickers.AddChild(Track(new UiPicker { LabelText = "Target part", ValueText = "Beam A", Open = true, SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        pickers.AddChild(Track(new UiPicker { LabelText = "Wheel target", ValueText = "Wheel", Locked = true, SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        content.AddChild(pickers);

        var rows = new HFlowContainer();
        rows.AddThemeConstantOverride("h_separation", UiSpacing.ControlGap(_tokens));
        rows.AddThemeConstantOverride("v_separation", UiSpacing.ControlGap(_tokens));
        rows.AddChild(Track(new UiPartRow { PartIconId = UiPartIconId.Servo, PartName = "Servo", Count = "1", State = UiComponentContracts.SemanticState.Selected, SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        rows.AddChild(Track(new UiPartRow { PartIconId = UiPartIconId.Spring, PartName = "Spring", Count = "0", State = UiComponentContracts.SemanticState.Disabled, SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        rows.AddChild(Track(new UiPartRow { PartIconId = UiPartIconId.LineOfSight, PartName = "LOS sensor", Count = "2", State = UiComponentContracts.SemanticState.Locked, SizeFlagsHorizontal = SizeFlags.ExpandFill }));
        rows.AddChild(Track(new UiIconTabs { ActiveIndex = 1 }));
        content.AddChild(rows);

        return WrapSection("Choices and tray rows · c_toggle / c_check / c_pick / c_row / c_tabs", content);
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

    private Control CreateOverlaysSection()
    {
        var content = CreateFlow();

        var menu = Track(new UiOverflowMenu
        {
            Visible = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });
        menu.SetActions(
            new UiOverflowMenu.MenuAction("open", "Open creation", UiIconId.Play, UiComponentContracts.SemanticState.Neutral),
            new UiOverflowMenu.MenuAction("duplicate", "Duplicate", UiIconId.Copy, UiComponentContracts.SemanticState.Neutral),
            new UiOverflowMenu.MenuAction("locked", "Locked action", UiIconId.Lock, UiComponentContracts.SemanticState.Locked),
            new UiOverflowMenu.MenuAction("delete", "Delete creation", UiIconId.Trash, UiComponentContracts.SemanticState.Danger));
        menu.CallDeferred(CanvasItem.MethodName.Show);
        content.AddChild(menu);

        return WrapSection("Overflow menu · c_menu", content);
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
            case UiActionButton button:
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
            case UiTokenSlider slider:
                slider.Tokens = tokens;
                break;
            case UiHoldButton holdButton:
                holdButton.Tokens = tokens;
                break;
            case UiStepperButton stepperButton:
                stepperButton.Tokens = tokens;
                break;
            case UiRangeSlider rangeSlider:
                rangeSlider.Tokens = tokens;
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
