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
    private UiMenu? _toolbarMenu;
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
        Menu,
        Picker,
        PartRows,
        PanelRows,
        StageCard,
    }

    public static IReadOnlyList<GalleryComponentSpec> CanonicalInventory { get; } =
    [
        new(UiComponentContracts.CanonicalComponent.Button, "Actions"),
        new(UiComponentContracts.CanonicalComponent.IconButton, "Actions"),
        new(UiComponentContracts.CanonicalComponent.HoldButton, "Actions"),
        new(UiComponentContracts.CanonicalComponent.TextField, "Text input"),
        new(UiComponentContracts.CanonicalComponent.NameField, "Text input"),
        new(UiComponentContracts.CanonicalComponent.Note, "Panel rows"),
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
        new(UiComponentContracts.CanonicalComponent.StageCard, "Stage card"),
        new(UiComponentContracts.CanonicalComponent.Menu, "Menu"),
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
        GallerySection.Menu,
        GallerySection.Picker,
        GallerySection.PartRows,
        GallerySection.PanelRows,
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
        _background = GetNode<ColorRect>("%Background");
        var frame = GetNode<MarginContainer>("%Frame");
        BindHeader();
        _scroll = GetNode<ScrollContainer>("%Scroll");
        _scrollContent = GetNode<MarginContainer>("%ContentFrame");
        var runtimeSections = GetNode<VBoxContainer>("%RuntimeSections");
        BindAuthoredControls(runtimeSections.GetParent<Control>());

        foreach (var section in RenderedSectionOrder)
        {
            if (section is not (
                GallerySection.Actions
                or GallerySection.Choices
                or GallerySection.Segmented
                or GallerySection.PartsTrayTabs
                or GallerySection.SelectionHandles
                or GallerySection.Number
                or GallerySection.Slider
                or GallerySection.Progress
                or GallerySection.Cards
                or GallerySection.Menu
                or GallerySection.Picker
                or GallerySection.PartRows
                or GallerySection.PanelRows
                or GallerySection.StageCard))
            {
                AddRenderedSection(runtimeSections, section);
            }
        }

        UiNativeScroll.AllowGesturesToBubble(_scrollContent);
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
            case GallerySection.TextInput:
                content.AddChild(CreateTextInputSection());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(section), section, null);
        }
    }

    private void BindHeader()
    {
        var title = GetNode<UiLabel>("%ToolbarTitle");
        _labelAppliers.Add(tokens =>
        {
            title.Tokens = tokens;
            title.AddThemeColorOverride("font_color", tokens.Ink);
        });

        var close = Track(GetNode<UiButton>("%CloseAction"));
        close.Visible = ShowCloseAction;
        close.Activated += () => EmitSignal(SignalName.CloseRequested);

        var switcher = Track(GetNode<UiSegmentedSwitch>("%ThemeSwitcher"));
        switcher.SelectionChanged += OnThemeSelectionChanged;

        _toolbarMore = Track(GetNode<UiButton>("%ToolbarMore"));
        _toolbarMore.Activated += ToggleToolbarMenu;
        _toolbarMore.ItemRectChanged += () => Callable.From(PositionToolbarMenu).CallDeferred();
    }

    private void OnThemeSelectionChanged(int index)
    {
        ApplyTokens(index switch
        {
            1 => UiTokens.Paper,
            2 => UiTokens.Neon.WithEffects(false),
            _ => UiTokens.Neon,
        });
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
        _toolbarMenu = Track(new UiMenu());
        _toolbarMenu.IndexClicked += index =>
        {
            if (index is 0 or 1)
            {
                HandleToolbarMenuAction(index);
            }
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
        if (_toolbarMenu is null)
        {
            return;
        }

        UiMenuItems.Populate(
            _toolbarMenu,
            [
                new UiMenuItemSpec(
                    "Debug bounds",
                    Selected: ShowDebugBounds),
                new UiMenuItemSpec("Popup Gallery", UiIconId.Model),
            ],
            _tokens,
            showSelectedIndicator: true);
    }

    private void HandleToolbarMenuAction(int index)
    {
        if (index == 0)
        {
            ShowDebugBounds = !ShowDebugBounds;
        }

        CloseToolbarMenu();
        if (index != 1)
        {
            return;
        }

        var gallery = GD.Load<PackedScene>("res://scenes/ui/PopupGalleryScreen.tscn").Instantiate<PopupGalleryScreen>();
        gallery.CloseRequested += () =>
        {
            gallery.QueueFree();
            Show();
        };
        GetParent().AddChild(gallery);
        Hide();
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

    private void BindAuthoredControls(Control control)
    {
        if (control is UiButton button)
        {
            Track(button);
            if (button.IsInGroup("gallery_toggle_button"))
            {
                button.Activated += () => button.Selected = !button.Selected;
            }
            return;
        }

        if (control is UiLabel label)
        {
            _labelAppliers.Add(tokens =>
            {
                label.Tokens = tokens;
                label.AddThemeColorOverride("font_color",
                    label.IsInGroup("gallery_muted") ? tokens.Muted : tokens.Ink);
            });
            return;
        }

        if (control is UiStageCard stageCard)
        {
            BindAuthoredStageCard(stageCard);
            Track(stageCard);
            return;
        }

        if (control is UiCard card)
        {
            Track(card);
            foreach (var child in card.GetChildren().OfType<Control>())
            {
                BindAuthoredControls(child);
            }
            return;
        }

        if (control is UiMenu menu)
        {
            Track(menu);
            foreach (var child in menu.GetChildren().OfType<Control>())
            {
                BindAuthoredControls(child);
            }
            return;
        }

        if (control is UiPicker picker)
        {
            BindAuthoredPicker(picker);
            Track(picker);
            return;
        }

        if (control is UiToggleRow
            or UiCheckRow
            or UiSegmentedSwitch
            or UiIconTabs
            or UiSelectionHandle
            or UiNumber
            or UiSlider
            or UiProgressRing
            or UiPartRow
            or UiMenuItem
            or UiNameField
            or UiValueRow
            or UiNoteRow)
        {
            Track(control);
            return;
        }

        foreach (var child in control.GetChildren().OfType<Control>())
        {
            BindAuthoredControls(child);
        }
    }

    private static void BindAuthoredPicker(UiPicker picker)
    {
        picker.Options = picker.Name.ToString() switch
        {
            "InteractivePicker" =>
            [
                new("Left thigh", UiIconId.Beam),
                new("Left shin", UiIconId.Beam, Note: "swaps"),
                new("Tail"),
            ],
            "LockedPicker" or "DisabledPicker" =>
            [
                new("Wheel 1", UiIconId.Beam),
            ],
            "FixedPartPicker" =>
            [
                new("Front thigh", UiIconId.Beam),
                new("Front shin", UiIconId.Beam),
            ],
            _ => picker.Options,
        };
    }

    private static void BindAuthoredStageCard(UiStageCard stageCard)
    {
        var body = stageCard.Name.ToString() switch
        {
            "Senses" => "9 readings · top first",
            "Thinks" => "24 neurons · 8 outputs",
            _ => string.Empty,
        };
        if (!string.IsNullOrWhiteSpace(body))
        {
            stageCard.SetBody(new Label
            {
                Text = body,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = MouseFilterEnum.Ignore,
            });
        }
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
