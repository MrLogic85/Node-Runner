using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Reference button bar (<c>ComponentToolbars</c>): vertical, down the left edge, with a divider
/// on its right edge, filled with <c>panel</c>. It only lays out: the screen authors the buttons in <c>%ButtonBarContent</c>
/// and decides what they do, including which is selected or locked. Its width, padding and
/// separation are the scene's own (#331); the bar never grows wider than its minimum width.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiButtonBar : MarginContainer
{
    private const float _unbounded = -1;

    public override Vector2 _GetMaximumSize() => new(CustomMinimumSize.X, _unbounded);

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged)
        {
            QueueRedraw();
        }
    }

    public override void _Draw() =>
        DrawRect(new Rect2(Vector2.Zero, Size), UiThemeLookup.Color(this, UiTokens.Color.Panel));
}
