using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class PartTrayTests
{
    [Fact]
    public void Groups_ListReferenceTabsInOrder()
    {
        PartTray.Groups().Select(group => group.Name)
            .ShouldBe(["Links", "On a joint", "Sensors", "Blocks"]);
    }

    [Fact]
    public void Groups_ListReferencePartsInOrder()
    {
        PartTray.Groups().Select(group => group.Rows.Select(row => row.Name).ToArray()).ShouldBe(
        [
            ["Spring", "Piston", "Wing"],
            ["Brake", "Servo", "Stepper", "Velocity motor", "Wheel"],
            ["Accelerometer", "LOS sensor"],
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
            "Pick one, then drag from one node to another, like the Beam tool.",
            "Drag onto a joint. A joint holds one part.",
            "Drag onto a beam. A beam holds one of each sensor.",
            "Drag it onto the canvas, then draw beams to its two eyes.",
        ]);
    }

    [Fact]
    public void SensorsTab_HasBothSensorsAvailable()
    {
        var sensors = PartTray.Groups()[2].Rows;

        sensors.Select(row => row.Part).ShouldBe([BuildPart.Accelerometer, BuildPart.LosSensor]);
        sensors.ShouldAllBe(row => row.State == PartTrayRowState.Available && row.LockedReason == string.Empty);
    }

    [Fact]
    public void Rows_NotYetImplemented_AreComingLater()
    {
        var rows = PartTray.Groups().SelectMany(group => group.Rows).ToList();

        rows.Where(row => row.Part is not (BuildPart.Accelerometer or BuildPart.LosSensor)).ShouldAllBe(row =>
            row.State == PartTrayRowState.ComingLater && !row.IsAvailable && row.LockedReason == "Coming later");
    }

    [Fact]
    public void LockedNote_ShowsOnTabsWithALockedRow()
    {
        PartTray.Groups().Select(group => group.LockedNote).ShouldBe(["Coming later", "Coming later", "", "Coming later"]);
    }

    [Theory]
    [InlineData(BuildTool.Move, "")]
    [InlineData(BuildTool.Beam, "Drag joint to joint.")]
    [InlineData(BuildTool.Joint, "Tap space or a beam.")]
    [InlineData(BuildTool.Select, "Tap or box parts.")]
    public void PanelToolHint_ShowsOnlyForRailToolsThatNeedIt(BuildTool tool, string expected)
    {
        new BuildPresentationViewModel(new BuildViewModel { IsActive = true, ActiveTool = tool })
            .PanelToolHint.ShouldBe(expected);
    }
}
