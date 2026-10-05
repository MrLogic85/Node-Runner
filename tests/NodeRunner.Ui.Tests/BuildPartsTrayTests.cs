using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Screens;

namespace NodeRunner.Ui.Tests;

/// <summary>The Build Parts tray (#374): one icon tab per catalog group, part glyph rows.</summary>
public sealed class BuildPartsTrayTests
{
    private static readonly SceneNodes.SceneNode[] _build = [.. SceneNodes.InScene("screens/BuildScreen.tscn")];

    [Fact]
    public void Tabs_AreOneIconTabPerGroup_WithTheReferenceGlyphs()
    {
        var tabs = _build.Single(node => node.Name == "PartTabs");

        tabs.Script.ShouldBe("res://src/ui/lib/UiIconTabs.cs");
        int[] icons = [(int)UiIconId.PartServo, (int)UiIconId.PartCamera, (int)UiIconId.PartBattery];
        tabs.Node.Body.ShouldContain($"Icons = Array[int]([{string.Join(", ", icons)}])");
        icons.Length.ShouldBe(PartTray.Groups().Count);
    }

    [Fact]
    public void Tray_PinsTabs_AndScrollsGroupHeader_Rows_AndHelp()
    {
        Children("/PartsTray").ShouldBe(["PartTabs", "PartScroll"]);
        _build.Single(node => node.Name == "PartScroll").Type.ShouldBe("ScrollContainer");
        Children("/PartScroll/PartList").ShouldBe(["PartGroupHeader", "PartRows", "PartHelp"]);
        Children("/PartGroupHeader").ShouldBe(["PartGroupName", "PartLockedIcon", "PartLockedNote"]);
    }

    [Fact]
    public void SidePanel_KeepsTray_Help_Settings_Selection_Saved_AndReadiness()
    {
        var content = _build.Where(node => node.Parent?.EndsWith("/SidePanelContent", StringComparison.Ordinal) == true).ToList();

        content.Select(node => node.Name).ShouldBe(
            ["PartsTray", "SavedCreation", "PartSettings", "Selection", "JointHelp", "SelectHelp", "PanelSpacer", "Readiness"]);
        content.ShouldAllBe(node => node.IsUnique);
    }

    [Fact]
    public void EveryPart_HasItsOwnPartGlyph()
    {
        var icons = Enum.GetValues<BuildPart>().Select(BuildScreen.PartIcon).ToList();

        icons.ShouldAllBe(icon => UiIcons.IsPartGlyph(icon));
        icons.ShouldBeUnique();
    }

    [Fact]
    public void EveryLink_HasItsOwnPartGlyph()
    {
        var icons = Enum.GetValues<BuildLink>().Select(BuildScreen.LinkIcon).ToList();

        icons.ShouldAllBe(icon => UiIcons.IsPartGlyph(icon));
        icons.ShouldBeUnique();
    }

    [Fact]
    public void OnlyAvailableSensorRows_CanBeDraggedOut()
    {
        var draggable = PartTray.Groups()
            .SelectMany(group => group.Rows)
            .Where(row => BuildScreen.DraggablePart(row) is not null)
            .Select(row => row.Part);

        draggable.ShouldBe([BuildPart.Accelerometer]);
    }

    [Fact]
    public void Canvas_TakesTrayDrops_InAZoneThatLetsTouchesThrough()
    {
        var zone = _build.Single(node => node.Name == "PartDropZone");
        var canvas = _build.Single(node => node.Name == "BuildCanvas");

        zone.Parent.ShouldEndWith("/CanvasSlot");
        zone.Node.Body.ShouldContain("mouse_filter = 2");
        canvas.Node.Body.ShouldContain("PartDropZone = NodePath(\"../../../PartDropZone\")");
    }

    [Fact]
    public void Canvas_IsTheWorld_OfTheBuildView()
    {
        var view = _build.Single(node => node.Name == "BuildView");
        var canvas = _build.Single(node => node.Name == "BuildCanvas");

        view.Parent.ShouldEndWith("/CanvasSlot");
        canvas.Parent.ShouldEndWith("/CanvasSlot/BuildView/WorldViewport");
        canvas.Node.Body.ShouldContain("WorldView = NodePath(\"../..\")");
    }

    private static string[] Children(string parent) =>
        [.. _build.Where(node => node.Parent?.EndsWith(parent, StringComparison.Ordinal) == true).Select(node => node.Name)];
}
