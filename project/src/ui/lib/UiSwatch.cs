using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// One colour token, placed in a scene: a rounded square filled with the token as the inherited
/// Theme resolves it, outlined in line-strong. It draws itself, so it follows a theme swap.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiSwatch : Control
{
    private UiTokens.Color _token = UiTokens.Color.Accent;

    public UiSwatch() => MouseFilter = MouseFilterEnum.Ignore;

    [Export]
    public UiTokens.Color Token
    {
        get => _token;
        set
        {
            _token = value;
            QueueRedraw();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged)
        {
            QueueRedraw();
        }
    }

    public override Vector2 _GetMinimumSize() => Vector2.One * UiSize.Control.Default;

    public override void _Draw()
    {
        var side = Mathf.Min(Size.X, Size.Y);
        var rect = new Rect2((Size - (Vector2.One * side)) / 2, Vector2.One * side);
        DrawStyleBox(
            UiThemeLookup.CreateStyleBox(
                UiThemeLookup.Color(this, Token), UiThemeLookup.Color(this, UiTokens.Color.LineStrong)),
            rect);
    }
}
