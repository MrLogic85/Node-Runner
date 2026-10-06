using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class PartTrayTests
{
    [Fact]
    public void Groups_ListReferenceTabsInOrder()
    {
        PartTray.Groups().Select(group => group.Name)
            .ShouldBe(Plain("Moving parts", "Sensors", "Blocks"));
    }

    [Fact]
    public void Groups_ListReferencePartsInOrder()
    {
        PartTray.Groups().Select(group => group.Rows.Select(row => row.Name).ToArray()).ShouldBe(
        [
            Plain("Servo", "Stepper", "Velocity motor", "Brake", "Wheel"),
            Plain("Accelerometer", "Camera", "Touch sensor", "Pulse"),
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
    public void OpeningGroup_IsTheFirstTabWithAnAvailablePart()
    {
        var opening = PartTray.OpeningGroup();

        opening.ShouldBe(0);
        PartTray.Groups()[opening].Rows.ShouldContain(row => row.IsAvailable);
        PartTray.Groups().Take(opening).ShouldAllBe(group => group.Rows.All(row => !row.IsAvailable));
    }

    [Fact]
    public void SensorsTab_HasTheAccelerometerAvailable_AndTheRestComingLater()
    {
        var sensors = PartTray.Groups()[1].Rows;

        sensors.Select(row => (row.Part, row.State)).ShouldBe(
            [
                (BuildPart.Accelerometer, PartTrayRowState.Available),
                (BuildPart.Camera, PartTrayRowState.ComingLater),
                (BuildPart.TouchSensor, PartTrayRowState.ComingLater),
                (BuildPart.Pulse, PartTrayRowState.ComingLater),
            ]);
        sensors[0].LockedReason.ShouldBeNull();
    }

    [Fact]
    public void Rows_NotYetImplemented_AreComingLater()
    {
        var rows = PartTray.Groups().SelectMany(group => group.Rows).ToList();

        rows.Where(row => row.Part is not BuildPart.Accelerometer and not BuildPart.Servo).ShouldAllBe(row =>
            row.State == PartTrayRowState.ComingLater && !row.IsAvailable && Equals(row.LockedReason, UiText.Plain("Coming later")));
    }

    [Fact]
    public void LinkList_ShowsBeamPickedWithWingLocked()
    {
        var list = BuildLinkList.Create(BuildLink.Beam);

        list.PickedInfo.ShouldBe(UiText.Plain("A rigid rod."));
        list.HelpText.ShouldBe(UiText.Plain("Drag from joint to joint to add the picked link."));
        list.Rows.Select(row => (row.Link, row.Name, row.State)).ShouldBe([
            (BuildLink.Beam, UiText.Plain("Beam"), LinkListRowState.Selected),
            (BuildLink.Piston, UiText.Plain("Piston"), LinkListRowState.Rest),
            (BuildLink.Spring, UiText.Plain("Spring"), LinkListRowState.Rest),
            (BuildLink.Wing, UiText.Plain("Wing"), LinkListRowState.Locked)]);
    }

    [Fact]
    public void LinkList_InfoFollowsPickedLink()
    {
        BuildLinkList.Create(BuildLink.Piston).PickedInfo.ShouldBe(UiText.Plain("Extends and retracts."));
    }

    private static UiText[] Plain(params string[] messages) => [.. messages.Select(UiText.Plain)];
}
