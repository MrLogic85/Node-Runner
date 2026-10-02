namespace NodeRunner.Ui.Lib;

/// <summary>
/// Typed identifiers for the values a Godot Theme supplies at runtime: the things that
/// actually change between Neon and Paper. Theme-independent dimensions
/// are constants in <see cref="UiSize"/> and <see cref="UiLayout"/>.
/// </summary>
public static class UiTokens
{
    public enum Color
    {
        Transparent,
        Background,
        Panel,
        PanelRaised,
        Line,
        LineStrong,
        Edge,
        Ink,
        Muted,
        Accent,
        Halo,
        OnAccent,
        Danger,
        Scrim,
        Output,
    }

    public enum Flag
    {
        EffectsEnabled,
    }

    /// <summary>
    /// Palette-specific opacities. A translucent fill is a base color at one of these alphas,
    /// e.g. <c>Accent.WithAlpha(Soft)</c>, not a separate palette color.
    /// </summary>
    public enum Alpha
    {
        /// <summary>Soft fill for selected, pressed and hovered backgrounds.</summary>
        Soft,

        /// <summary>Every shadow in Training except the followed one (#385).</summary>
        Shadow,
    }

    public enum Typography
    {
        Title,
        Heading,
        Subheading,
        Stage,
        Body,
        BodyStrong,
        Small,
        SmallStrong,
        Label,
        Note,
        NoteStrong,
        Caption,
        Overline,
        ReadoutLarge,
        Readout,
        ReadoutMedium,
        ReadoutSmall,
    }

    public static class Size
    {
        public enum Stroke
        {
            Hair,
            Signal,
            Beam,
            ButtonSelected,
            SelectionHandle,
            Number
        }
    }

    public static string Name(Color token) => token switch
    {
        Color.Transparent => "transparent",
        Color.Background => "background",
        Color.Panel => "panel",
        Color.PanelRaised => "panel_raised",
        Color.Line => "line",
        Color.LineStrong => "line_strong",
        Color.Edge => "edge",
        Color.Ink => "ink",
        Color.Muted => "muted",
        Color.Accent => "accent",
        Color.Halo => "halo",
        Color.OnAccent => "on_accent",
        Color.Danger => "danger",
        Color.Scrim => "scrim",
        Color.Output => "output",
        _ => throw new ArgumentOutOfRangeException(nameof(token), token, null),
    };

    public static string Name(Flag token) => token switch
    {
        Flag.EffectsEnabled => "effects_enabled",
        _ => throw new ArgumentOutOfRangeException(nameof(token), token, null),
    };

    public static string Name(Alpha token) => token switch
    {
        Alpha.Soft => "alpha_soft",
        Alpha.Shadow => "alpha_shadow",
        _ => throw new ArgumentOutOfRangeException(nameof(token), token, null),
    };

    public static string Variation(Typography token) => token switch
    {
        Typography.Title => "UiTitle",
        Typography.Heading => "UiHeading",
        Typography.Subheading => "UiSubheading",
        Typography.Stage => "UiStage",
        Typography.Body => "UiBody",
        Typography.BodyStrong => "UiBodyStrong",
        Typography.Small => "UiSmall",
        Typography.SmallStrong => "UiSmallStrong",
        Typography.Label => "UiLabel",
        Typography.Note => "UiNote",
        Typography.NoteStrong => "UiNoteStrong",
        Typography.Caption => "UiCaption",
        Typography.Overline => "UiOverline",
        Typography.ReadoutLarge => "UiReadoutLarge",
        Typography.Readout => "UiReadout",
        Typography.ReadoutMedium => "UiReadoutMedium",
        Typography.ReadoutSmall => "UiReadoutSmall",
        _ => throw new ArgumentOutOfRangeException(nameof(token), token, null),
    };

    /// <summary>Colors a text variation can combine with a typography style.</summary>
    public static bool IsTextColor(Color color) =>
        color is Color.Ink or Color.Muted or Color.Accent or Color.Halo or Color.OnAccent
            or Color.Danger or Color.Output;

    /// <summary>
    /// Letter case is a fixed property of the typography, not of the palette, so it maps
    /// straight to <c>Label.Uppercase</c> instead of travelling through the Theme.
    /// </summary>
    public static bool IsUppercase(Typography typography) =>
        typography is Typography.Stage or Typography.Label or Typography.Overline;

    /// <summary>
    /// A control has a single type variation, so each typography × text color pair is its own
    /// generated variation chained onto the typography variation, e.g. <c>UiNoteMuted</c>.
    /// </summary>
    public static string Variation(Typography typography, Color color)
    {
        if (!IsTextColor(color))
        {
            throw new ArgumentOutOfRangeException(nameof(color), color, "Not a text color.");
        }

        return Variation(typography) + color;
    }
}
