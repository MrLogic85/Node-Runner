using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Compact menu for infrequent actions that should not occupy the main shell.</summary>
public partial class UiOverflowMenu : PanelContainer
{
    [Signal]
    public delegate void ActionSelectedEventHandler(string actionId);

    private UiTokens _tokens = UiTokens.Neon;
    private VBoxContainer? _items;
    private (string Id, string Label, bool Danger)[]? _pendingActions;
    private (string Id, string Label, bool Danger)[] _currentActions = System.Array.Empty<(string, string, bool)>();

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
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        AddChild(margin);
        _items = new VBoxContainer();
        _items.AddThemeConstantOverride("separation", 4);
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

    public void SetActions(params (string Id, string Label, bool Danger)[] actions)
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
                Text = action.Danger ? $"!  {action.Label}" : action.Label,
                CustomMinimumSize = new Vector2(180, _tokens.TouchTarget),
                Alignment = HorizontalAlignment.Left,
            };
            button.AddThemeFontSizeOverride("font_size", (int)_tokens.LabelFontSize);
            button.AddThemeColorOverride("font_color", action.Danger ? _tokens.Danger : _tokens.Ink);
            button.AddThemeColorOverride("font_hover_color", _tokens.Ink);
            button.AddThemeColorOverride("font_pressed_color", _tokens.OnAccent);
            button.AddThemeStyleboxOverride("normal", CreateActionStyle(action.Danger, false));
            button.AddThemeStyleboxOverride("hover", CreateActionStyle(action.Danger, true));
            button.AddThemeStyleboxOverride("pressed", CreateActionStyle(action.Danger, true));
            button.AddThemeStyleboxOverride("focus", CreateActionStyle(action.Danger, true));
            var id = action.Id;
            button.Pressed += () =>
            {
                Hide();
                EmitSignal(SignalName.ActionSelected, id);
            };
            _items.AddChild(button);
        }
    }

    private StyleBoxFlat CreateActionStyle(bool danger, bool focused)
    {
        var color = danger ? _tokens.Danger : _tokens.Accent;
        return new StyleBoxFlat
        {
            BgColor = focused ? (danger ? _tokens.Danger with { A = 0.18f } : _tokens.AccentSoft) : Colors.Transparent,
            BorderColor = danger ? _tokens.Danger : _tokens.Edge,
            BorderWidthLeft = focused && danger ? 2 : 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = (int)_tokens.Radius,
            CornerRadiusTopRight = (int)_tokens.Radius,
            CornerRadiusBottomLeft = (int)_tokens.Radius,
            CornerRadiusBottomRight = (int)_tokens.Radius,
        };
    }

    private void RefreshStyle()
    {
        if (!IsInsideTree())
        {
            return;
        }

        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = _tokens.PanelRaised,
            BorderColor = _tokens.LineStrong,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = (int)_tokens.Radius,
            CornerRadiusTopRight = (int)_tokens.Radius,
            CornerRadiusBottomLeft = (int)_tokens.Radius,
            CornerRadiusBottomRight = (int)_tokens.Radius,
        });
    }
}
