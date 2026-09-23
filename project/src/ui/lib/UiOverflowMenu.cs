using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Compact menu for infrequent actions that should not occupy the main shell.</summary>
public partial class UiOverflowMenu : PanelContainer
{
    public enum MenuWidthMode
    {
        Fixed,
        WrapContent,
    }

    [Signal]
    public delegate void ActionSelectedEventHandler(string actionId);

    private UiTokens _tokens = UiTokens.Neon;
    private VBoxContainer? _items;
    public readonly record struct MenuAction(string Id, string Label, UiIconId? Icon, UiComponentContracts.SemanticState State);

    private MenuAction[]? _pendingActions;
    private MenuAction[] _currentActions = [];
    private float _width;
    private MenuWidthMode _widthMode = MenuWidthMode.Fixed;
    private bool _closeOnSelect = true;

    [Export]
    public float Width
    {
        get => _width;
        set
        {
            _width = Math.Max(0, value);
            RefreshWidth();
        }
    }

    [Export]
    public MenuWidthMode WidthMode
    {
        get => _widthMode;
        set
        {
            _widthMode = value;
            RefreshWidth();
        }
    }

    [Export]
    public bool CloseOnSelect
    {
        get => _closeOnSelect;
        set => _closeOnSelect = value;
    }

    public static float ResolveFixedWidth(float width, UiTokens tokens) =>
        width > 0 ? width : tokens.MenuWidth;

    public static float ResolveRowWidth(MenuWidthMode mode, float width, UiTokens tokens) =>
        mode == MenuWidthMode.WrapContent
            ? 0
            : Mathf.Max(0, ResolveFixedWidth(width, tokens) - (tokens.StrokeHair * 2));

    public static float ResolveContainerWidth(MenuWidthMode mode, float width, UiTokens tokens) =>
        mode == MenuWidthMode.WrapContent ? 0 : ResolveFixedWidth(width, tokens);

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            RefreshStyle();
            if (_items is not null)
            {
                SetActions(_currentActions);
            }
            else
            {
                RefreshWidth();
            }
        }
    }

    public override void _Ready()
    {
        SizeFlagsVertical = SizeFlags.ShrinkBegin;
        _items = new VBoxContainer();
        _items.AddThemeConstantOverride("separation", 0);
        AddChild(_items);
        RefreshStyle();
        RefreshWidth();
        if (_pendingActions is not null)
        {
            var actions = _pendingActions;
            _pendingActions = null;
            SetActions(actions);
        }
        Hide();
    }

    public void SetActions(params (string Id, string Label, bool Danger)[] actions) =>
        SetActions(actions.Select(action => new MenuAction(
            action.Id,
            action.Label,
            action.Danger ? UiIconId.Trash : null,
            action.Danger ? UiComponentContracts.SemanticState.Danger : UiComponentContracts.SemanticState.Neutral)).ToArray());

    public void SetActions(params MenuAction[] actions)
    {
        _currentActions = actions;
        if (_items is null)
        {
            _pendingActions = actions;
            return;
        }

        foreach (var child in _items.GetChildren())
        {
            _items.RemoveChild(child);
            child.QueueFree();
        }

        for (var index = 0; index < actions.Length; index++)
        {
            if (index > 0)
            {
                _items.AddChild(CreateSeparator());
            }

            var action = actions[index];
            var button = new Button
            {
                Text = action.Label,
                Alignment = HorizontalAlignment.Left,
                Disabled = action.State is UiComponentContracts.SemanticState.Disabled or UiComponentContracts.SemanticState.Locked,
                TooltipText = action.Label,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            _tokens.ApplyTextStyle(button, _tokens.BodyStrongText);
            button.AddThemeConstantOverride("h_separation", (int)_tokens.Space2);
            button.AddThemeColorOverride("font_color", TextColor(action.State));
            button.AddThemeColorOverride("font_hover_color", _tokens.Ink);
            button.AddThemeColorOverride("font_pressed_color", TextColor(action.State));
            button.AddThemeColorOverride("font_disabled_color", _tokens.Muted);
            var first = index == 0;
            var last = index == actions.Length - 1;
            button.AddThemeStyleboxOverride("normal", CreateActionStyle(action.State, false, first, last));
            button.AddThemeStyleboxOverride("hover", CreateActionStyle(action.State, true, first, last));
            button.AddThemeStyleboxOverride("pressed", CreateActionStyle(action.State, true, first, last));
            button.AddThemeStyleboxOverride("focus", CreateActionStyle(action.State, true, first, last));
            button.AddThemeStyleboxOverride("disabled", CreateActionStyle(action.State, false, first, last, 0.5f));
            if (action.Icon is { } icon)
            {
                UiIcons.Apply(button, icon, UiIconSize.Large, IconColor(action.State));
            }
            var id = action.Id;
            button.Pressed += () =>
            {
                if (CloseOnSelect)
                {
                    Hide();
                }

                EmitSignal(SignalName.ActionSelected, id);
            };
            _items.AddChild(button);
        }

        RefreshWidth();
    }

    private Color TextColor(UiComponentContracts.SemanticState state) =>
        state switch
        {
            UiComponentContracts.SemanticState.Danger or UiComponentContracts.SemanticState.Bad => _tokens.Danger,
            UiComponentContracts.SemanticState.Warning => _tokens.Halo,
            UiComponentContracts.SemanticState.Locked or UiComponentContracts.SemanticState.Disabled => _tokens.Muted,
            _ => _tokens.Ink,
        };

    private Color IconColor(UiComponentContracts.SemanticState state) =>
        state switch
        {
            UiComponentContracts.SemanticState.Danger or UiComponentContracts.SemanticState.Bad => _tokens.Danger,
            UiComponentContracts.SemanticState.Locked or UiComponentContracts.SemanticState.Disabled => _tokens.Muted,
            _ => _tokens.Accent,
        };

    private Color RowBackground(UiComponentContracts.SemanticState state, bool focused)
    {
        if (state == UiComponentContracts.SemanticState.Selected || focused)
        {
            return _tokens.AccentSoft;
        }

        return Colors.Transparent;
    }

    private StyleBoxFlat CreateActionStyle(
        UiComponentContracts.SemanticState state,
        bool focused,
        bool first,
        bool last,
        float opacity = 1)
    {
        var radius = (int)Mathf.Max(0, _tokens.RadiusLarge - _tokens.StrokeHair);
        return new StyleBoxFlat
        {
            BgColor = UiTokens.MultiplyAlpha(RowBackground(state, focused), opacity),
            CornerRadiusTopLeft = first ? radius : 0,
            CornerRadiusTopRight = first ? radius : 0,
            CornerRadiusBottomLeft = last ? radius : 0,
            CornerRadiusBottomRight = last ? radius : 0,
            ContentMarginLeft = _tokens.Space3,
            ContentMarginRight = _tokens.Space3,
            ContentMarginTop = 0,
            ContentMarginBottom = 0,
        };
    }

    private ColorRect CreateSeparator() =>
        new()
        {
            Color = _tokens.Line,
            CustomMinimumSize = new Vector2(0, _tokens.StrokeHair),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };

    private void RefreshStyle()
    {
        if (!IsInsideTree())
        {
            return;
        }

        var style = _tokens.FrameStyle(
            UiSurfaceContracts.FrameVariant.Menu,
            UiSurfaceContracts.FrameSize.Flush);
        style.ContentMarginLeft = _tokens.StrokeHair;
        style.ContentMarginRight = _tokens.StrokeHair;
        style.ContentMarginTop = _tokens.StrokeHair;
        style.ContentMarginBottom = _tokens.StrokeHair;
        UiGlow.ApplyButtonGlow(style, _tokens.Accent, _tokens.EffectsEnabled);
        AddThemeStyleboxOverride("panel", style);
    }

    private void RefreshWidth()
    {
        if (_items is not null)
        {
            foreach (var child in _items.GetChildren())
            {
                if (child is Button control)
                {
                    control.CustomMinimumSize = new Vector2(RowWidth, _tokens.TouchTarget);
                }
            }
        }

        CustomMinimumSize = new Vector2(ContainerWidth, 0);
        QueueSort();
        UpdateMinimumSize();
    }

    private float RowWidth => ResolveRowWidth(WidthMode, _width, _tokens);

    private float ContainerWidth => ResolveContainerWidth(WidthMode, _width, _tokens);
}
