using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Name-field composition over the canonical text field.</summary>
[Tool]
[GlobalClass]
public partial class UiNameField : UiTextField
{
    public UiNameField()
    {
        InputSize = TextInputSize.Compact;
        LabelText = "Name";
        TextValue = "Left foot";
    }
}
