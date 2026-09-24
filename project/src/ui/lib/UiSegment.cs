using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Text and optional canonical icon for one segmented choice.</summary>
[Tool]
[GlobalClass]
public partial class UiSegment : Resource
{
    private string _text = string.Empty;
    private UiIconId _iconId = UiIconId.None;

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
}
