using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Native Label with a canonical typography and text color selected in the Inspector.</summary>
[Tool]
[GlobalClass]
public sealed partial class UiLabel : Label
{
    private UiTokens.Typography _textStyle = UiTokens.Typography.Body;
    private UiTokens.Color _textColor = UiTokens.Color.Ink;

    [Export]
    public UiTokens.Typography TextStyle
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
            ApplyStyle();
        }
    }

    [Export]
    public UiTokens.Color TextColor
    {
        get => _textColor;
        set
        {
            if (!UiTokens.IsTextColor(value))
            {
                GD.PushError($"Invalid label text color: {value}. Keeping {_textColor}.");
                return;
            }

            _textColor = value;
            ApplyStyle();
        }
    }

    public override void _EnterTree() => RequestReady();

    public override void _Ready() => ApplyStyle();

    // Both values are theme-independent names; the inherited Theme resolves the font and color.
    private void ApplyStyle()
    {
        var variation = UiTokens.Variation(TextStyle, TextColor);
        if (ThemeTypeVariation != variation)
        {
            ThemeTypeVariation = variation;
        }

        Uppercase = UiTokens.IsUppercase(TextStyle);
    }

    private static readonly HashSet<StringName> _derivedProperties =
    [
        Control.PropertyName.Theme,
        Control.PropertyName.ThemeTypeVariation,
        Label.PropertyName.LabelSettings,
        Label.PropertyName.Uppercase,
    ];

    // Hides properties that TextStyle/TextColor own from the Inspector and from saved scenes.
    // Godot does not validate the dynamic theme_override_* properties, so baked colors and
    // fonts are caught by the scene guard in NodeRunner.Ui.Tests instead.
    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        var name = property["name"].AsStringName();

        if (_derivedProperties.Contains(name))
        {
            var usage = (PropertyUsageFlags)property["usage"].AsInt64();
            property["usage"] = (long)(usage & ~(PropertyUsageFlags.Editor | PropertyUsageFlags.Storage));
        }
    }
}
