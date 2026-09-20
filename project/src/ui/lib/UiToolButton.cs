using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Touch-safe labelled tool action with an explicit active or locked state.</summary>
public partial class UiToolButton : Button
{
    private UiTokens _tokens = UiTokens.Neon;
    private string _toolLabel = "Tool";
    private string _iconText = "•";
    private bool _active;
    private bool _locked;
    private string _lockReason = "Unavailable";

    [Signal]
    public delegate void ToolActivatedEventHandler();

    [Export]
    public string ToolLabel
    {
        get => _toolLabel;
        set
        {
            _toolLabel = value;
            Refresh();
        }
    }

    [Export]
    public string IconText
    {
        get => _iconText;
        set
        {
            _iconText = value;
            Refresh();
        }
    }

    [Export]
    public bool Active
    {
        get => _active;
        set
        {
            _active = value;
            Refresh();
        }
    }

    [Export]
    public bool Locked
    {
        get => _locked;
        set
        {
            _locked = value;
            Refresh();
        }
    }

    [Export]
    public string LockReason
    {
        get => _lockReason;
        set
        {
            _lockReason = value;
            Refresh();
        }
    }

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            Refresh();
        }
    }

    public override void _Ready()
    {
        Pressed += OnPressed;
        Refresh();
    }

    private void OnPressed()
    {
        if (!Locked)
        {
            EmitSignal(SignalName.ToolActivated);
        }
    }

    private void Refresh()
    {
        if (!IsInsideTree())
        {
            return;
        }

        Disabled = Locked;
        Text = Locked ? $"{IconText}  {ToolLabel} · {LockReason}" : $"{IconText}  {ToolLabel}";
        TooltipText = Locked ? LockReason : ToolLabel;
        CustomMinimumSize = new Vector2(0, _tokens.TouchTarget);
        AddThemeFontSizeOverride("font_size", (int)_tokens.LabelFontSize);
        AddThemeColorOverride("font_color", Locked ? _tokens.Muted : (_active ? _tokens.OnAccent : _tokens.Ink));
        AddThemeColorOverride("font_hover_color", _tokens.OnAccent);
        AddThemeStyleboxOverride("normal", CreateStyle(_active, false));
        AddThemeStyleboxOverride("hover", CreateStyle(true, true));
        AddThemeStyleboxOverride("pressed", CreateStyle(true, true));
        AddThemeStyleboxOverride("focus", CreateStyle(true, true, 2));
        AddThemeStyleboxOverride("disabled", CreateStyle(false, false, 1, 0.5f));
    }

    private StyleBoxFlat CreateStyle(bool selected, bool focused, int borderWidth = 1, float opacity = 1)
    {
        var background = selected ? _tokens.Accent : _tokens.Panel;
        var border = selected ? _tokens.Accent : _tokens.Edge;
        return new StyleBoxFlat
        {
            BgColor = new Color(background.R, background.G, background.B, background.A * opacity),
            BorderColor = new Color(border.R, border.G, border.B, border.A * opacity),
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = (int)_tokens.Radius,
            CornerRadiusTopRight = (int)_tokens.Radius,
            CornerRadiusBottomLeft = (int)_tokens.Radius,
            CornerRadiusBottomRight = (int)_tokens.Radius,
        };
    }
}
