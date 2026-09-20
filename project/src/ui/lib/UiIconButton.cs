using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Touch-safe icon-labelled action with a stable square hit target.</summary>
public partial class UiIconButton : Button
{
    private UiTokens _tokens = UiTokens.Neon;

    private string _iconText = "•";

    [Export]
    public string IconText
    {
        get => _iconText;
        set
        {
            _iconText = value;
            RefreshStyle();
        }
    }

    private string _accessibleLabel = string.Empty;

    [Export]
    public string AccessibleLabel
    {
        get => _accessibleLabel;
        set
        {
            _accessibleLabel = value;
            RefreshStyle();
        }
    }

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            RefreshStyle();
        }
    }

    public override void _Ready()
    {
        TooltipText = AccessibleLabel;
        CustomMinimumSize = new Vector2(_tokens.TouchTarget, _tokens.TouchTarget);
        RefreshStyle();
    }

    private void RefreshStyle()
    {
        if (!IsInsideTree())
        {
            return;
        }

        Text = IconText;
        TooltipText = AccessibleLabel;
        CustomMinimumSize = new Vector2(_tokens.TouchTarget, _tokens.TouchTarget);
        _tokens.ApplyTextStyle(this, _tokens.HeadingText);
        AddThemeColorOverride("font_color", _tokens.Ink);
        AddThemeColorOverride("font_hover_color", _tokens.Accent);
        AddThemeColorOverride("font_pressed_color", _tokens.OnAccent);
        AddThemeStyleboxOverride("normal", CreateStyle(false));
        AddThemeStyleboxOverride("hover", CreateStyle(true));
        AddThemeStyleboxOverride("pressed", CreateStyle(true));
        AddThemeStyleboxOverride("focus", CreateStyle(true, 2));
        AddThemeStyleboxOverride("disabled", CreateStyle(false, 1, 0.5f));
    }

    private StyleBoxFlat CreateStyle(bool focused, int borderWidth = 1, float opacity = 1)
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(
                (focused ? _tokens.AccentSoft : _tokens.Panel).R,
                (focused ? _tokens.AccentSoft : _tokens.Panel).G,
                (focused ? _tokens.AccentSoft : _tokens.Panel).B,
                (focused ? _tokens.AccentSoft : _tokens.Panel).A * opacity),
            BorderColor = new Color(_tokens.Edge.R, _tokens.Edge.G, _tokens.Edge.B, _tokens.Edge.A * opacity),
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = (int)_tokens.RadiusMedium,
            CornerRadiusTopRight = (int)_tokens.RadiusMedium,
            CornerRadiusBottomLeft = (int)_tokens.RadiusMedium,
            CornerRadiusBottomRight = (int)_tokens.RadiusMedium,
        };
    }
}
