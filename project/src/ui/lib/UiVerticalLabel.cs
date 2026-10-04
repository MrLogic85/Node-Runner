using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// One line of text turned a quarter turn clockwise, so it reads top to bottom like CSS
/// <c>writing-mode: vertical-rl</c>. It draws the text itself because a Container resets a
/// child's rotation, so a rotated <see cref="Label"/> cannot sit in one. Like a Label it translates
/// <c>Text</c> by its auto-translate mode and context, and cases it after translating.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiVerticalLabel : Control
{
    private const float _quarterTurn = Mathf.Pi / 2;

    private string _text = "";
    private UiTokens.Typography _textStyle = UiTokens.Typography.Label;
    private UiTokens.Color _textColor = UiTokens.Color.Muted;

    [Export]
    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            Refresh();
        }
    }

    [Export]
    public UiTokens.Typography TextStyle
    {
        get => _textStyle;
        set
        {
            _textStyle = value;
            Refresh();
        }
    }

    [Export]
    public UiTokens.Color TextColor
    {
        get => _textColor;
        set
        {
            _textColor = value;
            Refresh();
        }
    }

    private string DisplayText => UiThemeLookup.LetterCase(Atr(Text), TextStyle);

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged || what == NotificationTranslationChanged)
        {
            Refresh();
        }
    }

    public override Vector2 _GetMinimumSize()
    {
        var line = LineSize();
        return new Vector2(line.Y, line.X);
    }

    public override void _Draw()
    {
        if (Text.Length == 0)
        {
            return;
        }

        var variation = UiTokens.Variation(TextStyle);
        var font = GetThemeFont("font", variation);
        var line = LineSize();
        DrawSetTransform(new Vector2((Size.X + line.Y) / 2, (Size.Y - line.X) / 2), _quarterTurn);
        DrawString(
            font,
            new Vector2(0, font.GetAscent(UiThemeLookup.FontSize(this, TextStyle))),
            DisplayText,
            fontSize: UiThemeLookup.FontSize(this, TextStyle),
            modulate: UiThemeLookup.Color(this, TextColor));
    }

    private Vector2 LineSize()
    {
        if (Text.Length == 0)
        {
            return Vector2.Zero;
        }

        var font = GetThemeFont("font", UiTokens.Variation(TextStyle));
        return font.GetStringSize(DisplayText, fontSize: UiThemeLookup.FontSize(this, TextStyle));
    }

    private void Refresh()
    {
        UpdateMinimumSize();
        QueueRedraw();
    }
}
