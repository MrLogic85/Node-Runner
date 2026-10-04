using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Content-sized, wrapping note text with spacing owned by its parent.</summary>
[Tool]
[GlobalClass]
public partial class UiNoteRow : VBoxContainer
{
    private readonly Label _label = new()
    {
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
        MouseFilter = MouseFilterEnum.Ignore,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };

    private string _text = "";

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

        UiTranslation.ShareContext(this, _label);
        _label.Text = Text;
        UiThemeLookup.ApplyTextStyle(_label, UiTokens.Typography.Note, UiTokens.Color.Muted);
    }
}
