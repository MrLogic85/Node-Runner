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
    public void Create_WithNothingPicked_SaysHowToPick()
    {
        var tray = PartTray.Create(picked: null);

        tray.HelpText.ShouldBe(UiText.Plain("Tap a part to pick it, or drag it onto the creature."));
        tray.PickedInfo.ShouldBeNull();
        tray.Groups.SelectMany(group => group.Rows).ShouldNotContain(row => row.State == PartTrayRowState.Selected);
    }

    [Theory]
    [InlineData(BuildPart.Servo, "A motor that tries to hold a target angle.", "Tap a joint to place it. A joint holds one part.")]
    [InlineData(BuildPart.Accelerometer, "Measures its beam's acceleration.", "Tap a beam to place it. A beam holds one sensor.")]
    public void Create_WithAPickedPart_SelectsItsRow_AndSaysUnderItWhatItDoesAndWhereItGoes(BuildPart part, string info, string placement)
    {
        var tray = PartTray.Create(part);

        tray.Groups.SelectMany(group => group.Rows).Where(row => row.State == PartTrayRowState.Selected)
            .Select(row => row.Part).ShouldBe([part]);
        tray.Groups.SelectMany(group => group.Rows).Single(row => row.Part == part).IsAvailable.ShouldBeTrue();
        tray.PickedInfo.ShouldBe(UiText.Format("{0}\n{1}", UiText.Plain(info), UiText.Plain(placement)));
        tray.HelpText.ShouldBe(PartTray.PickHelp);
    }

    [Fact]
    public void Create_WithAComingLaterPart_PicksNothing()
    {
        var tray = PartTray.Create(BuildPart.Wheel);

        tray.Groups.SelectMany(group => group.Rows).ShouldNotContain(row => row.State == PartTrayRowState.Selected);
        tray.PickedInfo.ShouldBeNull();
        tray.HelpText.ShouldBe(PartTray.PickHelp);
    }

    [Fact]
    public void Create_OnALockedCreation_ShowsNoPick()
    {
        var tray = PartTray.Create(BuildPart.Servo, creationLocked: true);

        tray.Groups.SelectMany(group => group.Rows).Single(row => row.Part == BuildPart.Servo).State.ShouldBe(PartTrayRowState.CreationLocked);
        tray.PickedInfo.ShouldBeNull();
        tray.HelpText.ShouldBe(PartTray.CreationLockedHelp);
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
    public void SensorsTab_HasTheAccelerometerAndCameraAvailable_AndTheRestComingLater()
    {
        var sensors = PartTray.Groups()[1].Rows;

        sensors.Select(row => (row.Part, row.State)).ShouldBe(
            [
                (BuildPart.Accelerometer, PartTrayRowState.Available),
                (BuildPart.Camera, PartTrayRowState.Available),
                (BuildPart.TouchSensor, PartTrayRowState.ComingLater),
                (BuildPart.Pulse, PartTrayRowState.ComingLater),
            ]);
    }

    [Fact]
    public void Rows_NotYetImplemented_AreComingLater()
    {
        var rows = PartTray.Groups().SelectMany(group => group.Rows).ToList();

        rows.Where(row => row.Part is not BuildPart.Accelerometer and not BuildPart.Camera and not BuildPart.Servo).ShouldAllBe(row =>
            row.State == PartTrayRowState.ComingLater && !row.IsAvailable);
    }

    [Fact]
    public void ComingLaterRows_SayWhichVersionBringsThem()
    {
        PartTray.Groups().SelectMany(group => group.Rows).Select(row => (row.Part, row.Version)).ShouldBe([
            (BuildPart.Servo, null),
            (BuildPart.Stepper, "0.14.3"),
            (BuildPart.VelocityMotor, "0.14.2"),
            (BuildPart.Brake, "0.14.2"),
            (BuildPart.Wheel, "0.14.1"),
            (BuildPart.Accelerometer, null),
            (BuildPart.Camera, null),
            (BuildPart.TouchSensor, "0.14.1"),
            (BuildPart.Pulse, "0.14.4"),
            (BuildPart.Battery, "0.18.0"),
            (BuildPart.Generator, "0.18.0"),
            (BuildPart.FuelTank, "0.18.0"),
        ]);
        BuildLinkList.Create(BuildLink.Beam).Rows.Select(row => (row.Link, row.Version)).ShouldBe([
            (BuildLink.Beam, null),
            (BuildLink.Piston, null),
            (BuildLink.Spring, null),
            (BuildLink.Wing, "0.18.0"),
        ]);
    }

    [Fact]
    public void ATapOnAComingLaterRow_NamesThePartAndItsVersion()
    {
        PartTray.ComingLaterReason(BuildPart.Stepper).ShouldBe(UiText.Format("{0} comes in version {1}", UiText.Plain("Stepper"), "0.14.3"));
        PartTray.ComingLaterReason(BuildPart.Battery).ShouldBe(UiText.Format("{0} comes in version {1}", UiText.Plain("Battery"), "0.18.0"));
        BuildLinkList.ComingLaterReason(BuildLink.Wing).ShouldBe(UiText.Format("{0} comes in version {1}", UiText.Plain("Wing"), "0.18.0"));
        Should.Throw<ArgumentOutOfRangeException>(() => PartTray.ComingLaterReason(BuildPart.Servo));
        Should.Throw<ArgumentOutOfRangeException>(() => BuildLinkList.ComingLaterReason(BuildLink.Beam));
    }

    [Fact]
    public void ALockedCreationsTray_KeepsTheComingLaterRowsAndTheirVersions()
    {
        var rows = PartTray.Create(picked: null, creationLocked: true).Groups.SelectMany(group => group.Rows).ToList();
        rows.Where(row => row.State == PartTrayRowState.ComingLater).Select(row => row.Version).ShouldAllBe(version => version != null);
        rows.Count(row => row.State == PartTrayRowState.ComingLater).ShouldBe(9);
    }

    [Fact]
    public void LinkList_ShowsBeamPickedWithWingLocked()
    {
        var list = BuildLinkList.Create(BuildLink.Beam);

        list.PickedInfo.ShouldBe(UiText.Format("{0}\n{1}", UiText.Plain("A rigid rod."), UiText.Plain("Drag from joint to joint to add it.")));
        list.HelpText.ShouldBe(UiText.Plain("Tap a link to pick it."));
        list.Rows.Select(row => (row.Link, row.Name, row.State)).ShouldBe([
            (BuildLink.Beam, UiText.Plain("Beam"), LinkListRowState.Selected),
            (BuildLink.Piston, UiText.Plain("Piston"), LinkListRowState.Rest),
            (BuildLink.Spring, UiText.Plain("Spring"), LinkListRowState.Rest),
            (BuildLink.Wing, UiText.Plain("Wing"), LinkListRowState.Locked)]);
    }

    [Fact]
    public void LinkList_InfoFollowsPickedLink()
    {
        BuildLinkList.Create(BuildLink.Piston).PickedInfo.ShouldBe(UiText.Format("{0}\n{1}", UiText.Plain("Extends and retracts."), BuildLinkList.DrawHelp));
    }

    [Fact]
    public void LinkList_WithNothingPicked_SelectsNoRow_AndShowsNoInfo()
    {
        var list = BuildLinkList.Create(picked: null);

        list.PickedInfo.ShouldBeNull();
        list.HelpText.ShouldBe(BuildLinkList.HelpText);
        list.Rows.ShouldNotContain(row => row.State == LinkListRowState.Selected);
    }

    private static UiText[] Plain(params string[] messages) => [.. messages.Select(UiText.Plain)];
}
