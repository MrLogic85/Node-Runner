namespace NodeRunner.Ui.Lib;

/// <summary>An immediate on/off setting with a right-aligned switch.</summary>
public partial class UiToggleRow : UiChoiceRow
{
    public UiToggleRow()
    {
        LabelText = "Sounds";
        Selected = true;
    }

    [Godot.Export]
    public bool On
    {
        get => Selected;
        set => Selected = value;
    }

    protected override bool IsSwitch => true;
}
