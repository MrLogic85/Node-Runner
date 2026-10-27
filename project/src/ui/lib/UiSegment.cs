using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Text and optional canonical icon for one segmented choice.</summary>
[Tool]
[GlobalClass]
public partial class UiSegment : Resource
{
    private string _text = string.Empty;
    private UiIconId _iconId = UiIconId.None;
    private bool _disabled;
    private Func<string>? _textSource;

    [Export]
    public string Text
    {
        get => _text;
        set
        {
            if (_text == value)
                return;
            _text = value;
            EmitChanged();
        }
    }

    /// <summary>
    /// Text already in the player's language, used instead of <see cref="Text"/> when set; the
    /// switch asks again when the language changes.
    /// </summary>
    public Func<string>? TextSource
    {
        get => _textSource;
        set
        {
            _textSource = value;
            EmitChanged();
        }
    }

    [Export]
    public UiIconId IconId
    {
        get => _iconId;
        set
        {
            if (!Enum.IsDefined(value))
            {
                GD.PushError($"Invalid segment icon: {value}. Keeping {_iconId}.");
                return;
            }
            if (_iconId == value)
                return;
            _iconId = value;
            EmitChanged();
        }
    }

    /// <summary>Shown but not choosable, e.g. a mode that comes in a later version.</summary>
    [Export]
    public bool Disabled
    {
        get => _disabled;
        set
        {
            if (_disabled == value)
                return;
            _disabled = value;
            EmitChanged();
        }
    }
}
