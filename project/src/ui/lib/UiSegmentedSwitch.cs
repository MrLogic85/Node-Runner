using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Touch-safe mutually exclusive choice control for compact mode/profile switches.</summary>
[Tool]
[GlobalClass]
public partial class UiSegmentedSwitch : HBoxContainer, ISerializationListener
{
    [Signal]
    public delegate void SelectionChangedEventHandler(int index);

    private Godot.Collections.Array<UiSegment> _segments =
        [new() { Text = "1" }, new() { Text = "2" }];
    private const float _disabledOpacity = 0.5f;
    private readonly HashSet<UiSegment> _observedSegments = [];
    private readonly List<Button> _buttons = [];
    private int _selectedIndex;
    private bool _matchWidth = true;
    private ButtonGroup? _group;
    // Object/method callables survive assembly reloads without retaining managed delegates.
    private Callable ChildAddedCallback => new(this, MethodName.WarnAboutExtraChild);
    private Callable GroupPressedCallback => new(this, MethodName.HandleGroupPressed);
    private Callable SegmentChangedCallback => new(this, MethodName.ApplyContentAndTheme);

    [Export]
    public Godot.Collections.Array<UiSegment> Segments
    {
        get => _segments;
        set
        {
            _segments = value?.Duplicate() ?? [];
            bool populated = false;
            for (int index = 0; index < _segments.Count; index++)
            {
                if (_segments[index] is not null)
                    continue;
                _segments[index] = new UiSegment { Text = (index + 1).ToString() };
                populated = true;
            }
            ObserveSegments();
            _selectedIndex = UiComponentContracts.NormalizeTabIndex(_selectedIndex, _segments.Count);
            RebuildButtons();
            if (populated && Engine.IsEditorHint())
                CallDeferred(GodotObject.MethodName.NotifyPropertyListChanged);
        }
    }

    [Export]
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            _selectedIndex = UiComponentContracts.NormalizeTabIndex(value, _segments.Count);
            ApplySelection();
        }
    }

    [Export]
    public bool MatchWidth
    {
        get => _matchWidth;
        set
        {
            _matchWidth = value;
            ApplyLayout();
        }
    }

    public override void _EnterTree()
    {
        if (!IsConnected(SignalName.ChildEnteredTree, ChildAddedCallback))
            Connect(SignalName.ChildEnteredTree, ChildAddedCallback);
        RequestReady();
    }

    private readonly UiUnsavedState _unsaved = new("theme_override_constants/separation");

    public override void _Notification(int what)
    {
        if (_unsaved.Handle(this, what, ApplyContentAndTheme))
        {
            return;
        }

        if (what == NotificationThemeChanged && IsNodeReady())
        {
            UiThemeRefresh.Guarded(this, ApplyContentAndTheme);
        }
        else if (what == NotificationSortChildren)
        {
            QueueRedraw();
        }
        else if (what == NotificationTranslationChanged && IsNodeReady())
        {
            ApplyContentAndTheme();
        }
    }

    // A disabled segment looks like a disabled UiButton: its own dashed outline over a 50% fill.
    // Drawn here, under the segment, which leaves its border transparent.
    public override void _Draw()
    {
        float stroke = UiSize.Stroke.Hair;
        Color color = UiThemeLookup.Color(this, UiTokens.Color.LineStrong).ScaleAlpha(_disabledOpacity);
        for (int index = 0; index < _buttons.Count; index++)
        {
            Button button = _buttons[index];
            if (!button.Disabled)
                continue;
            Rect2 rect = button.GetRect().Grow(-stroke * 0.5f);
            UiDashedBorder.DrawRoundedRect(this, rect, CornersFor(index), color, stroke);
        }
    }

    private void WarnAboutExtraChild(Node child)
    {
        if (IsNodeReady() && GetChildren().Contains(child))
            GD.PushWarning($"{Name}: '{child.Name}' is not a segment. Use Segments to add options; this child is left unchanged.");
    }

    public override void _ExitTree()
    {
        if (IsConnected(SignalName.ChildEnteredTree, ChildAddedCallback))
            Disconnect(SignalName.ChildEnteredTree, ChildAddedCallback);
        DisconnectContent();
    }

    private void DisconnectContent()
    {
        if (_group is not null && _group.IsConnected(ButtonGroup.SignalName.Pressed, GroupPressedCallback))
            _group.Disconnect(ButtonGroup.SignalName.Pressed, GroupPressedCallback);
        DisconnectSegments();
    }

    private void DisconnectSegments()
    {
        foreach (UiSegment? segment in _observedSegments)
        {
            if (segment is not null && segment.IsConnected(Resource.SignalName.Changed, SegmentChangedCallback))
                segment.Disconnect(Resource.SignalName.Changed, SegmentChangedCallback);
        }
        _observedSegments.Clear();
    }

    public void OnBeforeSerialize()
    {
        if (IsConnected(SignalName.ChildEnteredTree, ChildAddedCallback))
            Disconnect(SignalName.ChildEnteredTree, ChildAddedCallback);
        DisconnectContent();
    }

    public void OnAfterDeserialize() => CallDeferred(MethodName.RestoreContent);

    private void RestoreContent()
    {
        if (!IsInsideTree())
            return;
        if (!IsConnected(SignalName.ChildEnteredTree, ChildAddedCallback))
            Connect(SignalName.ChildEnteredTree, ChildAddedCallback);
        InitializeContent();
    }

    private void ObserveSegments()
    {
        DisconnectSegments();
        if (!IsInsideTree())
            return;
        foreach (UiSegment? segment in _segments)
        {
            if (segment is not null && _observedSegments.Add(segment))
            {
                if (!segment.IsConnected(Resource.SignalName.Changed, SegmentChangedCallback))
                    segment.Connect(Resource.SignalName.Changed, SegmentChangedCallback);
            }
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        InitializeContent();
    }

    private void InitializeContent()
    {
        DisconnectContent();
        // Native children survive a C# assembly reload; recover them rather than adding duplicates.
        _buttons.Clear();
        _group = new ButtonGroup { AllowUnpress = false };
        _group.Connect(ButtonGroup.SignalName.Pressed, GroupPressedCallback);
        var authoredChildren = GetChildren();
        foreach (Node child in GetChildren(includeInternal: true))
        {
            if (child is Button button && !authoredChildren.Contains(child))
            {
                _buttons.Add(button);
                button.ButtonGroup = _group;
            }
        }
        ObserveSegments();
        RebuildButtons();
    }

    private void HandleGroupPressed(BaseButton button)
    {
        if (button is Button segmentButton && _buttons.IndexOf(segmentButton) is var index && index >= 0)
            Select(index);
    }

    private void RebuildButtons()
    {
        if (_group is null)
        {
            return;
        }

        int focusedIndex = -1;
        for (int index = 0; index < _buttons.Count; index++)
        {
            if (_buttons[index].HasFocus())
            {
                focusedIndex = index;
            }
        }

        while (_buttons.Count > _segments.Count)
        {
            Button button = _buttons[^1];
            _buttons.RemoveAt(_buttons.Count - 1);
            button.ButtonGroup = null;
            RemoveChild(button);
            button.QueueFree();
        }

        while (_buttons.Count < _segments.Count)
        {
            var button = new Button
            {
                ToggleMode = true,
                ButtonGroup = _group,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
                MouseFilter = MouseFilterEnum.Pass,
            };
            _buttons.Add(button);
            AddChild(button, false, InternalMode.Front);
        }

        ApplyContentAndTheme();
        ApplySelection();
        if (focusedIndex >= 0 && _buttons.Count > 0 && IsInsideTree())
        {
            _buttons[Math.Min(focusedIndex, _buttons.Count - 1)].GrabFocus();
        }
    }

    private void Select(int index)
    {
        if (Engine.IsEditorHint() || index == _selectedIndex)
        {
            return;
        }

        _selectedIndex = index;
        EmitSignal(SignalName.SelectionChanged, _selectedIndex);
    }

    private void ApplySelection()
    {
        if (_selectedIndex < _buttons.Count)
        {
            // SetPressedNoSignal bypasses ButtonGroup exclusivity. ButtonPressed
            // updates the group without emitting the Button.Pressed we forward.
            _buttons[_selectedIndex].ButtonPressed = true;
        }
    }

    private void ApplyContentAndTheme()
    {
        for (int index = 0; index < _buttons.Count; index++)
        {
            Button button = _buttons[index];
            UiSegment? segment = _segments[index];
            string text = string.IsNullOrWhiteSpace(segment?.Text) ? string.Empty : segment.Text;
            bool hasText = text.Length > 0;
            bool hasIcon = segment is { IconId: not UiIconId.None };
            button.Disabled = segment is null || segment.Disabled;
            // The switch translates the text itself, so it can be cased after translating.
            button.AutoTranslateMode = AutoTranslateModeEnum.Disabled;
            button.Text = UiThemeLookup.LetterCase(Atr(text), UiTokens.Typography.Label);
            UiThemeLookup.ApplyTypography(button, UiTokens.Typography.Label);

            // The segment draws its own text, so only its icon samples Linear (#734).
            var linear = button.Icon as UiLinearIcon;
            if (hasIcon)
            {
                UiIcons.Apply(button, segment!.IconId, UiIconSize.Standard);
                linear ??= new UiLinearIcon(button);
                linear.Icon = button.Icon;
                button.Icon = linear;
                button.IconAlignment = hasText ? HorizontalAlignment.Left : HorizontalAlignment.Center;
            }
            else
            {
                linear?.Detach();
                button.Icon = null;
            }

            button.AddThemeConstantOverride("h_separation", hasText && hasIcon ? (int)UiSize.Space.S2 : 0);
            StyleBoxFlat normal = CreateStyle(index, false);
            StyleBoxFlat selected = CreateStyle(index, true);
            button.AddThemeStyleboxOverride("normal", normal);
            button.AddThemeStyleboxOverride("hover", normal);
            button.AddThemeStyleboxOverride("pressed", selected);
            button.AddThemeStyleboxOverride("hover_pressed", selected);
            button.AddThemeStyleboxOverride("disabled", CreateDisabledStyle(index));
            StyleBoxFlat focus = CreateStyle(index, true);
            focus.DrawCenter = false;
            focus.BorderColor = UiThemeLookup.Color(this, UiTokens.Color.Halo);
            button.AddThemeStyleboxOverride("focus", focus);
        }

        ApplyLayout();
        QueueRedraw();
    }

    private StyleBoxFlat CreateDisabledStyle(int index)
    {
        StyleBoxFlat style = CreateStyle(index, false);
        style.BgColor = style.BgColor.ScaleAlpha(_disabledOpacity);
        style.BorderColor = Colors.Transparent;
        style.BorderWidthLeft = (int)UiSize.Stroke.Hair;
        return style;
    }

    private void ApplyLayout()
    {
        AddThemeConstantOverride("separation", 0);
        float width = UiSize.Control.Touch;
        foreach (Button button in _buttons)
        {
            width = Mathf.Max(width, button.GetMinimumSize().X);
        }

        foreach (Button button in _buttons)
        {
            button.CustomMinimumSize = new Vector2(MatchWidth ? width : UiSize.Control.Touch, UiSize.Control.Default);
            button.SizeFlagsHorizontal = MatchWidth ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin;
        }
    }

    private StyleBoxFlat CreateStyle(int index, bool selected)
    {
        bool first = index == 0;
        int stroke = (int)(selected ? UiSize.Stroke.Signal : UiSize.Stroke.Hair);
        StyleBoxFlat style = new()
        {
            BgColor = selected
                ? UiThemeLookup.Color(this, UiTokens.Color.PanelRaised).Blend(UiThemeLookup.Color(this, UiTokens.Color.Accent).WithAlpha(UiThemeLookup.Alpha(this, UiTokens.Alpha.Soft)))
                : UiThemeLookup.Color(this, UiTokens.Color.PanelRaised),
            BorderColor = selected ? UiThemeLookup.Color(this, UiTokens.Color.Accent) : UiThemeLookup.Color(this, UiTokens.Color.LineStrong),
            BorderWidthLeft = first || selected ? stroke : 0,
            BorderWidthTop = stroke,
            BorderWidthRight = stroke,
            BorderWidthBottom = stroke,
            ContentMarginLeft = UiSpacing.SegmentedControlHorizontalPadding,
            ContentMarginRight = UiSpacing.SegmentedControlHorizontalPadding,
        };
        CornersFor(index).ApplyTo(style);
        return style;
    }

    // Only the switch's outer corners are round, so its segments read as one control.
    private UiCorners CornersFor(int index)
    {
        float left = index == 0 ? UiSize.Radius.Medium : 0;
        float right = index == _segments.Count - 1 ? UiSize.Radius.Medium : 0;
        return new UiCorners(left, right, right, left);
    }
}
