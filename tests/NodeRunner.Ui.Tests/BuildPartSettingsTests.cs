using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Screens;

namespace NodeRunner.Ui.Tests;

/// <summary>Part settings for one selected part (#343) and the selection panel for several (#558).</summary>
public sealed class BuildPartSettingsTests
{
    private static readonly SceneNodes.SceneNode[] _build = [.. SceneNodes.InScene("screens/BuildScreen.tscn")];

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
    public void Readouts_AreMadeFromThePresentation_AfterTheBasicSettings()
    {
        var readouts = _build.Single(node => node.Name == "PartReadouts");

        readouts.IsUnique.ShouldBeTrue();
        readouts.Node.Body.ShouldContain("visible = false");
        Children("/PartSettings/PartRows/PartReadouts").ShouldBeEmpty();
        _build.ShouldNotContain(node => node.Script == "res://src/ui/lib/UiValueRow.cs");
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
