using NodeRunner.App.ViewModels;

namespace NodeRunner.Ui.Tests;

/// <summary>The Import screen's authored part rows (#899), which <c>ImportScreen</c> finds by part kind.</summary>
public sealed class ImportScreenTests
{
    // The Angle sensor's row comes with its icon and Build placement (#1206); until then
    // ImportPresentation lists no Angle sensors, so the screen never looks for its row.
    [Fact]
    public void PartRows_HaveOneRowPerKindOfPart_NamedAfterIt() =>
        SceneNodes.InScene("screens/ImportScreen.tscn")
            .Where(node => node.Parent?.EndsWith("/PartRows", StringComparison.Ordinal) == true)
            .Select(node => node.Name)
            .ShouldBe(Enum.GetNames<PartSettingsKind>().Where(name => name != nameof(PartSettingsKind.AngleSensor)), ignoreOrder: true);
}
