using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Reference side panel (<c>ComponentToolbars</c> "SideBar"): the fixed panel on the right,
/// <see cref="UiLayout.SidePanelWidth"/> wide, with a divider down its left edge. Its own header
/// row holds an optional icon, an optional title and a bare chevron; the screen authors the
/// content below it in <c>%SidePanelContent</c> and decides what the panel shows. Tapping the
/// chevron collapses it to a <see cref="UiLayout.SidePanelTabWidth"/> tab with a left chevron
/// and the title on its side; tapping the tab expands it again.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiSidePanel : MarginContainer
{
    [Signal]
    public delegate void CollapsedChangedEventHandler(bool collapsed);

    private const float _unbounded = -1;
    private const double _collapseSeconds = 0.2;

    private string _title = "";
    private Func<string>? _titleSource;
    private UiIconId _iconId = UiIconId.None;
    private bool _collapsed;
    private Control? _pressedTarget;
    private bool _pressInside;
    private float _width = UiLayout.SidePanelWidth;
    private Tween? _collapseTween;

    [Export]
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            Refresh();
        }
    }

    /// <summary>
    /// Already translated text shown instead of <see cref="Title"/>; asked again when the language
    /// changes, with the title labels' own auto-translation off meanwhile. Null shows Title.
    /// </summary>
    public Func<string>? TitleSource
    {
        get => _titleSource;
        set
        {
            _titleSource = value;
            Refresh();
        }
    }

    [Export]
    public UiIconId IconId
    {
        get => _iconId;
        set
        {
            _iconId = value;
            Refresh();
        }
    }

    /// <summary>Only the user collapses the panel; a narrow screen never does it for them.</summary>
    [Export]
    public bool Collapsed
    {
        get => _collapsed;
        set
        {
            if (_collapsed == value)
            {
                return;
            }

            _collapsed = value;
            if (!IsNodeReady() || !IsInsideTree() || Engine.IsEditorHint())
            {
                Refresh();
                return;
            }

            AnimateCollapse();
            EmitSignal(SignalName.CollapsedChanged, value);
        }
    }

    private float TargetWidth => Collapsed ? UiLayout.SidePanelTabWidth : UiLayout.SidePanelWidth;

    public override Vector2 _GetMaximumSize() => new(_width, _unbounded);

    // Paddings, separations and slot sizes are the scene's own (#331, #335).
    public override void _Ready()
    {
        GetNode<Control>("%SidePanelIcon").Draw += DrawHeaderIcon;
        GetNode<Control>("%SidePanelChevron").Draw += DrawCollapseChevron;
        GetNode<Control>("%SidePanelTabChevron").Draw += DrawExpandChevron;
        GetNode<Control>("%SidePanelTab").Draw += DrawTabPress;
        if (!Engine.IsEditorHint())
        {
            GetNode<Control>("%SidePanelChevron").GuiInput += OnChevronInput;
            GetNode<Control>("%SidePanelTab").GuiInput += OnTabInput;
        }

        Refresh();
    }

    private readonly UiUnsavedState _unsaved = new(
        [
            (".", Control.PropertyName.CustomMinimumSize),
            ("%SidePanelDrawer", CanvasItem.PropertyName.Visible),
            ("%SidePanelDrawer", CanvasItem.PropertyName.Modulate),
            ("%SidePanelTab", CanvasItem.PropertyName.Visible),
            ("%SidePanelTab", CanvasItem.PropertyName.Modulate),
            ("%SidePanelIcon", CanvasItem.PropertyName.Visible),
            ("%SidePanelTitle", Label.PropertyName.Text),
            ("%SidePanelTabLabel", UiVerticalLabel.PropertyName.Text),
            ("%SidePanelTabLabel", CanvasItem.PropertyName.Visible),
        ]);

    public override void _Notification(int what)
    {
        if (_unsaved.Handle(this, what, Refresh))
        {
            return;
        }

        if (what == NotificationThemeChanged && IsNodeReady())
        {
            RedrawIcons();
            QueueRedraw();
        }
        else if (what == NotificationTranslationChanged && TitleSource is not null)
        {
            Refresh();
        }
    }

    public override void _Draw() =>
        DrawRect(new Rect2(Vector2.Zero, Size), UiThemeLookup.Color(this, UiTokens.Color.Panel));

    private void Refresh()
    {
        if (!IsNodeReady())
        {
            return;
        }

        if (_collapseTween is null)
        {
            SettleCollapse();
        }

        GetNode<Control>("%SidePanelIcon").Visible = IconId != UiIconId.None;
        var title = TitleSource?.Invoke() ?? Title;
        var translateMode = TitleSource is null ? AutoTranslateModeEnum.Inherit : AutoTranslateModeEnum.Disabled;
        var titleLabel = GetNode<Label>("%SidePanelTitle");
        titleLabel.AutoTranslateMode = translateMode;
        UiTranslation.ShareContext(this, titleLabel);
        titleLabel.Text = title;
        var tabLabel = GetNode<UiVerticalLabel>("%SidePanelTabLabel");
        tabLabel.AutoTranslateMode = translateMode;
        UiTranslation.ShareContext(this, tabLabel);
        tabLabel.Text = title;
        tabLabel.Visible = title.Length > 0;
        RedrawIcons();
    }

    private void SetWidth(float width)
    {
        _width = width;
        CustomMinimumSize = new Vector2(width, 0);
        UpdateMaximumSize();
    }

    private void SettleCollapse()
    {
        _collapseTween?.Kill();
        _collapseTween = null;
        SetWidth(TargetWidth);
        var drawer = GetNode<Control>("%SidePanelDrawer");
        var tab = GetNode<Control>("%SidePanelTab");
        drawer.Visible = !Collapsed;
        tab.Visible = Collapsed;
        drawer.Modulate = Colors.White;
        tab.Modulate = Colors.White;
    }

    // The drawer keeps its full width and is clipped as the panel narrows, so its content
    // slides out past the panel's edge instead of reflowing, while the tab fades in.
    private void AnimateCollapse()
    {
        _collapseTween?.Kill();
        var drawer = GetNode<Control>("%SidePanelDrawer");
        var tab = GetNode<Control>("%SidePanelTab");
        foreach (var part in new[] { drawer, tab })
        {
            if (!part.Visible)
            {
                part.Modulate = Colors.Transparent;
                part.Visible = true;
            }
        }

        _collapseTween = CreateTween()
            .SetParallel()
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.InOut);
        _collapseTween.TweenMethod(Callable.From<float>(SetWidth), _width, TargetWidth, _collapseSeconds);
        // The outgoing part fades out in the first half and the incoming part fades in
        // the second, so the tab's title never lies over the content.
        var (outgoing, incoming) = Collapsed ? (drawer, tab) : (tab, drawer);
        var half = _collapseSeconds / 2;
        _collapseTween.TweenProperty(outgoing, "modulate", Colors.Transparent, half);
        _collapseTween.TweenProperty(incoming, "modulate", Colors.White, half).SetDelay(half);
        _collapseTween.Chain().TweenCallback(Callable.From(SettleCollapse));
    }

    private void RedrawIcons()
    {
        GetNode<Control>("%SidePanelIcon").QueueRedraw();
        GetNode<Control>("%SidePanelChevron").QueueRedraw();
        GetNode<Control>("%SidePanelTabChevron").QueueRedraw();
    }

    // Icons are drawn rather than set as a TextureRect's texture: the icon texture is
    // built at runtime, and a screen that edits the panel's children would save it.
    private void DrawHeaderIcon()
    {
        if (IconId != UiIconId.None)
        {
            DrawIcon(GetNode<Control>("%SidePanelIcon"), IconId, UiIconSize.Large, UiTokens.Color.Ink);
        }
    }

    // The reference's handle is a bare muted chevron, not a button; its control is the touch area.
    private void DrawCollapseChevron()
    {
        var chevron = GetNode<Control>("%SidePanelChevron");
        DrawPress(chevron);
        DrawIcon(chevron, UiIconId.ChevronRight, UiIconSize.Standard, UiTokens.Color.Muted);
    }

    // The tab's chevron and label are its children, so they draw over the tint.
    private void DrawTabPress() => DrawPress(GetNode<Control>("%SidePanelTab"));

    // The press tint (#325) over the touch area while it is held and the pointer is still on it.
    private void DrawPress(Control target)
    {
        if (_pressedTarget == target && _pressInside)
        {
            UiPressFeedback.Draw(target, UiCorners.Uniform(UiSize.Radius.Small), UiTokens.Color.Panel, danger: false);
        }
    }

    // The reference's left chevron is named back.
    private void DrawExpandChevron() =>
        DrawIcon(GetNode<Control>("%SidePanelTabChevron"), UiIconId.Back, UiIconSize.Standard, UiTokens.Color.Muted);

    private void DrawIcon(Control target, UiIconId icon, UiIconSize size, UiTokens.Color color)
    {
        var pixels = Vector2.One * UiIcons.Pixels(size);
        var origin = (target.Size - pixels) / 2;
        target.DrawTextureRect(UiIcons.Load(icon, size), new Rect2(origin, pixels), tile: false, UiThemeLookup.Color(this, color));
    }

    private void OnChevronInput(InputEvent inputEvent) => OnToggleInput(inputEvent, GetNode<Control>("%SidePanelChevron"));

    private void OnTabInput(InputEvent inputEvent) => OnToggleInput(inputEvent, GetNode<Control>("%SidePanelTab"));

    // Toggles on a release over the control the press started on, so sliding off cancels.
    private void OnToggleInput(InputEvent inputEvent, Control control)
    {
        if (PointerInput.TryGetPressPosition(inputEvent, out _))
        {
            _pressedTarget = control;
            _pressInside = true;
            control.QueueRedraw();
            control.AcceptEvent();
            return;
        }

        if (PointerInput.TryGetDragPosition(inputEvent, out var dragged) && _pressedTarget == control)
        {
            var inside = new Rect2(Vector2.Zero, control.Size).HasPoint(dragged);
            if (inside != _pressInside)
            {
                _pressInside = inside;
                control.QueueRedraw();
            }
            return;
        }

        if (PointerInput.TryGetReleasePosition(inputEvent, out var position))
        {
            bool toggles = _pressedTarget == control && new Rect2(Vector2.Zero, control.Size).HasPoint(position);
            _pressedTarget = null;
            _pressInside = false;
            control.QueueRedraw();
            control.AcceptEvent();
            if (toggles)
            {
                Collapsed = !Collapsed;
            }
        }
    }
}
