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

    public static Vector2 IndicatorSize(UiTokens tokens, bool isSwitch) =>
        new(isSwitch ? SwitchWidth : CheckboxWidth, tokens.ControlExtraSmall);

    public static Vector2 ThumbCenter(UiTokens tokens, Rect2 track, bool on) =>
        new(on ? track.End.X - ThumbInset - tokens.Icon * 0.5f
            : track.Position.X + ThumbInset + tokens.Icon * 0.5f,
            track.GetCenter().Y);

    public static Colors Resolve(UiTokens tokens, bool isSwitch, bool on) =>
        new(on ? (isSwitch ? tokens.AccentSoft : tokens.Accent) : Godot.Colors.Transparent,
            on ? tokens.Accent : tokens.LineStrong,
            isSwitch ? (on ? tokens.Accent : tokens.LineStrong) : tokens.OnAccent);
}
