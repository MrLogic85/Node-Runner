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

    private bool _showLockReasonInText = true;

    [Export]
    public bool ShowLockReasonInText
    {
        get => _showLockReasonInText;
        set
        {
            _showLockReasonInText = value;
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
    private UiIconId? _iconId;

    /// <summary>Optional canonical SVG displayed before the action label.</summary>
    public UiIconId? IconId
    {
        get => _iconId;
        set
        {
            _iconId = value;
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
        Tokens.ApplyTextStyle(this, Tokens.LabelText);
        var textColor = Kind == ActionKind.Primary ? Tokens.OnAccent : Kind == ActionKind.Danger ? Tokens.Danger : Tokens.Ink;
        AddThemeColorOverride("font_color", textColor);
        AddThemeColorOverride("font_hover_color", textColor);
        AddThemeColorOverride("font_pressed_color", textColor);
        AddThemeColorOverride("font_disabled_color", Tokens.Muted);
        if (IconId is { } icon)
        {
            UiIcons.Apply(this, icon, UiIconSize.Standard, textColor);
        }
        else
        {
            Icon = null;
        }

        if (!string.IsNullOrWhiteSpace(LabelText))
        {
            Text = DisplayText();
        }
        AddThemeStyleboxOverride("normal", CreateStyle(false));
        AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        AddThemeStyleboxOverride("hover", CreateStyle(true));
        AddThemeStyleboxOverride("pressed", CreateStyle(true));
        AddThemeStyleboxOverride("disabled", CreateStyle(false, 0.5f));
        if (Locked && string.IsNullOrWhiteSpace(TooltipText))
        {
            TooltipText = LockReason;
        }
    }

    private string DisplayText()
    {
        var text = Locked && ShowLockReasonInText ? $"{LabelText} · {LockReason}" : LabelText;
        return text.ToUpperInvariant();
    }

    private StyleBoxFlat CreateStyle(bool focused, float opacity = 1)
    {
        var color = Kind == ActionKind.Danger ? Tokens.Danger : Tokens.Accent;
        var opacityMultiplier = Locked ? 0.5f : 1f;
        var background = Kind == ActionKind.Primary
            ? UiTokens.WithAlpha(color, opacity * opacityMultiplier)
            : focused
                ? UiTokens.MultiplyAlpha(Kind == ActionKind.Danger ? UiTokens.WithAlpha(Tokens.Danger, Tokens.AccentSoft.A) : Tokens.AccentSoft, opacity * opacityMultiplier)
                : UiTokens.MultiplyAlpha(Tokens.PanelRaised, opacity * opacityMultiplier);
        var border = Kind == ActionKind.Primary
            ? UiTokens.WithAlpha(color, opacity * opacityMultiplier)
            : UiTokens.WithAlpha(Kind == ActionKind.Danger ? Tokens.Danger : Tokens.LineStrong, opacity * opacityMultiplier);
        return Tokens.ControlStyle(
            background,
            border,
            glow: Kind == ActionKind.Primary && opacityMultiplier > 0.99f,
            horizontalPadding: UiSpacing.ControlHorizontalPadding(Tokens),
            verticalPadding: UiSpacing.ControlVerticalPadding(Tokens));
    }

}
