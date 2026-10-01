using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Screens;

namespace NodeRunner.Ui.Tests;

/// <summary>Part settings for one selected part (#343): Name first, read-only rows, Delete in its own column.</summary>
public sealed class BuildPartSettingsTests
{
    private static readonly SceneNodes.SceneNode[] _build = [.. SceneNodes.InScene("screens/BuildScreen.tscn")];

    [Fact]
    public void PartSettings_IsRowsThenDelete_WithNoCloseButton()
    {
        Children("/PartSettings").ShouldBe(["PartRows", "PartActions"]);
        Children("/PartSettings/PartRows").ShouldBe(["PartName", "PartConnections", "PartNote"]);
        Children("/PartSettings/PartActions").ShouldBe(["PartDelete"]);
        _build.ShouldNotContain(node => node.Name == "PartClose");
    }

    [Fact]
    public void Name_IsALibraryTextField()
    {
        var name = _build.Single(node => node.Name == "PartName");

        name.IsUnique.ShouldBeTrue();
        name.Script.ShouldBe("res://src/ui/lib/UiTextField.cs");
        name.Node.Body.ShouldContain("LabelText = \"Name\"");
    }

    [Fact]
    public void Delete_IsAFullWidthDangerButtonInItsOwnColumn()
    {
        var actions = _build.Single(node => node.Name == "PartActions");
        var delete = _build.Single(node => node.Name == "PartDelete");

        actions.Type.ShouldBe("VBoxContainer");
        delete.Script.ShouldBe("res://src/ui/lib/UiButton.cs");
        delete.Node.Body.ShouldContain($"Kind = {(int)UiButtonKind.Tertiary}");
    }

    [Fact]
    public void EveryPartKind_HasItsOwnPanelGlyph()
    {
        var icons = Enum.GetValues<PartSettingsKind>().Select(BuildScreen.PartSettingsIcon).ToList();

        icons.ShouldNotContain(UiIconId.None);
        icons.ShouldBeUnique();
    }

    private static string[] Children(string parent) =>
        [.. _build.Where(node => node.Parent?.EndsWith(parent, StringComparison.Ordinal) == true).Select(node => node.Name)];
}
