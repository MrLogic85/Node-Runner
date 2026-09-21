namespace NodeRunner.Ui.Lib;

/// <summary>Default outlined action, matching the canonical Cancel button.</summary>
public partial class UiSecondaryButton : UiButton
{
    public UiSecondaryButton()
    {
        BorderColor = UiColor.LineStrong;
        ContentColor = UiColor.Ink;
        HorizontalPadding = UiSpace.Space4;
        VerticalPadding = UiSpace.None;
        Filled = false;
        Enabled = true;
        Progress = -1;
    }
}
