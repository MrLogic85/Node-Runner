namespace NodeRunner.Ui.Tests;

public sealed class UiLabelTests
{
    [Fact]
    public void Scenes_DoNotBakeThemeValuesIntoUiLabels()
    {
        var offenders = SceneNodes.WithScript("UiLabel.cs")
            .Where(node => SceneNodes.PaletteOverrideGroups.Any(
                group => node.Body.Contains(group, StringComparison.Ordinal)))
            .Select(node => node.ToString());

        offenders.ShouldBeEmpty();
    }
}
