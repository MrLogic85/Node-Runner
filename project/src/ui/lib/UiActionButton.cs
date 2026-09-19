using Godot;
namespace NodeRunner.Ui.Lib;

/// <summary>Touch-safe labelled action with explicit visual state.</summary>
public partial class UiActionButton : Button
{
    public enum ActionKind
    {
        Primary,
        Secondary,
        Danger,
    }

    private ActionKind _kind = ActionKind.Secondary;

    [Export]
    public ActionKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            RefreshStyle();
        }
    }

    private bool _locked;

    [Export]
    public bool Locked
    {
        get => _locked;
        set
        {
            _locked = value;
            RefreshStyle();
        }
    }

    private string _lockReason = "Unavailable";

    [Export]
    public string LockReason
    {
        get => _lockReason;
        set
        {
            _lockReason = value;
            RefreshStyle();
        }
    }

    private string _labelText = string.Empty;

    [Export]
    public string LabelText
    {
        get => _labelText;
        set
        {
            _labelText = value;
            RefreshStyle();
        }
    }

    private UiTokens _tokens = UiTokens.Neon;

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
        RefreshStyle();
    }

    private void RefreshStyle()
    {
        if (!IsInsideTree())
        {
            return;
        }

        CustomMinimumSize = new Vector2(0, Tokens.TouchTarget);
        Disabled = Locked;
        AddThemeFontSizeOverride("font_size", (int)Tokens.LabelFontSize);
        AddThemeColorOverride("font_color", Tokens.Ink);
        AddThemeColorOverride("font_hover_color", Tokens.Ink);
        AddThemeColorOverride("font_pressed_color", Tokens.Ink);
        AddThemeColorOverride("font_disabled_color", Tokens.Muted);
        if (!string.IsNullOrWhiteSpace(LabelText))
        {
            Text = Locked ? $"{LabelText} · {LockReason}" : LabelText;
        }
        AddThemeStyleboxOverride("normal", CreateStyle(false));
        AddThemeStyleboxOverride("focus", CreateStyle(true));
        AddThemeStyleboxOverride("hover", CreateStyle(true));
        AddThemeStyleboxOverride("pressed", CreateStyle(true));
        AddThemeStyleboxOverride("disabled", CreateStyle(false, 0.5f));
        if (Locked && string.IsNullOrWhiteSpace(TooltipText))
        {
            TooltipText = LockReason;
        }
    }

    private StyleBoxFlat CreateStyle(bool focused, float opacity = 1)
    {
        var color = Kind == ActionKind.Danger ? Tokens.Danger : Tokens.Accent;
        var opacityMultiplier = Locked ? 0.5f : 1f;
        return new StyleBoxFlat
        {
            BgColor = Kind == ActionKind.Primary
                ? new Color(color.R, color.G, color.B, 0.82f * opacity * opacityMultiplier)
                : new Color(color.R, color.G, color.B, focused ? 0.18f * opacity * opacityMultiplier : 0.08f * opacity * opacityMultiplier),
            BorderColor = new Color(color.R, color.G, color.B, opacity * opacityMultiplier),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = (int)Tokens.Radius,
            CornerRadiusTopRight = (int)Tokens.Radius,
            CornerRadiusBottomLeft = (int)Tokens.Radius,
            CornerRadiusBottomRight = (int)Tokens.Radius,
        };
    }
}
