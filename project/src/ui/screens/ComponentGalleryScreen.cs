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
    private ScrollContainer? _scroll;
    private Control? _scrollContent;
    private UiFrame? _frame;

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
        _frame = Track(GetNode<UiFrame>("%UiFrame"));
        BindHeader();
        _scroll = GetNode<ScrollContainer>("%Scroll");
        _scrollContent = GetNode<MarginContainer>("%ContentFrame");
        var runtimeSections = GetNode<VBoxContainer>("%RuntimeSections");
        BindAuthoredControls(runtimeSections.GetParent<Control>());

        UiNativeScroll.AllowGesturesToBubble(_scrollContent);
        _boundsOverlay = new UiBoundsDebugOverlay
        {
            RootPath = _frame.GetPath(),
        };
        AddChild(_boundsOverlay);
        ShowDebugBounds = ShowDebugBounds || ProjectSettings.GetSetting("ui/component_gallery_debug_bounds", false).AsBool();
        CreateToolbarMenu();
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

        var gallery = GD.Load<PackedScene>("res://scenes/screens/PopupGalleryScreen.tscn").Instantiate<PopupGalleryScreen>();
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
