using NodeRunner.Domain;

namespace NodeRunner.Ui.Tests;

/// <summary>The Import screen's authored part rows (#899), which <c>ImportScreen</c> finds by part kind.</summary>
public sealed class ImportScreenTests
{
    [Fact]
    public void PartRows_HaveOneRowPerKindOfPart_NamedAfterIt() =>
        SceneNodes.InScene("screens/ImportScreen.tscn")
            .Where(node => node.Parent?.EndsWith("/PartRows", StringComparison.Ordinal) == true)
            .Select(node => node.Name)
            .ShouldBe(Enum.GetNames<PartKind>(), ignoreOrder: true);

    // The mono count stands on its name's baseline (#1232); Godot resolves the reference only
    // when the header lists it in node_paths.
    [Fact]
    public void PartCounts_StandOnTheirNamesBaseline() =>
        SceneNodes.InScene("screens/ImportScreen.tscn")
            .Where(node => node.Name == "Count" && node.Parent?.Contains("/PartRows/", StringComparison.Ordinal) == true)
            .Where(node => node.Node.Header.Contains("node_paths=PackedStringArray(\"BaselineOf\")", StringComparison.Ordinal)
                && node.Node.Body.Contains("\nBaselineOf = NodePath(\"../Label\")", StringComparison.Ordinal))
            .Select(node => node.Parent![(node.Parent!.LastIndexOf('/') + 1)..])
            .ShouldBe(Enum.GetNames<PartKind>(), ignoreOrder: true);
}
