namespace NodeRunner.Ui.Lib;

/// <summary>Filled primary action using the active theme's accent colour.</summary>
public partial class UiPrimaryButton : UiButton
{
    public UiPrimaryButton()
    {
        Style = UiButtonStyle.Primary;
        HorizontalPadding = UiSpace.Space4;
        VerticalPadding = UiSpace.None;
        Filled = true;
        Enabled = true;
        Progress = -1;
    }
}
