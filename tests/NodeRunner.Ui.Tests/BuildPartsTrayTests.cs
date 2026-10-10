using System.Text.RegularExpressions;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Screens;

namespace NodeRunner.Ui.Tests;

/// <summary>The Build Parts tray (#374): one icon tab per catalog group, part glyph rows.</summary>
public sealed partial class BuildPartsTrayTests
{
    private static readonly SceneNodes.SceneNode[] _build = [.. SceneNodes.InScene("screens/BuildScreen.tscn")];

    // UiIconTabs' selected index is the group's index in PartTray.Groups().
    [Fact]
    public void Tabs_HaveOneIconPerGroup()
    {
        var tabs = _build.Single(node => node.Name == "PartTabs").Node.Body;

        var icons = TabIcons().Match(tabs).Groups["icons"].Value.Split(", ");

        icons.Length.ShouldBe(PartTray.Groups().Count);
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

        draggable.ShouldBe([BuildPart.Servo, BuildPart.Wheel, BuildPart.Accelerometer, BuildPart.Camera]);
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

    [GeneratedRegex(@"Icons = Array\[int\]\(\[(?<icons>[^\]]+)\]\)")]
    private static partial Regex TabIcons();
}
