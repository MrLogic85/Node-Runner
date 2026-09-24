using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Content-sized, wrapping note text with spacing owned by its parent.</summary>
public partial class UiNoteRow : VBoxContainer
{
    private UiTokens _tokens = UiTokens.Neon;
    private readonly Label _label = new()
    {
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
        MouseFilter = MouseFilterEnum.Ignore,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };

    private string _text = "Sits on an empty joint.";

    [Export]
    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            Refresh();
        }
    }

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            Refresh();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_label);
        Refresh();
    }

    private void Refresh()
    {
        if (!IsInsideTree())
        {
            return;
        }

        _label.Text = Text;
        _tokens.ApplyTextStyle(_label, _tokens.NoteText);
        _label.AddThemeColorOverride("font_color", _tokens.Muted);
    }
}
