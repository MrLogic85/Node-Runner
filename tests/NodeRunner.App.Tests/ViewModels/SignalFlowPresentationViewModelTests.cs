using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class SignalFlowPresentationViewModelTests
{
    // An Accelerometer (2 inputs) and a 5-ray Camera (6), with no driven parts.
    private static readonly CreatureDef _eightInputs = new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0)), new NodeDef(3, new Vector2D(4, 0))],
        [new BeamDef(5, 1, 2), new BeamDef(6, 2, 3)],
        [new SensorDef(8, 5, SensorKind.Accelerometer), new SensorDef(9, 6, SensorKind.Camera, "Eye", rays: 5)]);

    [Fact]
    public void Update_ReadsTheDistanceAndSpeed_AndNotifiesOnlyWhenTheCardChanged()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.Update(double.NaN, double.NaN, double.NaN);

        (viewModel.DistanceReadout, viewModel.SpeedReadout, viewModel.TrackEnd).ShouldBe((null, null, null));
        changed.ShouldBeEmpty();

        viewModel.Update(4225, 120, 5000);

        viewModel.DistanceReadout.ShouldBe(UiText.Format("{0} m", new FixedNumber(42.3, 1)));
        viewModel.SpeedReadout.ShouldBe(UiText.Format("{0} m/s", new FixedNumber(1.2, 1)));
        viewModel.DistanceTorn.ShouldBeFalse();
        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.DistanceReadout)]);
        changed.Clear();

        viewModel.Update(4225, 121, 5000);

        changed.ShouldBeEmpty("The card reads the same.");

        viewModel.Update(4226, 121, 5000);

        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.TrackFill)], "Only the strip moved.");
        changed.Clear();

        viewModel.Update(4225, -80, 5000);

        viewModel.SpeedReadout.ShouldBe(UiText.Format("{0} m/s", new FixedNumber(-0.8, 1)));

        viewModel.Update(4225, -4, 5000);

        viewModel.SpeedReadout.ShouldBe(UiText.Format("{0} m/s", new FixedNumber(0, 1)), "−0.04 m/s reads 0.0, not −0.0.");
        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.DistanceReadout), nameof(SignalFlowPresentationViewModel.DistanceReadout)]);
    }

    // The full length is the farther of the best ever (none counts as 0 m) and the distance, so
    // the scale follows a try past the best and returns to it when the try falls back; the fill stays in 0…1.
    [Theory]
    [InlineData(double.NaN, 1200, 1, 0, 1200)]
    [InlineData(0, 1200, 1, 0, 1200)]
    [InlineData(2000, 500, 0.25, 1, 2000)]
    [InlineData(2000, 4000, 1, 0.5, 4000)]
    [InlineData(2000, 1000, 0.5, 1, 2000)]
    [InlineData(double.NaN, 0, 0, 0, 0)]
    [InlineData(2000, -100, 0, 1, 2000)]
    public void Update_ScalesTheTrackToTheFartherOfTheBestAndTheDistance(double best, double distance, double fill, double marker, double full)
    {
        var viewModel = new SignalFlowPresentationViewModel();

        viewModel.Update(distance, 0, best);

        (viewModel.TrackFill, viewModel.TrackMarker).ShouldBe((fill, marker));
        viewModel.TrackEnd.ShouldBe(Metres.WithUnit(full));
    }

    [Fact]
    public void TrackEndRoom_ReadsTheLongestTrackEndTheCardKeepsRoomFor() =>
        SignalFlowPresentationViewModel.TrackEndRoom.ShouldBe(UiText.Format("{0} m", new FixedNumber(999.9, 1)));

    // A tear freezes the distance, says "Crash" and hides the speed; with no try the card has no
    // body, and a new try reads plainly again.
    [Fact]
    public void Update_AfterATear_SaysCrash_AndHidesTheSpeed()
    {
        var viewModel = new SignalFlowPresentationViewModel();

        viewModel.Update(340, 50, 1000, crashed: true);

        viewModel.DistanceReadout.ShouldBe(UiText.Format("Crash {0}", UiText.Format("{0} m", new FixedNumber(3.4, 1))));
        viewModel.DistanceTorn.ShouldBeTrue();
        viewModel.SpeedReadout.ShouldBeNull();
        (viewModel.TrackFill, viewModel.TrackMarker).ShouldBe((0.34, 1));

        viewModel.Update(double.NaN, double.NaN, 1000, crashed: true);

        (viewModel.DistanceReadout, viewModel.SpeedReadout, viewModel.TrackEnd).ShouldBe((null, null, null), "No try runs.");
        viewModel.DistanceTorn.ShouldBeFalse("There is nothing to show in danger.");

        viewModel.Update(100, 50, 1000);

        viewModel.DistanceReadout.ShouldBe(UiText.Format("{0} m", new FixedNumber(1, 1)));
        viewModel.DistanceTorn.ShouldBeFalse();
        viewModel.SpeedReadout.ShouldNotBeNull();
    }

    // Past the bars that fit, the first inputs in port order that fit beside the chip get bars, each with its port's range, and the chip counts the rest.
    [Fact]
    public void ShowPorts_WithMoreInputsThanFit_BarsTheFirstInPortOrder_AndCountsTheRest()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        var values = NetworkPortValues.Sample(PortedCreature.Layout, PortedCreature.Network(), PortedCreature.Readings());

        viewModel.ShowPorts(PortedCreature.Def, values);

        viewModel.InputsNote.ShouldBe(UiText.Number(PortedCreature.Layout.Inputs.Count));
        viewModel.InputBars.ShouldBe(PortedCreature.Ports.Take(SignalFlowPresentationViewModel.InputBarsBesideChip).Select(entry => entry.Range));
        viewModel.InputValues.ShouldBe(values.Inputs!.Take(SignalFlowPresentationViewModel.InputBarsBesideChip));
        viewModel.MoreInputs.ShouldBe(UiText.Format("+{0}", PortedCreature.Layout.Inputs.Count - SignalFlowPresentationViewModel.InputBarsBesideChip));
    }

    // The chip works like an ellipsis: every input gets a bar while they all fit, and only one more
    // gives the last bars' places to the chip.
    [Theory]
    [InlineData(1, 11, null)]
    [InlineData(2, 8, 4)]
    public void ShowPorts_BarsEveryInputThatFits_AndMakesRoomForTheChipOnlyPastThat(int touchSensors, int bars, int? more)
    {
        var viewModel = new SignalFlowPresentationViewModel();
        var creature = TenInputsAnd(touchSensors);

        viewModel.ShowPorts(creature, NetworkPortValues.Sample(NetworkPorts.Of(creature), null, []));

        viewModel.InputsNote.ShouldBe(UiText.Number(10 + touchSensors));
        viewModel.InputBars.Count.ShouldBe(bars);
        viewModel.MoreInputs.ShouldBe(more is { } rest ? UiText.Format("+{0}", rest) : null);
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
        viewModel.InputBars.Count.ShouldBe(SignalFlowPresentationViewModel.InputBarsBesideChip);
        viewModel.InputValues.ShouldAllBe(value => double.IsNaN(value));
        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.InputBars), nameof(SignalFlowPresentationViewModel.OutputDials), nameof(SignalFlowPresentationViewModel.NetworkSize)]);
        changed.Clear();

        viewModel.ShowPorts(PortedCreature.Def, live);
        viewModel.ShowPorts(PortedCreature.Def, live);

        viewModel.InputValues.ShouldBe(readings.Take(SignalFlowPresentationViewModel.InputBarsBesideChip));
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
        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.InputBars), nameof(SignalFlowPresentationViewModel.OutputDials), nameof(SignalFlowPresentationViewModel.NetworkSize)]);
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

    // The Network card counts what training tunes: a weight per input and output pair and a bias per
    // output. It is notified only when the port layout changes, and has no size with no creature.
    [Fact]
    public void ShowPorts_SizesTheNetworkByItsLearnableParameters()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        var inputs = PortedCreature.Layout.Inputs.Count;
        var live = NetworkPortValues.Sample(PortedCreature.Layout, PortedCreature.Network(), PortedCreature.Readings());

        viewModel.ShowPorts(PortedCreature.Def, NetworkPortValues.Sample(PortedCreature.Layout, null, []));

        viewModel.NetworkSize.ShouldBe(UiText.Counted("{0} parameter", "{0} parameters", (inputs * 7) + 7));
        changed.ShouldContain(nameof(SignalFlowPresentationViewModel.NetworkSize));
        changed.Clear();

        viewModel.ShowPorts(PortedCreature.Def, live);

        changed.ShouldNotContain(nameof(SignalFlowPresentationViewModel.NetworkSize));

        viewModel.ShowPorts(_eightInputs, NetworkPortValues.Sample(NetworkPorts.Of(_eightInputs), null, []));

        viewModel.NetworkSize.ShouldBe(UiText.Counted("{0} parameter", "{0} parameters", 0), "With nothing to drive there is nothing to tune.");

        viewModel.ShowPorts(null, NetworkPortValues.Empty);

        viewModel.NetworkSize.ShouldBeNull();
    }

    // Exact below the threshold, so a part's parameters add up visibly; past it at most three
    // significant digits, with no ".0" once the rounded value reaches 100.
    [Theory]
    [InlineData(SignalFlowPresentationViewModel.ShortenedParametersFrom - 1, null, 0, 0)]
    [InlineData(SignalFlowPresentationViewModel.ShortenedParametersFrom, "{0}k parameters", 10.0, 1)]
    [InlineData(99_949, "{0}k parameters", 99.9, 1)]
    [InlineData(99_950, "{0}k parameters", 100, 0)]
    [InlineData(999_499, "{0}k parameters", 999, 0)]
    [InlineData(999_500, "{0}M parameters", 1.0, 1)]
    [InlineData(1_234_567, "{0}M parameters", 1.2, 1)]
    public void Parameters_IsExactBelowTheThreshold_AndShortenedPastIt(int count, string? shortened, double shown, int decimals)
    {
        var text = SignalFlowPresentationViewModel.Parameters(count);

        text.ShouldBe(shortened is null
            ? UiText.Counted("{0} parameter", "{0} parameters", count)
            : UiText.Format(shortened, new FixedNumber(shown, decimals)));
    }

    // A creature with no sensors or driven parts has stages that say so; with no creature, or once
    // it has ports, they do not. The first creature brings no new ports, yet it is notified.
    [Fact]
    public void ShowPorts_SaysAStageHasNone_OnlyForACreatureWithNoPorts()
    {
        var viewModel = new SignalFlowPresentationViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        var bare = new CreatureDef([new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0)), new BeamDef(3, 1, 2)]);

        viewModel.ShowPorts(null, NetworkPortValues.Empty);

        viewModel.HasNoInputs.ShouldBeFalse("No creature is shown.");
        viewModel.HasNoOutputs.ShouldBeFalse("No creature is shown.");
        changed.ShouldBeEmpty();

        viewModel.ShowPorts(bare, NetworkPortValues.Sample(NetworkPorts.Of(bare), null, []));

        viewModel.HasNoInputs.ShouldBeTrue();
        viewModel.HasNoOutputs.ShouldBeTrue();
        viewModel.InputsNote.ShouldBeNull();
        viewModel.OutputsNote.ShouldBeNull();
        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.InputBars), nameof(SignalFlowPresentationViewModel.OutputDials), nameof(SignalFlowPresentationViewModel.NetworkSize)]);
        changed.Clear();

        viewModel.ShowPorts(_eightInputs, NetworkPortValues.Sample(NetworkPorts.Of(_eightInputs), null, []));

        viewModel.HasNoInputs.ShouldBeFalse();
        viewModel.HasNoOutputs.ShouldBeTrue("The sensors drive nothing.");
        changed.ShouldBe([nameof(SignalFlowPresentationViewModel.InputBars)]);

        viewModel.ShowPorts(null, NetworkPortValues.Empty);

        viewModel.HasNoOutputs.ShouldBeFalse("No creature is shown.");
    }

    // An Accelerometer (2 inputs), a 5-ray Camera (6) and a 1-ray Camera (2), with a Touch sensor (1) on each of the first nodes.
    private static CreatureDef TenInputsAnd(int touchSensors)
    {
        List<IPartDef> parts =
        [
            new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0)), new NodeDef(3, new Vector2D(4, 0)), new NodeDef(4, new Vector2D(6, 0)),
            new BeamDef(5, 1, 2), new BeamDef(6, 2, 3), new BeamDef(7, 3, 4),
            new SensorDef(8, 5, SensorKind.Accelerometer), new SensorDef(9, 6, SensorKind.Camera, rays: 5), new SensorDef(10, 7, SensorKind.Camera, rays: 1),
        ];
        parts.AddRange(Enumerable.Range(1, touchSensors).Select(node => new TouchSensorDef(10 + node, node)));
        return new CreatureDef(parts);
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
