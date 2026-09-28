using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// The screen frame's card (reference <c>.frame</c>): the shared card border and glow over the
/// <c>bg</c> fill rather than a card's <c>panel</c>, so the toolbar, button bar and side panel
/// stand out on their own <c>panel</c>.
/// </summary>
[Tool]
public partial class UiFrameCard : UiCard
{
    protected override StyleBoxFlat CreateStyle()
    {
        var style = base.CreateStyle();
        style.BgColor = UiThemeLookup.Color(this, UiTokens.Color.Background);
        return style;
    }
}
