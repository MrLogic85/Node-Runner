namespace NodeRunner.Ui.Lib;

/// <summary>An action option with a left-aligned checkbox.</summary>
[Godot.Tool]
[Godot.GlobalClass]
public partial class UiCheckRow : UiChoiceRow
{
    public UiCheckRow()
    {
        LabelText = "Run until power is out";
    }

    [Godot.Export]
    public bool Checked
    {
        get => Selected;
        set => Selected = value;
    }

    protected override bool IsSwitch => false;
}
