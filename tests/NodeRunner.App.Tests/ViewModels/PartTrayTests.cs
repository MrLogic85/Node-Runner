using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class PartTrayTests
{
    [Fact]
    public void Groups_ListReferenceTabsInOrder()
    {
        PartTray.Groups().Select(group => group.Name)
            .ShouldBe(Plain("On a joint", "Sensors", "Blocks"));
    }

    [Fact]
    public void Groups_ListReferencePartsInOrder()
    {
        PartTray.Groups().Select(group => group.Rows.Select(row => row.Name).ToArray()).ShouldBe(
        [
            Plain("Brake", "Servo", "Stepper", "Velocity motor", "Wheel"),
            Plain("Accelerometer", "Camera"),
            Plain("Battery", "Generator", "Fuel tank"),
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
        PartTray.Groups().Select(group => group.HelpText).ShouldBe(Plain(
            "Drag onto a joint. A joint holds one part.",
            "Drag onto a beam. A beam holds one sensor.",
            "Drag it onto the canvas, then draw beams to its two eyes."));
    }

    [Fact]
    public void SensorsTab_HasTheAccelerometerAvailable_AndTheCameraComingLater()
    {
        var sensors = PartTray.Groups()[1].Rows;

        sensors.Select(row => (row.Part, row.State)).ShouldBe(
            [(BuildPart.Accelerometer, PartTrayRowState.Available), (BuildPart.Camera, PartTrayRowState.ComingLater)]);
        sensors[0].LockedReason.ShouldBeNull();
    }

    [Fact]
    public void Rows_NotYetImplemented_AreComingLater()
    {
        var rows = PartTray.Groups().SelectMany(group => group.Rows).ToList();

        rows.Where(row => row.Part is not BuildPart.Accelerometer).ShouldAllBe(row =>
            row.State == PartTrayRowState.ComingLater && !row.IsAvailable && Equals(row.LockedReason, UiText.Plain("Coming later")));
    }

    [Fact]
    public void LockedNote_ShowsOnTabsWithALockedRow()
    {
        PartTray.Groups().Select(group => group.LockedNote).ShouldAllBe(note => Equals(note, UiText.Plain("Coming later")));
    }

    [Fact]
    public void LinkList_ShowsBeamPickedWithWingLocked()
    {
        var list = BuildLinkList.Create(BuildLink.Beam);

        list.Name.ShouldBe(UiText.Plain("Links"));
        list.LockedNote.ShouldBe(UiText.Plain("Coming later"));
        list.HelpText.ShouldBe(UiText.Plain("A rigid rod. Drag joint to joint."));
        list.Rows.Select(row => (row.Link, row.Name, row.State)).ShouldBe([
            (BuildLink.Beam, UiText.Plain("Beam"), LinkListRowState.Selected),
            (BuildLink.Piston, UiText.Plain("Piston"), LinkListRowState.Rest),
            (BuildLink.Spring, UiText.Plain("Spring"), LinkListRowState.Rest),
            (BuildLink.Wing, UiText.Plain("Wing"), LinkListRowState.Locked)]);
    }

    [Fact]
    public void LinkList_HelpFollowsPickedLink()
    {
        BuildLinkList.Create(BuildLink.Piston).HelpText.ShouldBe(UiText.Plain("The brain pushes and pulls it. Drag joint to joint."));
    }

    private static UiText[] Plain(params string[] messages) => [.. messages.Select(UiText.Plain)];
}
