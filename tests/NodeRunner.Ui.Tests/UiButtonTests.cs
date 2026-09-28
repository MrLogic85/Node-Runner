namespace NodeRunner.Ui.Tests;

public sealed class UiButtonTests
{
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
