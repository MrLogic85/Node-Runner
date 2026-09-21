namespace NodeRunner.Ui.Lib;

/// <summary>Obsolete compatibility aliases for product callers not yet migrated to typed icon identities.</summary>
public static class UiIconGlyphs
{
    public const string Back = "back";
    public const string Brain = "brain";
    public const string Model = "model";
    public const string Train = "play";
    public const string More = "more";
    public const string Trophy = "trophy";

    public static bool TryParse(string value, out UiIconId icon)
    {
        icon = value switch
        {
            "back" or "‹" or "<" => UiIconId.Back,
            "beam" or "link" or "⛓" => UiIconId.Beam,
            "brain" or "model" or "◎" => UiIconId.Model,
            "build" or "✎" => UiIconId.Build,
            "check" or "✓" => UiIconId.Check,
            "close" or "x" or "×" => UiIconId.Close,
            "copy" => UiIconId.Copy,
            "core" => UiIconId.Core,
            "gear" or "settings" => UiIconId.Gear,
            "joint" or "◌" => UiIconId.Joint,
            "lock" or "locked" or "🔒" => UiIconId.Lock,
            "more" or "..." or "⋯" => UiIconId.More,
            "move" => UiIconId.Move,
            "padlock" => UiIconId.Lock,
            "play" or "▶" or ">" => UiIconId.Play,
            "plus" or "+" => UiIconId.Plus,
            "rotate" => UiIconId.Rotate,
            "select" or "▣" => UiIconId.Select,
            "trash" or "delete" => UiIconId.Trash,
            "trophy" => UiIconId.Trophy,
            "unlock" => UiIconId.Unlock,
            "warn" or "!" or "⚠" or "?" => UiIconId.Warn,
            _ => default,
        };

        return value is
            "back" or "‹" or "<" or
            "beam" or "link" or "⛓" or
            "brain" or "model" or "◎" or
            "build" or "✎" or
            "check" or "✓" or
            "close" or "x" or "×" or
            "copy" or "core" or
            "gear" or "settings" or
            "joint" or "◌" or
            "lock" or "locked" or "🔒" or
            "more" or "..." or "⋯" or
            "move" or "padlock" or
            "play" or "▶" or ">" or
            "plus" or "+" or
            "rotate" or
            "select" or "▣" or
            "trash" or "delete" or "trophy" or "unlock" or
            "warn" or "!" or "⚠" or "?";
    }

    public static UiIconId ParseOr(string value, UiIconId fallback) =>
        TryParse(value, out var icon) ? icon : fallback;
}
