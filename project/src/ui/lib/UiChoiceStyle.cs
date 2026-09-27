using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Reference geometry and colors shared by toggle and checkbox rows.</summary>
public static class UiChoiceStyle
{
    public const float SwitchWidth = 44;
    public const float CheckboxWidth = 22;
    public const float ThumbInset = 5;
    public const float DisabledOpacity = 0.5f;

    public readonly record struct Colors(Color Background, Color Border, Color Mark);

    public static Vector2 IndicatorSize(bool isSwitch) =>
        new(isSwitch ? SwitchWidth : CheckboxWidth, UiSize.Control.ExtraSmall);

    public static Vector2 ThumbCenter(Rect2 track, bool on) =>
        new(on ? track.End.X - ThumbInset - UiSize.Icon.Default * 0.5f
            : track.Position.X + ThumbInset + UiSize.Icon.Default * 0.5f,
            track.GetCenter().Y);

    public static Colors Resolve(Control control, bool isSwitch, bool on) =>
        Resolve(
            isSwitch,
            on,
            UiThemeLookup.Color(control, UiTokens.Color.Accent),
            UiThemeLookup.Color(control, UiTokens.Color.LineStrong),
            UiThemeLookup.Color(control, UiTokens.Color.OnAccent),
            UiThemeLookup.Color(control, UiTokens.Color.Accent).WithAlpha(UiThemeLookup.Alpha(control, UiTokens.Alpha.Soft)));

    public static Colors Resolve(
        bool isSwitch,
        bool on,
        Color accent,
        Color lineStrong,
        Color onAccent,
        Color accentSoft) =>
        new(on
                ? isSwitch ? accentSoft : accent
                : Godot.Colors.Transparent,
            on ? accent : lineStrong,
            isSwitch
                ? on ? accent : lineStrong
                : onAccent);
}
