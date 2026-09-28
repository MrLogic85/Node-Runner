using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// The dialog and notification frame (reference <c>.pnl.dialog</c>/<c>.pnl.toast</c>): a card whose
/// border and glow take the severity colour, accent for Default, halo for Warn and danger for
/// Danger. It is not a card variant, since the owner picks it from the popup type, not the screen.
/// </summary>
[Tool]
public partial class UiPopupCard : UiCard
{
    private UiPopupType _popupType;

    public UiPopupType PopupType
    {
        get => _popupType;
        set
        {
            _popupType = value;
            RefreshStyle();
        }
    }

    protected override StyleBoxFlat CreateStyle()
    {
        var style = base.CreateStyle();
        style.BorderColor = UiPopupStyle.SemanticColor(PopupType, this);
        UiGlow.ApplyToControl(style, style.BorderColor, UiThemeLookup.EffectsEnabled(this));
        return style;
    }
}
