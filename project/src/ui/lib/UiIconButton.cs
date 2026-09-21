using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Touch-safe canonical SVG icon action with a stable square hit target.</summary>
public partial class UiIconButton : Button
{
    private UiTokens _tokens = UiTokens.Neon;
    private UiIconId _iconId = UiIconId.More;
    private string _iconText = "more";
    private bool _accentRole;
    private bool _dangerRole;

    [Export]
    public string IconText
    {
        get => _iconText;
        set
        {
            _iconText = value;
            if (UiIconGlyphs.TryParse(value, out var icon))
            {
                _iconId = icon;
            }
            else
            {
                GD.PushError($"UiIconButton.IconText '{value}' is not a canonical icon. Use IconId.");
            }
            RefreshStyle();
        }
    }

    [Export]
    public UiIconId IconId
    {
        get => _iconId;
        set
        {
            _iconId = value;
            _iconText = value.ToString();
            RefreshStyle();
        }
    }

    [Export]
    public UiIconSize IconSize { get; set; } = UiIconSize.Large;

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

    [Export]
    public bool AccentRole
    {
        get => _accentRole;
        set
        {
            _accentRole = value;
            RefreshStyle();
        }
    }

    [Export]
    public bool DangerRole
    {
        get => _dangerRole;
        set
        {
            _dangerRole = value;
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

        Text = string.Empty;
        TooltipText = AccessibleLabel;
        CustomMinimumSize = new Vector2(_tokens.TouchTarget, _tokens.TouchTarget);
        _tokens.ApplyTextStyle(this, _tokens.HeadingText);
        var foreground = Disabled ? _tokens.Muted : DangerRole ? _tokens.Danger : AccentRole ? _tokens.Accent : _tokens.Ink;
        AddThemeColorOverride("font_color", foreground);
        AddThemeColorOverride("font_hover_color", DangerRole ? _tokens.Danger : _tokens.Accent);
        AddThemeColorOverride("font_pressed_color", _tokens.OnAccent);
        UiIcons.Apply(this, IconId, IconSize, foreground);
        AddThemeStyleboxOverride("normal", CreateStyle(false));
        AddThemeStyleboxOverride("hover", CreateStyle(true));
        AddThemeStyleboxOverride("pressed", CreateStyle(true));
        AddThemeStyleboxOverride("focus", _tokens.FocusRingStyle());
        AddThemeStyleboxOverride("disabled", CreateStyle(false, 1, 0.5f));
    }

    private StyleBoxFlat CreateStyle(bool focused, int borderWidth = 1, float opacity = 1)
    {
        var roleColor = DangerRole ? _tokens.Danger : AccentRole ? _tokens.Accent : _tokens.LineStrong;
        var background = focused ? (DangerRole ? UiTokens.WithAlpha(_tokens.Danger, 0.18f) : _tokens.AccentSoft) : _tokens.PanelRaised;
        var border = focused ? (DangerRole ? _tokens.Danger : _tokens.Accent) : roleColor;
        return _tokens.ControlStyle(
            UiTokens.MultiplyAlpha(background, opacity),
            UiTokens.MultiplyAlpha(border, opacity),
            borderWidth,
            horizontalPadding: UiSpacing.ControlHorizontalPadding(_tokens),
            verticalPadding: UiSpacing.ControlVerticalPadding(_tokens));
    }
}
