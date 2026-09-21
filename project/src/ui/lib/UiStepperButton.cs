using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Exact-value minus/plus button used next to sliders.</summary>
public partial class UiStepperButton : Button
{
    private UiTokens _tokens = UiTokens.Neon;
    private UiComponentContracts.StepperSymbol _symbol;

    [Export]
    public UiComponentContracts.StepperSymbol Symbol
    {
        get => _symbol;
        set
        {
            _symbol = value;
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

    public override void _Ready() => Refresh();

    private void Refresh()
    {
        if (!IsInsideTree())
        {
            return;
        }

        Text = Symbol == UiComponentContracts.StepperSymbol.Minus ? "−" : "+";
        CustomMinimumSize = new Vector2(_tokens.TouchTarget, _tokens.TouchTarget);
        _tokens.ApplyTextStyle(this, _tokens.HeadingText);
        AddThemeColorOverride("font_color", Disabled ? _tokens.Muted : _tokens.Ink);
        AddThemeColorOverride("font_hover_color", _tokens.Accent);
        AddThemeStyleboxOverride("normal", CreateStyle(false));
        AddThemeStyleboxOverride("hover", CreateStyle(true));
        AddThemeStyleboxOverride("pressed", CreateStyle(true));
        AddThemeStyleboxOverride("focus", _tokens.FocusRingStyle());
        AddThemeStyleboxOverride("disabled", CreateStyle(false, 0.5f));
    }

    private StyleBoxFlat CreateStyle(bool active, float opacity = 1) =>
        _tokens.ControlStyle(
            UiTokens.MultiplyAlpha(active ? _tokens.AccentSoft : _tokens.PanelRaised, opacity),
            UiTokens.MultiplyAlpha(active ? _tokens.Accent : _tokens.LineStrong, opacity),
            active ? _tokens.StrokeSignal : _tokens.StrokeHair);
}
