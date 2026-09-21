namespace NodeRunner.Ui.Lib;

/// <summary>Outlined destructive action using the active theme's danger colour.</summary>
public partial class UiTertiaryButton : UiButton
{
    public UiTertiaryButton()
    {
        BorderColor = UiColor.Danger;
        ContentColor = UiColor.Danger;
        HorizontalPadding = UiSpace.Space4;
        VerticalPadding = UiSpace.None;
        Filled = false;
        Enabled = true;
        Progress = -1;
    }
}
