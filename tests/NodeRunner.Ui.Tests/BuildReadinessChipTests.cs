namespace NodeRunner.Ui.Tests;

/// <summary>Build's readiness chip at the canvas's bottom right (#1163).</summary>
public sealed class BuildReadinessChipTests
{
    private static readonly SceneNodes.SceneNode[] _build = [.. SceneNodes.InScene("screens/BuildScreen.tscn")];

    // The chip covers the canvas's corner, so a tap there still reaches the joint under it.
    [Fact]
    public void Chip_LetsTouchesThroughToTheCanvas()
    {
        var inset = _build.Single(node => node.Name == "ReadinessInset");
        var chip = _build.Single(node => node.Name == "Readiness");

        inset.Node.Body.ShouldContain("mouse_filter = 2");
        chip.Node.Body.ShouldContain("mouse_filter = 2");
    }
}
