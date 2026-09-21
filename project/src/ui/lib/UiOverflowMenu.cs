using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Compact menu for infrequent actions that should not occupy the main shell.</summary>
public partial class UiOverflowMenu : PanelContainer
{
    [Signal]
    public delegate void ActionSelectedEventHandler(string actionId);

    private UiTokens _tokens = UiTokens.Neon;
    private VBoxContainer? _items;
    public readonly record struct MenuAction(string Id, string Label, UiIconId? Icon, UiComponentContracts.SemanticState State);

    private MenuAction[]? _pendingActions;
    private MenuAction[] _currentActions = [];
    private float _width;

    [Export]
    public float Width
    {
        get => _width;
        set
        {
            _width = Math.Max(0, value);
            RefreshStyle();
            if (_items is not null)
            {
                SetActions(_currentActions);
            }
        }
    }

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
        }
    }

    public override void _Ready()
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", (int)_tokens.Space2);
        margin.AddThemeConstantOverride("margin_top", (int)_tokens.Space2);
        margin.AddThemeConstantOverride("margin_right", (int)_tokens.Space2);
        margin.AddThemeConstantOverride("margin_bottom", (int)_tokens.Space2);
        AddChild(margin);
        _items = new VBoxContainer();
        _items.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        margin.AddChild(_items);
        RefreshStyle();
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

        foreach (var action in actions)
        {
            var button = new Button
            {
                Text = action.Label,
                CustomMinimumSize = new Vector2(MenuWidth, _tokens.TouchTarget),
                Alignment = HorizontalAlignment.Left,
                Disabled = action.State is UiComponentContracts.SemanticState.Disabled or UiComponentContracts.SemanticState.Locked,
                TooltipText = action.Label,
            };
            _tokens.ApplyTextStyle(button, _tokens.LabelText);
            button.AddThemeColorOverride("font_color", TextColor(action.State));
            button.AddThemeColorOverride("font_hover_color", _tokens.Ink);
            button.AddThemeColorOverride("font_pressed_color", _tokens.OnAccent);
            button.AddThemeColorOverride("font_disabled_color", _tokens.Muted);
            button.AddThemeStyleboxOverride("normal", CreateActionStyle(action.State, false));
            button.AddThemeStyleboxOverride("hover", CreateActionStyle(action.State, true));
            button.AddThemeStyleboxOverride("pressed", CreateActionStyle(action.State, true));
            button.AddThemeStyleboxOverride("focus", _tokens.FocusRingStyle());
            button.AddThemeStyleboxOverride("disabled", CreateActionStyle(action.State, false, 0.5f));
            if (action.Icon is { } icon)
            {
                UiIcons.Apply(button, icon, UiIconSize.Large, TextColor(action.State));
            }
            var id = action.Id;
            button.Pressed += () =>
            {
                Hide();
                EmitSignal(SignalName.ActionSelected, id);
            };
            _items.AddChild(button);
        }
    }

    private Color TextColor(UiComponentContracts.SemanticState state) =>
        state switch
        {
            UiComponentContracts.SemanticState.Danger or UiComponentContracts.SemanticState.Bad => _tokens.Danger,
            UiComponentContracts.SemanticState.Warning => _tokens.Halo,
            UiComponentContracts.SemanticState.Locked or UiComponentContracts.SemanticState.Disabled => _tokens.Muted,
            _ => _tokens.Ink,
        };

    private StyleBoxFlat CreateActionStyle(UiComponentContracts.SemanticState state, bool focused, float opacity = 1)
    {
        var danger = state is UiComponentContracts.SemanticState.Danger or UiComponentContracts.SemanticState.Bad;
        return new StyleBoxFlat
        {
            BgColor = UiTokens.MultiplyAlpha(focused ? (danger ? UiTokens.WithAlpha(_tokens.Danger, 0.18f) : _tokens.AccentSoft) : Colors.Transparent, opacity),
            BorderColor = UiTokens.MultiplyAlpha(danger ? _tokens.Danger : _tokens.Edge, opacity),
            BorderWidthLeft = (int)(focused && danger ? _tokens.StrokeSignal : _tokens.StrokeHair),
            BorderWidthTop = (int)_tokens.StrokeHair,
            BorderWidthRight = (int)_tokens.StrokeHair,
            BorderWidthBottom = (int)_tokens.StrokeHair,
            CornerRadiusTopLeft = (int)_tokens.RadiusMedium,
            CornerRadiusTopRight = (int)_tokens.RadiusMedium,
            CornerRadiusBottomLeft = (int)_tokens.RadiusMedium,
            CornerRadiusBottomRight = (int)_tokens.RadiusMedium,
        };
    }

    private void RefreshStyle()
    {
        if (!IsInsideTree())
        {
            return;
        }

        AddThemeStyleboxOverride("panel", _tokens.FrameStyle(UiSurfaceContracts.FrameVariant.Menu));
    }

    private float MenuWidth => _width > 0 ? _width : _tokens.MenuWidth;
}
