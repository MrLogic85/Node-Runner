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

    // Each change is notified by name, so a view applies only what changed.
    [Fact]
    public void Update_NotesTheOutputsAndDistance_AndNotifiesOnlyWhatChanged()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.Update(0, double.NaN);

        viewModel.OutputsNote.ShouldBeNull();
        viewModel.DistanceReadout.ShouldBeNull();
        changed.ShouldBeEmpty();

        viewModel.Update(parts: 2, distance: 4225);

        viewModel.OutputsNote.ShouldBe(Parts(2));
        viewModel.DistanceReadout.ShouldBe(UiText.Format("{0} m", new FixedNumber(42.3, 1)));
        viewModel.DistanceTorn.ShouldBeFalse();
        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.OutputsNote), nameof(SignalFlowPresentationViewModel.DistanceReadout)]);
        changed.Clear();

        viewModel.Update(2, 4226);
        viewModel.Update(2, 340);

        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.DistanceReadout)]);
        changed.Clear();

        viewModel.Update(2, 340, crashed: true);

        viewModel.DistanceReadout.ShouldBe(UiText.Format("Crash {0}", UiText.Format("{0} m", new FixedNumber(3.4, 1))));
        viewModel.DistanceTorn.ShouldBeTrue();
        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.DistanceReadout)]);

        viewModel.Update(2, double.NaN, crashed: true);

        viewModel.DistanceReadout.ShouldBeNull("No try runs.");
        viewModel.DistanceTorn.ShouldBeFalse("There is nothing to show in danger.");

        viewModel.Update(2, 340, crashed: true);
        viewModel.Update(2, 100);

        viewModel.DistanceReadout.ShouldBe(UiText.Format("{0} m", new FixedNumber(1, 1)));
        viewModel.DistanceTorn.ShouldBeFalse();
        changed.Clear();

        viewModel.Update(1, 100);

        viewModel.OutputsNote.ShouldBe(Parts(1));
        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.OutputsNote)]);
    }

    [Fact]
    public void Update_WithoutAListener_StillUpdates_AndWithANegativeCountThrows()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        viewModel.Update(1, 100);
        viewModel.OutputsNote.ShouldBe(Parts(1));

        Should.Throw<ArgumentOutOfRangeException>(() => viewModel.Update(-1, 0));
    }

    // Past eight inputs the first eight in port order get bars, each with its port's range, and a chip counts the rest.
    [Fact]
    public void ShowInputs_BarsTheFirstEightInPortOrder_AndCountsTheRest()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        var values = NetworkPortValues.Sample(PortedCreature.Layout, PortedCreature.Network(), PortedCreature.Readings());

        viewModel.ShowInputs(PortedCreature.Def, values);

        viewModel.InputsNote.ShouldBe(UiText.Number(PortedCreature.Layout.Inputs.Count));
        viewModel.InputBars.ShouldBe(PortedCreature.Ports.Take(SignalFlowPresentationViewModel.MaxInputBars).Select(entry => entry.Range));
        viewModel.InputValues.ShouldBe(values.Inputs!.Take(SignalFlowPresentationViewModel.MaxInputBars));
        viewModel.MoreInputs.ShouldBe(UiText.Format("+{0}", PortedCreature.Layout.Inputs.Count - SignalFlowPresentationViewModel.MaxInputBars));
    }

    [Fact]
    public void ShowInputs_WithExactlyEightInputs_ShowsEveryBarAndNoChip()
    {
        var viewModel = new SignalFlowPresentationViewModel();

        viewModel.ShowInputs(_eightInputs, NetworkPortValues.Sample(NetworkPorts.Of(_eightInputs), null, []));

        viewModel.InputsNote.ShouldBe(UiText.Number(8));
        viewModel.InputBars.Count.ShouldBe(8);
        viewModel.MoreInputs.ShouldBeNull();
    }

    // The bars are rebuilt only for a new layout; a sample notifies only when a value changed, and
    // with no live network the bars keep their places and the count but show no values.
    [Fact]
    public void ShowInputs_RebuildsOnlyForANewLayout_AndEmptiesTheBarsWithNoLiveNetwork()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        var readings = PortedCreature.Readings();
        var live = NetworkPortValues.Sample(PortedCreature.Layout, PortedCreature.Network(), readings);

        viewModel.ShowInputs(PortedCreature.Def, NetworkPortValues.Sample(PortedCreature.Layout, null, []));

        viewModel.InputsNote.ShouldBe(UiText.Number(PortedCreature.Layout.Inputs.Count));
        viewModel.InputBars.Count.ShouldBe(SignalFlowPresentationViewModel.MaxInputBars);
        viewModel.InputValues.ShouldAllBe(value => double.IsNaN(value));
        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.InputBars)]);

        viewModel.ShowInputs(PortedCreature.Def, live);
        viewModel.ShowInputs(PortedCreature.Def, live);

        viewModel.InputValues.ShouldBe(readings.Take(SignalFlowPresentationViewModel.MaxInputBars));
        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.InputBars), nameof(SignalFlowPresentationViewModel.InputValues)]);

        viewModel.ShowInputs(_eightInputs, NetworkPortValues.Sample(NetworkPorts.Of(_eightInputs), null, []));

        changed[^1].ShouldBe(nameof(SignalFlowPresentationViewModel.InputBars));

        viewModel.ShowInputs(null, NetworkPortValues.Empty);

        viewModel.InputsNote.ShouldBeNull();
        viewModel.InputBars.ShouldBeEmpty();
        viewModel.InputValues.ShouldBeEmpty();
        viewModel.MoreInputs.ShouldBeNull();
    }

    // A −1…1 input fills from the middle and a 0…1 input from the bottom, as in the part callout.
    [Fact]
    public void ShowInputs_GivesEachBarItsPortsRange()
    {
        var viewModel = new SignalFlowPresentationViewModel();

        viewModel.ShowInputs(_eightInputs, NetworkPortValues.Sample(NetworkPorts.Of(_eightInputs), null, []));

        viewModel.InputBars.ShouldBe([PortRange.MinusOneToOne, PortRange.MinusOneToOne, .. Enumerable.Repeat(PortRange.ZeroToOne, 6)]);
    }

    private static UiText Parts(int count) => UiText.Counted("{0} part", "{0} parts", count);
}
