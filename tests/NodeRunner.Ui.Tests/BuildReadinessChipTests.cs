namespace NodeRunner.Ui.Tests;

/// <summary>Build's readiness chip at the canvas's bottom right (#1163).</summary>
public sealed class BuildReadinessChipTests
{
    private static readonly SceneNodes.SceneNode[] _build = [.. SceneNodes.InScene("screens/BuildScreen.tscn")];

    // UiChip's constructor sets Pass after the scene's own mouse_filter, so only the inset's
    // recursive behaviour lets a tap on the chip reach a joint under it.
    [Fact]
    public void Chip_LetsTouchesThroughToTheCanvas()
    {
        var inset = _build.Single(node => node.Name == "ReadinessInset");

        inset.Node.Body.ShouldContain("mouse_filter = 2");
        inset.Node.Body.ShouldContain("mouse_behavior_recursive = 1");
    }
}
