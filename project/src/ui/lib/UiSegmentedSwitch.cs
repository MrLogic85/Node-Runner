using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Touch-safe mutually exclusive choice control for compact mode/profile switches.</summary>
[Tool]
[GlobalClass]
public partial class UiSegmentedSwitch : HBoxContainer, ISerializationListener
{
    [Signal]
    public delegate void SelectionChangedEventHandler(int index);

    private UiTokens _tokens = UiTokens.Neon;
    private Godot.Collections.Array<UiSegment> _segments =
        [new() { Text = "1" }, new() { Text = "2" }];
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

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            ApplyContentAndTheme();
        }
    }

    public override void _EnterTree()
    {
        if (!IsConnected(SignalName.ChildEnteredTree, ChildAddedCallback))
            Connect(SignalName.ChildEnteredTree, ChildAddedCallback);
        RequestReady();
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
            button.Disabled = segment is null;
            button.Text = text;
            _tokens.ApplyTextStyle(button, _tokens.LabelText);
            foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
            {
                button.AddThemeColorOverride(state, _tokens.Ink);
            }

            if (hasIcon)
            {
                UiIcons.Apply(button, segment!.IconId, UiIconSize.Standard, _tokens.Ink);
                button.IconAlignment = hasText ? HorizontalAlignment.Left : HorizontalAlignment.Center;
                button.AddThemeColorOverride("icon_hover_pressed_color", _tokens.Ink);
                button.AddThemeColorOverride("icon_focus_color", _tokens.Ink);
            }
            else
            {
                button.Icon = null;
            }

            button.AddThemeConstantOverride("h_separation", hasText && hasIcon ? (int)_tokens.Space2 : 0);
            StyleBoxFlat normal = CreateStyle(index, false);
            StyleBoxFlat selected = CreateStyle(index, true);
            button.AddThemeStyleboxOverride("normal", normal);
            button.AddThemeStyleboxOverride("hover", normal);
            button.AddThemeStyleboxOverride("pressed", selected);
            button.AddThemeStyleboxOverride("hover_pressed", selected);
            StyleBoxFlat focus = CreateStyle(index, true);
            focus.DrawCenter = false;
            focus.BorderColor = _tokens.Halo;
            button.AddThemeStyleboxOverride("focus", focus);
        }

        ApplyLayout();
    }

    private void ApplyLayout()
    {
        AddThemeConstantOverride("separation", 0);
        float width = _tokens.TouchTarget;
        foreach (Button button in _buttons)
        {
            width = Mathf.Max(width, button.GetMinimumSize().X);
        }

        foreach (Button button in _buttons)
        {
            button.CustomMinimumSize = new Vector2(MatchWidth ? width : _tokens.TouchTarget, _tokens.ControlHeight);
            button.SizeFlagsHorizontal = MatchWidth ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin;
        }
    }

    private StyleBoxFlat CreateStyle(int index, bool selected)
    {
        bool first = index == 0;
        bool last = index == _segments.Count - 1;
        int stroke = (int)(selected ? _tokens.StrokeSignal : _tokens.StrokeHair);
        return new StyleBoxFlat
        {
            BgColor = selected ? _tokens.PanelRaised.Blend(_tokens.AccentSoft) : _tokens.PanelRaised,
            BorderColor = selected ? _tokens.Accent : _tokens.LineStrong,
            BorderWidthLeft = first || selected ? stroke : 0,
            BorderWidthTop = stroke,
            BorderWidthRight = stroke,
            BorderWidthBottom = stroke,
            CornerRadiusTopLeft = first ? (int)_tokens.RadiusMedium : 0,
            CornerRadiusTopRight = last ? (int)_tokens.RadiusMedium : 0,
            CornerRadiusBottomLeft = first ? (int)_tokens.RadiusMedium : 0,
            CornerRadiusBottomRight = last ? (int)_tokens.RadiusMedium : 0,
            ContentMarginLeft = UiSpacing.SegmentedControlHorizontalPadding(_tokens),
            ContentMarginRight = UiSpacing.SegmentedControlHorizontalPadding(_tokens),
        };
    }
}
