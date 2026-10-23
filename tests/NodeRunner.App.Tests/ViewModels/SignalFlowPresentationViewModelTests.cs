using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class SignalFlowPresentationViewModelTests
{
    // An Accelerometer (2 inputs) and a 5-ray Camera (6): exactly as many inputs as there are bars.
    private static readonly CreatureDef _eightInputs = new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0)), new NodeDef(3, new Vector2D(4, 0))],
        [new BeamDef(5, 1, 2), new BeamDef(6, 2, 3)],
        [new SensorDef(8, 5, SensorKind.Accelerometer), new SensorDef(9, 6, SensorKind.Camera, "Eye", rays: 5)]);

    [Fact]
    public void Update_ReadsTheDistance_AndNotifiesOnlyWhenItChanged()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.Update(double.NaN);

        viewModel.DistanceReadout.ShouldBeNull();
        changed.ShouldBeEmpty();

        viewModel.Update(4225);

        viewModel.DistanceReadout.ShouldBe(UiText.Format("{0} m", new FixedNumber(42.3, 1)));
        viewModel.DistanceTorn.ShouldBeFalse();
        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.DistanceReadout)]);
        changed.Clear();

        viewModel.Update(4226);

        changed.ShouldBeEmpty("The readout rounds to the same text.");

        viewModel.Update(340, crashed: true);

        viewModel.DistanceReadout.ShouldBe(UiText.Format("Crash {0}", UiText.Format("{0} m", new FixedNumber(3.4, 1))));
        viewModel.DistanceTorn.ShouldBeTrue();
        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.DistanceReadout)]);

        viewModel.Update(double.NaN, crashed: true);

        viewModel.DistanceReadout.ShouldBeNull("No try runs.");
        viewModel.DistanceTorn.ShouldBeFalse("There is nothing to show in danger.");

        viewModel.Update(340, crashed: true);
        viewModel.Update(100);

        viewModel.DistanceReadout.ShouldBe(UiText.Format("{0} m", new FixedNumber(1, 1)));
        viewModel.DistanceTorn.ShouldBeFalse();
    }

    // Past eight inputs the first eight in port order get bars, each with its port's range, and a chip counts the rest.
    [Fact]
    public void ShowPorts_BarsTheFirstEightInPortOrder_AndCountsTheRest()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        var values = NetworkPortValues.Sample(PortedCreature.Layout, PortedCreature.Network(), PortedCreature.Readings());

        viewModel.ShowPorts(PortedCreature.Def, values);

        viewModel.InputsNote.ShouldBe(UiText.Number(PortedCreature.Layout.Inputs.Count));
        viewModel.InputBars.ShouldBe(PortedCreature.Ports.Take(SignalFlowPresentationViewModel.MaxInputBars).Select(entry => entry.Range));
        viewModel.InputValues.ShouldBe(values.Inputs!.Take(SignalFlowPresentationViewModel.MaxInputBars));
        viewModel.MoreInputs.ShouldBe(UiText.Format("+{0}", PortedCreature.Layout.Inputs.Count - SignalFlowPresentationViewModel.MaxInputBars));
    }

    [Fact]
    public void ShowPorts_WithExactlyEightInputs_ShowsEveryBarAndNoChip()
    {
        var viewModel = new SignalFlowPresentationViewModel();

        viewModel.ShowPorts(_eightInputs, NetworkPortValues.Sample(NetworkPorts.Of(_eightInputs), null, []));

        viewModel.InputsNote.ShouldBe(UiText.Number(8));
        viewModel.InputBars.Count.ShouldBe(8);
        viewModel.MoreInputs.ShouldBeNull();
    }

    // The bars are rebuilt only for a new layout; a sample notifies only when a value changed, and
    // with no live network the bars keep their places and the count but show no values.
    [Fact]
    public void ShowPorts_RebuildsOnlyForANewLayout_AndEmptiesTheBarsWithNoLiveNetwork()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        var readings = PortedCreature.Readings();
        var live = NetworkPortValues.Sample(PortedCreature.Layout, PortedCreature.Network(), readings);

        viewModel.ShowPorts(PortedCreature.Def, NetworkPortValues.Sample(PortedCreature.Layout, null, []));

        viewModel.InputsNote.ShouldBe(UiText.Number(PortedCreature.Layout.Inputs.Count));
        viewModel.InputBars.Count.ShouldBe(SignalFlowPresentationViewModel.MaxInputBars);
        viewModel.InputValues.ShouldAllBe(value => double.IsNaN(value));
        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.InputBars), nameof(SignalFlowPresentationViewModel.OutputDials)]);
        changed.Clear();

        viewModel.ShowPorts(PortedCreature.Def, live);
        viewModel.ShowPorts(PortedCreature.Def, live);

        viewModel.InputValues.ShouldBe(readings.Take(SignalFlowPresentationViewModel.MaxInputBars));
        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.InputValues), nameof(SignalFlowPresentationViewModel.OutputValues)]);
        changed.Clear();

        viewModel.ShowPorts(_eightInputs, NetworkPortValues.Sample(NetworkPorts.Of(_eightInputs), null, []));

        changed.ShouldContain(nameof(SignalFlowPresentationViewModel.InputBars));

        viewModel.ShowPorts(null, NetworkPortValues.Empty);

        viewModel.InputsNote.ShouldBeNull();
        viewModel.InputBars.ShouldBeEmpty();
        viewModel.InputValues.ShouldBeEmpty();
        viewModel.MoreInputs.ShouldBeNull();
    }

    // A −1…1 input fills from the middle and a 0…1 input from the bottom, as in the part callout.
    [Fact]
    public void ShowPorts_GivesEachBarItsPortsRange()
    {
        var viewModel = new SignalFlowPresentationViewModel();

        viewModel.ShowPorts(_eightInputs, NetworkPortValues.Sample(NetworkPorts.Of(_eightInputs), null, []));

        viewModel.InputBars.ShouldBe([PortRange.MinusOneToOne, PortRange.MinusOneToOne, .. Enumerable.Repeat(PortRange.ZeroToOne, 6)]);
    }

    // Every driven part kind gets one dial at its drive, in port order, with that port's range and live value.
    [Fact]
    public void ShowPorts_DialsEachDrivenPartsDrive_AndCountsTheOutputs()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        var values = NetworkPortValues.Sample(PortedCreature.Layout, PortedCreature.Network(), PortedCreature.Readings());
        var drives = PortedCreature.Layout.Outputs
            .Select((port, index) => (port, index))
            .Where(output => output.port.Signal != PortSignal.Strength)
            .ToArray();

        viewModel.ShowPorts(PortedCreature.Def, values);

        drives.Select(drive => drive.port.PartId).ShouldBe([PortedCreature.Servo, PortedCreature.Piston, PortedCreature.VelocityMotor, PortedCreature.Stepper], ignoreOrder: true);
        viewModel.OutputsNote.ShouldBe(UiText.Number(7));
        viewModel.OutputDials.ShouldBe(drives.Select(drive => PortedCreature.Ports.Single(entry => entry.Port == drive.port).Range));
        viewModel.OutputValues.ShouldBe(drives.Select(drive => values.Outputs![drive.index]));
        viewModel.MoreOutputs.ShouldBeNull("Exactly four driven parts all get a dial.");
    }

    // Past four parts the last dial makes room for a chip counting the rest.
    [Fact]
    public void ShowPorts_WithFiveDrivenParts_DialsThree_AndCountsTheRestInAChip()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        var creature = Pistons(5);

        viewModel.ShowPorts(creature, NetworkPortValues.Sample(NetworkPorts.Of(creature), null, []));

        viewModel.OutputDials.Count.ShouldBe(3);
        viewModel.OutputsNote.ShouldBe(UiText.Number(10));
        viewModel.MoreOutputs.ShouldBe(UiText.Format("+{0}", 2));
    }

    // The dials are rebuilt only for a new output layout, and with no live network they keep their
    // places and the count but show no values; a creature with no driven parts has neither.
    [Fact]
    public void ShowPorts_RebuildsTheDialsOnlyForANewLayout_AndEmptiesThemWithNoLiveNetwork()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        var live = NetworkPortValues.Sample(PortedCreature.Layout, PortedCreature.Network(), PortedCreature.Readings());

        viewModel.ShowPorts(PortedCreature.Def, NetworkPortValues.Sample(PortedCreature.Layout, null, []));

        viewModel.OutputsNote.ShouldBe(UiText.Number(7));
        viewModel.OutputDials.Count.ShouldBe(4);
        viewModel.OutputValues.ShouldAllBe(value => double.IsNaN(value));
        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.InputBars), nameof(SignalFlowPresentationViewModel.OutputDials)]);
        changed.Clear();

        viewModel.ShowPorts(PortedCreature.Def, live);
        viewModel.ShowPorts(PortedCreature.Def, live);

        viewModel.OutputValues.ShouldAllBe(value => !double.IsNaN(value));
        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.InputValues), nameof(SignalFlowPresentationViewModel.OutputValues)]);
        changed.Clear();

        viewModel.ShowPorts(PortedCreature.Def, NetworkPortValues.Sample(PortedCreature.Layout, null, []));

        viewModel.OutputValues.ShouldAllBe(value => double.IsNaN(value));
        changed.ShouldContain(nameof(SignalFlowPresentationViewModel.OutputValues));
        changed.ShouldNotContain(nameof(SignalFlowPresentationViewModel.OutputDials));

        viewModel.ShowPorts(_eightInputs, NetworkPortValues.Sample(NetworkPorts.Of(_eightInputs), null, []));

        viewModel.OutputsNote.ShouldBeNull();
        viewModel.OutputDials.ShouldBeEmpty();
        viewModel.OutputValues.ShouldBeEmpty();
        viewModel.MoreOutputs.ShouldBeNull();
    }

    // Pistons side by side, each spanning its own two nodes.
    private static CreatureDef Pistons(int count)
    {
        var parts = new List<IPartDef>();
        for (var piston = 0; piston < count; piston++)
        {
            var node = (piston * 2) + 1;
            parts.Add(new NodeDef(node, new Vector2D(node * 2, 0)));
            parts.Add(new NodeDef(node + 1, new Vector2D(node * 2, 2)));
            parts.Add(new PistonDef(100 + piston, node, node + 1));
        }

        return new CreatureDef(parts);
    }
}
