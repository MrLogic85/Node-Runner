using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Screens;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// Part settings for one selected part (#343): Name first, read-only rows, Delete in its own column;
/// and the selection panel for several (#558).
/// </summary>
public sealed class BuildPartSettingsTests
{
    private static readonly SceneNodes.SceneNode[] _build = [.. SceneNodes.InScene("screens/BuildScreen.tscn")];

    [Fact]
    public void PartSettings_IsRowsThenDelete_WithNoCloseButton()
    {
        Children("/PartSettings").ShouldBe(["PartRows", "PartActions"]);
        Children("/PartSettings/PartRows").ShouldBe(["PartName", "PartParameters", "PartPickers", "PartConnections", "PartAdvancedGap", "PartAdvanced", "PartNote"]);
        Children("/PartSettings/PartRows/PartAdvanced").ShouldBe(["PartAdvancedParameters"]);
        Children("/PartSettings/PartActions").ShouldBe(["PartDelete"]);
        _build.ShouldNotContain(node => node.Name == "PartClose");
    }

    [Fact]
    public void ParameterSliders_AreMadeFromThePresentation_NotNamedInTheScene()
    {
        var part = _build.Single(node => node.Name == "PartParameters");

        part.IsUnique.ShouldBeTrue();
        part.Node.Body.ShouldContain("visible = false");
        _build.Single(node => node.Name == "SelectionParameters").IsUnique.ShouldBeTrue();
        Children("/PartSettings/PartRows/PartParameters").ShouldBeEmpty();
        Children("/Selection/SelectionSettings/SelectionParameters").ShouldBeEmpty();
        _build.ShouldNotContain(node => node.Script == "res://src/ui/lib/UiSlider.cs");
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

    [Fact]
    public void Selection_HasSharedSettings_TheThreeHandles_ThenDeleteAndItsNote_WithNoCloseButton()
    {
        Children("/Selection").ShouldBe(["SelectionSettings", "SelectionEmptyNote", "SelectionRows", "SelectionActions"]);
        Children("/Selection/SelectionSettings").ShouldBe(["SelectionParameters", "SelectionAdvancedGap", "SelectionAdvanced", "SelectionSettingsNote"]);
        Children("/Selection/SelectionSettings/SelectionAdvanced").ShouldBe(["SelectionAdvancedParameters"]);
        Children("/Selection/SelectionRows").ShouldBe(["SelectionMove", "SelectionRotate", "SelectionScale"]);
        Children("/Selection/SelectionActions").ShouldBe(["SelectionDelete", "SelectionDeleteNote"]);
        _build.ShouldNotContain(node => node.Name == "SelectionClear");

        int[] icons = [(int)UiIconId.Move, (int)UiIconId.Rotate, (int)UiIconId.Scale];
        var rows = _build.Where(node => node.Parent?.EndsWith("/SelectionRows", StringComparison.Ordinal) == true).ToList();
        rows.ShouldAllBe(row => row.Script == "res://src/ui/lib/UiInfoRow.cs");
        rows.Select(row => row.Node.Body).Zip(icons).ShouldAllBe(pair => pair.First.Contains($"IconId = {pair.Second}"));
        _build.Single(node => node.Name == "SelectionDelete").Node.Body.ShouldContain($"Kind = {(int)UiButtonKind.Tertiary}");
    }

    private static string[] Children(string parent) =>
        [.. _build.Where(node => node.Parent?.EndsWith(parent, StringComparison.Ordinal) == true).Select(node => node.Name)];
}
