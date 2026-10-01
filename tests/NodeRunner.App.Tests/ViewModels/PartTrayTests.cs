using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class PartTrayTests
{
    [Fact]
    public void Groups_ListReferenceTabsInOrder()
    {
        PartTray.Groups(BuildTool.Move).Select(group => group.Name)
            .ShouldBe(["Links", "On a joint", "Sensors", "Blocks"]);
    }

    [Fact]
    public void Groups_ListReferencePartsInOrder()
    {
        PartTray.Groups(BuildTool.Move).Select(group => group.Rows.Select(row => row.Name).ToArray()).ShouldBe(
        [
            ["Spring", "Piston", "Wing"],
            ["Brake", "Servo", "Stepper", "Velocity motor", "Wheel"],
            ["Accelerometer", "LOS sensor", "Core"],
            ["Battery", "Generator", "Fuel tank"],
        ]);
    }

    [Fact]
    public void Groups_ListEveryPartOnce()
    {
        PartTray.Groups(BuildTool.Move).SelectMany(group => group.Rows).Select(row => row.Part)
            .ShouldBe(Enum.GetValues<BuildPart>(), ignoreOrder: true);
    }

    [Fact]
    public void Groups_GiveEachTabOneHelpLine()
    {
        PartTray.Groups(BuildTool.Move).Select(group => group.HelpText).ShouldBe(
        [
            "Pick one, then drag from one node to another, like the Beam tool.",
            "Drag onto a joint. A joint holds one part.",
            "Drag onto a beam. A beam holds one of each sensor.",
            "Drag it onto the canvas, then draw beams to its two eyes.",
        ]);
    }

    [Fact]
    public void Rows_NotYetImplemented_AreComingLater()
    {
        var rows = PartTray.Groups(BuildTool.Move).SelectMany(group => group.Rows).ToList();

        rows.Where(row => row.Part != BuildPart.Core).ShouldAllBe(row =>
            row.State == PartTrayRowState.ComingLater && !row.IsAvailable && row.LockedReason == "Coming later");
    }

    [Fact]
    public void CoreRow_IsAvailableWithNoCount_AndSelectedWhileItsToolIsActive()
    {
        Core(BuildTool.Move).State.ShouldBe(PartTrayRowState.Available);
        Core(BuildTool.Move).LockedReason.ShouldBeEmpty();
        Core(BuildTool.Core).State.ShouldBe(PartTrayRowState.Selected);
        PartTray.ToolFor(BuildPart.Core).ShouldBe(BuildTool.Core);
    }

    [Fact]
    public void PresentationPartGroups_FollowActiveTool()
    {
        var build = new BuildViewModel { IsActive = true, ActiveTool = BuildTool.Core };

        new BuildPresentationViewModel(build).PartGroups[2].Rows.Single(row => row.Part == BuildPart.Core)
            .State.ShouldBe(PartTrayRowState.Selected);
    }

    [Fact]
    public void LockedNote_ShowsOncePerTabWithALockedRow()
    {
        PartTray.Groups(BuildTool.Move).Select(group => group.LockedNote).ShouldAllBe(note => note == "Coming later");
    }

    [Fact]
    public void HelpLine_OfTheTabHoldingTheActiveTrayTool_IsThatToolsHint()
    {
        var groups = PartTray.Groups(BuildTool.Core);

        groups[2].HelpText.ShouldBe("Tap a node to attach a core, tap again to remove it.");
        groups[0].HelpText.ShouldBe("Pick one, then drag from one node to another, like the Beam tool.");
        PartTray.IsTrayTool(BuildTool.Core).ShouldBeTrue();
        PartTray.IsTrayTool(BuildTool.Beam).ShouldBeFalse();
    }

    [Theory]
    [InlineData(BuildTool.Core, 0, BuildTool.Move)]
    [InlineData(BuildTool.Core, 2, null)]
    [InlineData(BuildTool.Beam, 0, null)]
    [InlineData(BuildTool.Move, 3, null)]
    public void OpeningATab_PutsMoveBack_OnlyWhenItHidesTheActivePartTool(BuildTool active, int group, BuildTool? expected) =>
        PartTray.ToolOnTabOpened(active, group).ShouldBe(expected);

    [Theory]
    [InlineData(BuildTool.Move, "")]
    [InlineData(BuildTool.Core, "")]
    [InlineData(BuildTool.Beam, "Drag joint to joint.")]
    [InlineData(BuildTool.Joint, "Tap space or a beam.")]
    [InlineData(BuildTool.Select, "Tap or box parts.")]
    public void PanelToolHint_ShowsOnlyForRailToolsThatNeedIt(BuildTool tool, string expected)
    {
        new BuildPresentationViewModel(new BuildViewModel { IsActive = true, ActiveTool = tool })
            .PanelToolHint.ShouldBe(expected);
    }

    private static PartTrayRow Core(BuildTool tool) =>
        PartTray.Groups(tool).SelectMany(group => group.Rows).Single(row => row.Part == BuildPart.Core);
}
