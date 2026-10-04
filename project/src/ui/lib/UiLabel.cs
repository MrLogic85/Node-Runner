using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Native Label with a canonical typography and text color selected in the Inspector.</summary>
[Tool]
[GlobalClass]
public sealed partial class UiLabel : Label
{
    private UiTokens.Typography _textStyle = UiTokens.Typography.Body;
    private UiTokens.Color _textColor = UiTokens.Color.Ink;
    private Func<string>? _textSource;
    private AutoTranslateModeEnum _authoredTranslateMode;

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

    /// <summary>
    /// Text built in code that follows the language (#682): the label shows what this returns and
    /// asks again when the language changes. Its own auto-translation is off meanwhile, so the
    /// already translated text is not translated twice; null hands <c>Text</c> back to it.
    /// </summary>
    public Func<string>? TextSource
    {
        get => _textSource;
        set
        {
            if (_textSource is null && value is not null)
            {
                _authoredTranslateMode = AutoTranslateMode;
            }
            else if (_textSource is not null && value is null)
            {
                AutoTranslateMode = _authoredTranslateMode;
            }

            _textSource = value;
            if (value is not null)
            {
                AutoTranslateMode = AutoTranslateModeEnum.Disabled;
                Text = value();
            }
        }
    }

    public override void _EnterTree() => RequestReady();

    // A label with auto-translation off gets no translation notice when it enters the tree, so a
    // label that missed a language change while outside the tree asks its source here too.
    public override void _Notification(int what)
    {
        if (_textSource is not null && (what == NotificationTranslationChanged || what == NotificationEnterTree))
        {
            Text = _textSource();
        }
    }

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
