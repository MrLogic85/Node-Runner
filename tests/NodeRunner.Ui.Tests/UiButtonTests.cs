namespace NodeRunner.Ui.Tests;

public sealed class UiButtonTests
{
    // UiButton renders its caption through an internal UiLabel, so a saved font or color
    // override on the button would either pin a palette value or reveal the native text.
    [Fact]
    public void Scenes_DoNotBakeThemeValuesIntoUiButtons()
    {
        var offenders = SceneNodes.WithScript("UiButton.cs")
            .Where(node => SceneNodes.PaletteOverrideGroups.Any(
                group => node.Body.Contains(group, StringComparison.Ordinal)))
            .Select(node => node.ToString());

        offenders.ShouldBeEmpty();
    }

    // The caption lives in native Text; Godot drops unknown saved properties silently.
    [Fact]
    public void Scenes_AuthorUiButtonCaptionsInNativeText()
    {
        SceneNodes.WithScript("UiButton.cs")
            .Where(node => node.Body.Contains("\nLabelText = ", StringComparison.Ordinal))
            .Select(node => node.ToString())
            .ShouldBeEmpty();
    }
}
