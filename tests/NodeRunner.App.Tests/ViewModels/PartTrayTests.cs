using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class PartTrayTests
{
    [Fact]
    public void Groups_ListReferenceTabsInOrder()
    {
        PartTray.Groups().Select(group => group.Name)
            .ShouldBe(["On a joint", "Sensors", "Blocks"]);
    }

    [Fact]
    public void Groups_ListReferencePartsInOrder()
    {
        PartTray.Groups().Select(group => group.Rows.Select(row => row.Name).ToArray()).ShouldBe(
        [
            ["Brake", "Servo", "Stepper", "Velocity motor", "Wheel"],
            ["Accelerometer", "Camera"],
            ["Battery", "Generator", "Fuel tank"],
        ]);
    }

    [Fact]
    public void Groups_ListEveryPartOnce()
    {
        PartTray.Groups().SelectMany(group => group.Rows).Select(row => row.Part)
            .ShouldBe(Enum.GetValues<BuildPart>(), ignoreOrder: true);
    }

    [Fact]
    public void Groups_GiveEachTabOneHelpLine()
    {
        PartTray.Groups().Select(group => group.HelpText).ShouldBe(
        [
            "Drag onto a joint. A joint holds one part.",
            "Drag onto a beam. A beam holds one sensor.",
            "Drag it onto the canvas, then draw beams to its two eyes.",
        ]);
    }

    [Fact]
    public void SensorsTab_HasBothSensorsAvailable()
    {
        var sensors = PartTray.Groups()[1].Rows;

        sensors.Select(row => row.Part).ShouldBe([BuildPart.Accelerometer, BuildPart.Camera]);
        sensors.ShouldAllBe(row => row.State == PartTrayRowState.Available && row.LockedReason == string.Empty);
    }

    [Fact]
    public void Rows_NotYetImplemented_AreComingLater()
    {
        var rows = PartTray.Groups().SelectMany(group => group.Rows).ToList();

        rows.Where(row => row.Part is not (BuildPart.Accelerometer or BuildPart.Camera)).ShouldAllBe(row =>
            row.State == PartTrayRowState.ComingLater && !row.IsAvailable && row.LockedReason == "Coming later");
    }

    [Fact]
    public void LockedNote_ShowsOnTabsWithALockedRow()
    {
        PartTray.Groups().Select(group => group.LockedNote).ShouldBe(["Coming later", "", "Coming later"]);
    }

    [Fact]
    public void LinkList_ShowsBeamPickedWithFutureLinksLocked()
    {
        var list = BuildLinkList.Create(BuildLink.Beam);

        list.Title.ShouldBe("Beams");
        list.Name.ShouldBe("Links");
        list.LockedNote.ShouldBe("Coming later");
        list.HelpText.ShouldBe("A rigid rod. Drag joint to joint.");
        list.Rows.Select(row => (row.Link, row.Name, row.State)).ShouldBe([
            (BuildLink.Beam, "Beam", LinkListRowState.Selected),
            (BuildLink.Piston, "Piston", LinkListRowState.Rest),
            (BuildLink.Spring, "Spring", LinkListRowState.Locked),
            (BuildLink.Wing, "Wing", LinkListRowState.Locked)]);
    }

    [Fact]
    public void LinkList_HelpFollowsPickedLink()
    {
        BuildLinkList.Create(BuildLink.Piston).HelpText.ShouldBe("The brain pushes and pulls it. Drag joint to joint.");
    }

    [Theory]
    [InlineData(BuildTool.Move, "")]
    [InlineData(BuildTool.Beam, "")]
    [InlineData(BuildTool.Joint, "Tap space or a beam.")]
    [InlineData(BuildTool.Select, "Tap or box parts.")]
    public void PanelToolHint_ShowsOnlyForRailToolsThatNeedIt(BuildTool tool, string expected)
    {
        new BuildPresentationViewModel(new BuildViewModel { IsActive = true, ActiveTool = tool })
            .PanelToolHint.ShouldBe(expected);
    }
}
