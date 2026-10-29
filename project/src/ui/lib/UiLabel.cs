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
    private Func<string>? _widthSource;
    private AutoTranslateModeEnum _authoredTranslateMode;
    private Label? _baselineOf;
    private readonly UiUnsavedState _unsaved = new("custom_minimum_size", "size_flags_vertical", "vertical_alignment");

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
    /// Text built in code that follows the language: the label shows what this returns and
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

    /// <summary>
    /// Text built in code whose width the label keeps as its least, such as the longest number it
    /// shows, so a changing number does not resize it. Each digit counts as the font's widest
    /// digit. It follows the language and the theme as <see cref="TextSource"/> does. While set the
    /// label owns its minimum width; null hands it back at 0.
    /// </summary>
    public Func<string>? WidthSource
    {
        get => _widthSource;
        set
        {
            _widthSource = value;
            if (value is null)
            {
                CustomMinimumSize = CustomMinimumSize with { X = 0 };
            }

            ApplyWidth();
        }
    }

    /// <summary>
    /// A label beside this one whose baseline this one stands on, for a scene: as
    /// <see cref="UiBaseline"/> does for a row built in code, this label starts at the top of its
    /// row, as tall as the other label's top margin and ascent plus its own descent and bottom
    /// margin, and draws its text at the bottom.
    /// The other label must start at the row's top with its text at the top, as a Label does by
    /// default, and come before this one in the tree, so a theme change reaches it first. While set
    /// the label owns its minimum size, vertical size flags and vertical alignment; clearing it
    /// hands them back at their defaults.
    /// </summary>
    [Export]
    public Label? BaselineOf
    {
        get => _baselineOf;
        set
        {
            if (_baselineOf is not null && value is null)
            {
                UiBaseline.Release(this);
            }

            _baselineOf = value;
            ApplyBaseline();
        }
    }

    public override void _EnterTree() => RequestReady();

    // A label with auto-translation off gets no translation notice when it enters the tree, so a
    // label that missed a language change while outside the tree asks its source here too.
    public override void _Notification(int what)
    {
        if (_baselineOf is not null && _unsaved.Handle(this, what, ApplyBaseline))
        {
            return;
        }

        if (what == NotificationThemeChanged)
        {
            ApplyBaseline();
            ApplyWidth();
        }

        if (what == NotificationTranslationChanged || what == NotificationEnterTree)
        {
            if (_textSource is not null)
            {
                Text = _textSource();
            }

            ApplyWidth();
        }
    }

    public override void _Ready()
    {
        ApplyStyle();
        ApplyBaseline();
    }

    private void ApplyBaseline()
    {
        if (_baselineOf is null || !IsInstanceValid(_baselineOf) || !IsInsideTree())
        {
            return;
        }

        UiBaseline.Stand(this, UiBaseline.ExtentOf(_baselineOf).Above + UiBaseline.ExtentOf(this).Below);
    }

    private void ApplyWidth()
    {
        if (_widthSource is null || !IsInsideTree())
        {
            return;
        }

        var font = GetThemeFont("font");
        var fontSize = GetThemeFontSize("font_size");
        var widestDigit = "0123456789".MaxBy(digit => font.GetStringSize(digit.ToString(), fontSize: fontSize).X);
        var text = string.Concat(_widthSource().Select(c => char.IsAsciiDigit(c) ? widestDigit : c));
        text = Uppercase ? UiThemeLookup.Uppercase(text) : text;
        var width = font.GetStringSize(text, fontSize: fontSize).X + GetThemeStylebox("normal").GetMinimumSize().X;
        CustomMinimumSize = CustomMinimumSize with { X = Mathf.Ceil(width) };
    }

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
