namespace NodeRunner.Ui.Lib;

/// <summary>Filled primary icon action using the active theme's accent colour.</summary>
public partial class UiPrimaryIconButton : UiIconButton
{
    public UiPrimaryIconButton()
    {
        BorderColor = UiColor.Accent;
        ContentColor = UiColor.OnAccent;
        Filled = true;
    }
}
