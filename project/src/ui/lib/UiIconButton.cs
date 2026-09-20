using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Touch-safe icon-labelled action with a stable square hit target.</summary>
public partial class UiIconButton : Button
{
    public enum IconRole
    {
        Custom,
        Back,
        Brain,
        Train,
        More,
        Add,
        Help,
        Trophy,
    }

    private UiTokens _tokens = UiTokens.Neon;
    private IconRole _role;

    private string _iconText = "•";

    [Export]
    public string IconText
    {
        get => _iconText;
        set
        {
            _iconText = value;
            _role = RoleFor(value);
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

    public override void _Draw()
    {
        base._Draw();
        if (_role == IconRole.Custom)
        {
            return;
        }

        DrawIcon(_role);
    }

    private void RefreshStyle()
    {
        if (!IsInsideTree())
        {
            return;
        }

        _role = RoleFor(IconText);
        Text = _role == IconRole.Custom ? IconText : string.Empty;
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
        QueueRedraw();
    }

    private void DrawIcon(IconRole role)
    {
        var color = Disabled ? _tokens.Muted : _tokens.Ink;
        var center = Size * 0.5f;
        const float stroke = 2f;
        switch (role)
        {
            case IconRole.Back:
                DrawLine(center + new Vector2(5, -9), center + new Vector2(-5, 0), color, stroke, antialiased: true);
                DrawLine(center + new Vector2(-5, 0), center + new Vector2(5, 9), color, stroke, antialiased: true);
                break;
            case IconRole.Brain:
                DrawArc(center, 8, 0, Mathf.Tau, 32, color, stroke, antialiased: true);
                DrawCircle(center + new Vector2(-4, -2), 2.2f, color);
                DrawCircle(center + new Vector2(4, -2), 2.2f, color);
                DrawLine(center + new Vector2(-3, 4), center + new Vector2(3, 4), color, stroke, antialiased: true);
                break;
            case IconRole.Train:
                DrawColoredPolygon(
                    [
                        center + new Vector2(-5, -9),
                        center + new Vector2(9, 0),
                        center + new Vector2(-5, 9),
                    ],
                    color);
                break;
            case IconRole.More:
                DrawCircle(center + new Vector2(-8, 0), 2.4f, color);
                DrawCircle(center, 2.4f, color);
                DrawCircle(center + new Vector2(8, 0), 2.4f, color);
                break;
            case IconRole.Add:
                DrawLine(center + new Vector2(-8, 0), center + new Vector2(8, 0), color, stroke, antialiased: true);
                DrawLine(center + new Vector2(0, -8), center + new Vector2(0, 8), color, stroke, antialiased: true);
                break;
            case IconRole.Help:
                DrawArc(center + new Vector2(0, -3), 6, Mathf.Pi, Mathf.Tau * 0.95f, 24, color, stroke, antialiased: true);
                DrawLine(center + new Vector2(4, 2), center + new Vector2(0, 6), color, stroke, antialiased: true);
                DrawCircle(center + new Vector2(0, 11), 1.8f, color);
                break;
            case IconRole.Trophy:
                DrawArc(center + new Vector2(0, -5), 8, 0, Mathf.Pi, 24, color, stroke, antialiased: true);
                DrawLine(center + new Vector2(-8, -5), center + new Vector2(-5, 4), color, stroke, antialiased: true);
                DrawLine(center + new Vector2(8, -5), center + new Vector2(5, 4), color, stroke, antialiased: true);
                DrawLine(center + new Vector2(-4, 6), center + new Vector2(4, 6), color, stroke, antialiased: true);
                DrawLine(center + new Vector2(0, 6), center + new Vector2(0, 12), color, stroke, antialiased: true);
                DrawLine(center + new Vector2(-7, 12), center + new Vector2(7, 12), color, stroke, antialiased: true);
                DrawArc(center + new Vector2(-10, -3), 5, -Mathf.Pi / 2, Mathf.Pi / 2, 16, color, stroke, antialiased: true);
                DrawArc(center + new Vector2(10, -3), 5, Mathf.Pi / 2, Mathf.Pi * 1.5f, 16, color, stroke, antialiased: true);
                break;
        }
    }

    private static IconRole RoleFor(string iconText) =>
        iconText switch
        {
            UiIconGlyphs.Back => IconRole.Back,
            UiIconGlyphs.Brain => IconRole.Brain,
            UiIconGlyphs.Train => IconRole.Train,
            UiIconGlyphs.More => IconRole.More,
            UiIconGlyphs.Trophy => IconRole.Trophy,
            "+" => IconRole.Add,
            "?" => IconRole.Help,
            "..." => IconRole.More,
            "<" => IconRole.Back,
            ">" => IconRole.Train,
            _ => IconRole.Custom,
        };

    private StyleBoxFlat CreateStyle(bool focused, int borderWidth = 1, float opacity = 1)
    {
        var background = focused ? _tokens.AccentSoft : _tokens.PanelRaised;
        var border = focused ? _tokens.Accent : _tokens.LineStrong;
        return _tokens.ControlStyle(
            UiTokens.MultiplyAlpha(background, opacity),
            UiTokens.MultiplyAlpha(border, opacity),
            borderWidth);
    }
}
