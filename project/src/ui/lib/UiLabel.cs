using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Native Label with a canonical typography preset selected in the Inspector.</summary>
[Tool]
[GlobalClass]
public sealed partial class UiLabel : Label
{
    public enum TextPreset
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

    private TextPreset _textStyle = TextPreset.Body;
    private UiTokens _tokens = UiTokens.Neon;

    [Export]
    public TextPreset TextStyle
    {
        get => _textStyle;
        set
        {
            if (!Enum.IsDefined(value))
            {
                GD.PushError($"Invalid label text style: {value}. Keeping {_textStyle}.");
                return;
            }
            _textStyle = value;
            ApplyTypography();
        }
    }

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _tokens = value;
            ApplyTypography();
        }
    }

    public override void _EnterTree() => RequestReady();

    public override void _Ready() => ApplyTypography();

    private void ApplyTypography()
    {
        if (!IsNodeReady() || !IsInsideTree())
        {
            return;
        }

        var style = ResolveStyle(TextStyle, Tokens);
        // A private derived theme preserves native color inheritance without serialized font overrides.
        var typography = new Godot.Theme();
        if (UiTokens.CreateTextFont(style) is { } font)
        {
            typography.SetFont("font", "Label", font);
        }
        typography.SetFontSize("font_size", "Label", (int)style.FontSize);
        typography.SetConstant("line_spacing", "Label", 0);
        LabelSettings = null;
        RemoveThemeFontOverride("font");
        RemoveThemeFontSizeOverride("font_size");
        RemoveThemeConstantOverride("line_spacing");
        Theme = typography;
        Uppercase = style.Uppercase;
    }

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        var name = property["name"].AsString();
        if (name is "theme" or "label_settings" or "uppercase" or "resize_font_to_fit"
            or "minimum_font_size" or "maximum_font_size")
        {
            // Typography is derived from TextStyle; do not expose or serialize competing values.
            var usage = (PropertyUsageFlags)property["usage"].AsInt64();
            property["usage"] = (long)(usage & ~(PropertyUsageFlags.Editor | PropertyUsageFlags.Storage));
        }
    }

    public static UiTokens.TextStyle ResolveStyle(TextPreset preset, UiTokens tokens) => preset switch
    {
        TextPreset.Title => tokens.TitleText,
        TextPreset.Heading => tokens.HeadingText,
        TextPreset.Subheading => tokens.SubheadingText,
        TextPreset.Stage => tokens.StageText,
        TextPreset.Body => tokens.BodyText,
        TextPreset.BodyStrong => tokens.BodyStrongText,
        TextPreset.Small => tokens.SmallText,
        TextPreset.SmallStrong => tokens.SmallStrongText,
        TextPreset.Label => tokens.LabelText,
        TextPreset.Note => tokens.NoteText,
        TextPreset.NoteStrong => tokens.NoteStrongText,
        TextPreset.Caption => tokens.CaptionText,
        TextPreset.Overline => tokens.OverlineText,
        TextPreset.ReadoutLarge => tokens.ReadoutLargeText,
        TextPreset.Readout => tokens.ReadoutText,
        TextPreset.ReadoutMedium => tokens.ReadoutMediumText,
        TextPreset.ReadoutSmall => tokens.ReadoutSmallText,
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown text style."),
    };
}
