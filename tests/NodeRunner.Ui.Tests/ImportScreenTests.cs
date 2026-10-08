using NodeRunner.App.ViewModels;

namespace NodeRunner.Ui.Tests;

/// <summary>The Import screen's authored part rows (#899), which <c>ImportScreen</c> finds by part kind.</summary>
public sealed class ImportScreenTests
{
    [Fact]
    public void PartRows_HaveOneRowPerKindOfPart_NamedAfterIt_InThePartsTrayOrder() =>
        SceneNodes.InScene("screens/ImportScreen.tscn")
            .Where(node => node.Parent?.EndsWith("/PartRows", StringComparison.Ordinal) == true)
            .Select(node => node.Name)
            .ShouldBe(Enum.GetNames<PartSettingsKind>());
}
