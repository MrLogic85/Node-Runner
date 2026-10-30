using NodeRunner.App.ViewModels;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class PartTrayTests
{
    // The opening tab is the first with an available part; a coming-later row names the version that brings it.
    [Fact]
    public void Groups_ListEveryPartOnce_InReferenceOrder_WithWhatIsComingLater()
    {
        PartTray.Groups().Select(group => group.Name).ShouldBe(Plain("Moving parts", "Sensors", "Blocks"));
        PartTray.Groups().Select(group => group.Rows.Select(row => row.Name).ToArray()).ShouldBe(
        [
            Plain("Servo", "Stepper", "Motor", "Brake", "Wheel", "Rubber"),
            Plain("Accelerometer", "Camera", "Touch sensor", "Angle sensor", "Pulse", "Memory cell"),
            Plain("Battery", "Generator", "Fuel tank"),
        ]);
        var rows = PartTray.Groups().SelectMany(group => group.Rows).ToArray();
        rows.Select(row => row.Part).ShouldBe(Enum.GetValues<BuildPart>(), ignoreOrder: true);
        rows.Where(row => row.State == PartTrayRowState.ComingLater).Select(row => (row.Part, row.Version)).ShouldBe([
            (BuildPart.Pulse, "0.14.5"),
            (BuildPart.Battery, "0.18.0"),
            (BuildPart.Generator, "0.18.0"),
            (BuildPart.FuelTank, "0.18.0"),
        ]);
        rows.Where(row => row.State != PartTrayRowState.ComingLater).ShouldAllBe(row => row.State == PartTrayRowState.Available && row.Version == null);
        PartTray.OpeningGroup().ShouldBe(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(BuildPart.Pulse)]
    public void Create_WithNothingOrAComingLaterPartPicked_PicksNothingAndSaysHowToPick(BuildPart? picked)
    {
        var tray = PartTray.Create(picked);

        tray.HelpText.ShouldBe(UiText.Plain("Tap a part to pick it, or drag it onto the creature."));
        tray.PickedInfo.ShouldBeNull();
        tray.Groups.SelectMany(group => group.Rows).ShouldNotContain(row => row.State == PartTrayRowState.Selected);
    }

    [Theory]
    [InlineData(BuildPart.Servo, "Turns and holds an angle.", "Drag onto a joint, or tap a joint to place it.")]
    [InlineData(BuildPart.Stepper, "A motor that ticks like a clock.", "Drag onto a joint, or tap a joint to place it.")]
    [InlineData(BuildPart.VelocityMotor, "Spins round and round.", "Drag onto a joint, or tap a joint to place it.")]
    [InlineData(BuildPart.Brake, "Slows or locks its joint.", "Drag onto a joint, or tap a joint to place it.")]
    [InlineData(BuildPart.Wheel, "Rolls freely, a motor can drive it.", "Drag onto a joint, or tap a joint to place it.")]
    [InlineData(BuildPart.Accelerometer, "Measures a beam's acceleration.", "Drag onto a beam, or tap a beam to place it.")]
    [InlineData(BuildPart.Camera, "Measures distance to the ground.", "Drag onto a beam, or tap a beam to place it.")]
    [InlineData(BuildPart.TouchSensor, "Signals when a joint is touching the ground.", "Drag onto a joint, or tap a joint to place it.")]
    [InlineData(BuildPart.Rubber, "Doubles a joint's friction.", "Drag onto a joint, or tap a joint to place it.")]
    [InlineData(BuildPart.AngleSensor, "Measures a joint's angle and velocity.", "Drag onto a joint, or tap a joint to place it.")]
    public void Create_WithAPickedPart_SelectsItsRow_AndSaysUnderItWhatItDoesAndWhereItGoes(BuildPart part, string info, string placement)
    {
        var tray = PartTray.Create(part);

        tray.Groups.SelectMany(group => group.Rows).Where(row => row.State == PartTrayRowState.Selected)
            .Select(row => row.Part).ShouldBe([part]);
        tray.Groups.SelectMany(group => group.Rows).Single(row => row.Part == part).IsAvailable.ShouldBeTrue();
        tray.PickedInfo.ShouldBe(UiText.Format("{0}\n{1}", UiText.Plain(info), UiText.Plain(placement)));
        tray.HelpText.ShouldBe(PartTray.PickHelp);
    }

    // A Wheel and a Rubber have no network ports, so a locked creation still takes one.
    [Theory]
    [InlineData(BuildPart.Servo, false)]
    [InlineData(BuildPart.Wheel, true)]
    [InlineData(BuildPart.Rubber, true)]
    public void Create_OnALockedCreation_PicksOnlyAPartWithoutNetworkPorts_AndKeepsTheComingLaterRows(BuildPart part, bool picks)
    {
        var tray = PartTray.Create(part, creationLocked: true);

        var rows = tray.Groups.SelectMany(group => group.Rows).ToArray();
        rows.Single(row => row.Part == part).State.ShouldBe(picks ? PartTrayRowState.Selected : PartTrayRowState.CreationLocked);
        rows.Single(row => row.Part == BuildPart.Servo).State.ShouldBe(PartTrayRowState.CreationLocked);
        (tray.PickedInfo is not null).ShouldBe(picks);
        tray.HelpText.ShouldBe(UiText.Plain("Unlock to add parts the network uses."));
        PartTray.CanPick(part, creationLocked: true).ShouldBe(picks);
        rows.Where(row => row.State == PartTrayRowState.ComingLater).Select(row => row.Version).ShouldAllBe(version => version != null);
        rows.Count(row => row.State == PartTrayRowState.ComingLater).ShouldBe(4);
    }

    [Fact]
    public void ATapOnAComingLaterRow_NamesThePartAndItsVersion()
    {
        PartTray.ComingLaterReason(BuildPart.Pulse).ShouldBe(UiText.Format("{0} comes in version {1}", UiText.Plain("Pulse"), "0.14.5"));
        PartTray.ComingLaterReason(BuildPart.Battery).ShouldBe(UiText.Format("{0} comes in version {1}", UiText.Plain("Battery"), "0.18.0"));
        BuildLinkList.ComingLaterReason(BuildLink.Wing).ShouldBe(UiText.Format("{0} comes in version {1}", UiText.Plain("Wing"), "0.18.0"));
        Should.Throw<ArgumentOutOfRangeException>(() => PartTray.ComingLaterReason(BuildPart.Servo));
        Should.Throw<ArgumentOutOfRangeException>(() => BuildLinkList.ComingLaterReason(BuildLink.Beam));
    }

    [Fact]
    public void LinkList_ShowsThePickedLinksInfo_WithWingComingLater()
    {
        var list = BuildLinkList.Create(BuildLink.Beam);

        list.PickedInfo.ShouldBe(UiText.Format("{0}\n{1}", UiText.Plain("A rigid beam."), UiText.Plain("Drag from joint to joint to add it.")));
        list.HelpText.ShouldBe(UiText.Plain("Tap a link to pick it."));
        list.Rows.Select(row => (row.Link, row.Name, row.State, row.Version)).ShouldBe([
            (BuildLink.Beam, UiText.Plain("Beam"), LinkListRowState.Selected, null),
            (BuildLink.Piston, UiText.Plain("Piston"), LinkListRowState.Rest, null),
            (BuildLink.Spring, UiText.Plain("Spring"), LinkListRowState.Rest, null),
            (BuildLink.Wing, UiText.Plain("Wing"), LinkListRowState.Locked, "0.18.0")]);
        BuildLinkList.Create(BuildLink.Piston).PickedInfo.ShouldBe(UiText.Format("{0}\n{1}", UiText.Plain("Extends and retracts."), BuildLinkList.DrawHelp));
        var none = BuildLinkList.Create(picked: null);
        none.PickedInfo.ShouldBeNull();
        none.HelpText.ShouldBe(BuildLinkList.HelpText);
        none.Rows.ShouldNotContain(row => row.State == LinkListRowState.Selected);
    }

    private static UiText[] Plain(params string[] messages) => [.. messages.Select(UiText.Plain)];
}
