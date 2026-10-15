using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.ML;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class NetworkFocusPresentationViewModelTests
{
    private static readonly UiText _along = UiText.Plain("Accelerometer: along");
    private static readonly UiText _kneeSpeed = UiText.Plain("Front knee: speed");
    private static readonly UiText _centre = UiText.Plain("Camera: centre");
    private static readonly UiText _rearKnee = UiText.Plain("Rear knee");
    private static readonly UiText _frontKnee = UiText.Plain("Front knee");
    private static readonly UiText _noSelection = UiText.Plain("Tap an input or an output to see what drives what.");

    private static readonly NetworkPortLabels _labels = new([_along, _kneeSpeed, _centre], [_rearKnee, _frontKnee]);

    private static readonly NetworkPortLayout _layout = new(
        [NetworkPort.Input(1, "along"), NetworkPort.Input(2, "speed"), NetworkPort.Input(3, "centre")],
        [NetworkPort.Output(4, "angle", PortSignal.Position), NetworkPort.Output(2, "angle", PortSignal.Position)]);

    // Weights by output, then input: Rear knee <- (0.2, -1.5, 0.9); Front knee <- (1.0, 0.0, -0.1).
    private static NeuralNetwork Network() =>
        NeuralNetwork.FromGenome([3, 2], [0.2, -1.5, 0.9, 1.0, 0.0, -0.1, 0.0, 0.0], Activation.Tanh);

    private static double[] Readings() => [0.5, -0.25, 1];

    private static NetworkPortValues Sample(NeuralNetwork network) => NetworkPortValues.Sample(_layout, network, Readings());

    private static NetworkFocusPresentationViewModel Live(params int[] disabledGenes)
    {
        var viewModel = new NetworkFocusPresentationViewModel();
        viewModel.Configure(_labels, disabledGenes);
        viewModel.Update(Sample(Network()));
        return viewModel;
    }

    [Fact]
    public void Update_ShowsInputsAndOutputsByPortName_WithNothingSelected()
    {
        var viewModel = Live();

        viewModel.HasNetwork.ShouldBeTrue();
        viewModel.Summary.ShouldBe(UiText.Format(
            "{0} → {1}. Solid blue: pushes up. Dashed red: pushes down. Thicker: stronger.",
            UiText.Counted("{0} input", "{0} inputs", 3),
            UiText.Counted("{0} output", "{0} outputs", 2)));
        viewModel.Layers.Select(layer => layer.Title).ShouldBe([UiText.Plain("Inputs"), UiText.Plain("Outputs")]);
        viewModel.Layers[0].Neurons.Select(neuron => neuron.Label).ShouldBe(_labels.Inputs);
        viewModel.Layers[1].Neurons.Select(neuron => neuron.Label).ShouldBe(_labels.Outputs);
        viewModel.Layers[0].Neurons[0].Activation.ShouldBe(0.5);
        viewModel.Layers[1].Neurons[0].Activation.ShouldBe(Math.Tanh((0.2 * 0.5) + (-1.5 * -0.25) + 0.9), 1e-12);
        viewModel.Selected.ShouldBeNull();
        viewModel.SelectionText.ShouldBe(_noSelection);
        viewModel.Layers.SelectMany(layer => layer.Neurons).ShouldAllBe(neuron => !neuron.IsHighlighted);
    }

    [Fact]
    public void Update_DrawsEveryEnabledConnection()
    {
        var viewModel = Live(disabledGenes: 2);

        viewModel.Edges.Count.ShouldBe(5);
        viewModel.Edges.ShouldNotContain(edge => edge.FromNeuronIndex == 2 && edge.ToNeuronIndex == 0);
        var edge = viewModel.Edges.Single(edge => edge.FromNeuronIndex == 1 && edge.ToNeuronIndex == 0);
        edge.Weight.ShouldBe(-1.5);
        edge.Strength.ShouldBe(0.75);
    }

    [Fact]
    public void SelectOutput_NamesTheInputsThatDriveItMost()
    {
        var viewModel = Live();

        viewModel.SelectNeuron(NetworkFocusPresentationViewModel.OutputLayer, 0);

        viewModel.SelectionText.ShouldBe(UiText.Format("{0} is driven most by {1} and {2}.", _rearKnee, _kneeSpeed, _centre));
        viewModel.Layers[1].Neurons[0].IsSelected.ShouldBeTrue();
        viewModel.Layers[0].Neurons.Select(neuron => neuron.IsHighlighted).ShouldBe([false, true, true]);
        viewModel.Edges.Where(edge => edge.IsHighlighted).Select(edge => edge.ToNeuronIndex).ShouldAllBe(output => output == 0);
        viewModel.Edges.Count(edge => edge.IsHighlighted).ShouldBe(3);
    }

    [Fact]
    public void SelectInput_NamesTheOutputsItDrivesMost_SkippingZeroAndDisabledConnections()
    {
        var viewModel = Live();

        viewModel.SelectNeuron(NetworkFocusPresentationViewModel.InputLayer, 1);

        viewModel.SelectionText.ShouldBe(UiText.Format("{0} drives {1} most.", _kneeSpeed, _rearKnee));
        viewModel.Layers[1].Neurons.Select(neuron => neuron.IsHighlighted).ShouldBe([true, false]);
    }

    [Fact]
    public void SelectOutput_WithEveryConnectionDisabled_SaysNothingDrivesIt()
    {
        var viewModel = Live(0, 1, 2);

        viewModel.SelectNeuron(NetworkFocusPresentationViewModel.OutputLayer, 0);

        viewModel.SelectionText.ShouldBe(UiText.Format("{0} is not driven by any input yet.", _rearKnee));
    }

    [Fact]
    public void SelectOutput_WithOneInputDrivingIt_NamesThatInput()
    {
        var viewModel = Live(disabledGenes: 5);

        viewModel.SelectNeuron(NetworkFocusPresentationViewModel.OutputLayer, 1);

        viewModel.SelectionText.ShouldBe(UiText.Format("{0} is driven most by {1}.", _frontKnee, _along));
    }

    [Fact]
    public void SelectInput_DrivingNothing_SaysItDrivesNoOutput()
    {
        var viewModel = Live(disabledGenes: 1);

        viewModel.SelectNeuron(NetworkFocusPresentationViewModel.InputLayer, 1);

        viewModel.SelectionText.ShouldBe(UiText.Format("{0} does not drive any output yet.", _kneeSpeed));
    }

    [Fact]
    public void SelectingTheSelectedNeuronAgain_ClearsTheSelection()
    {
        var viewModel = Live();
        viewModel.SelectNeuron(NetworkFocusPresentationViewModel.OutputLayer, 1);

        viewModel.SelectNeuron(NetworkFocusPresentationViewModel.OutputLayer, 1);

        viewModel.Selected.ShouldBeNull();
        viewModel.Edges.ShouldAllBe(edge => !edge.IsHighlighted);
    }

    [Fact]
    public void ClearSelection_ReturnsToTheUnselectedHint()
    {
        var viewModel = Live();
        viewModel.SelectNeuron(NetworkFocusPresentationViewModel.InputLayer, 1);

        viewModel.ClearSelection();

        viewModel.Selected.ShouldBeNull();
        viewModel.SelectionText.ShouldBe(_noSelection);
        viewModel.Layers.SelectMany(layer => layer.Neurons).ShouldAllBe(neuron => !neuron.IsHighlighted);
    }

    [Fact]
    public void Selection_SurvivesTheNextUpdate()
    {
        var viewModel = Live();
        viewModel.SelectNeuron(NetworkFocusPresentationViewModel.InputLayer, 0);

        viewModel.Update(Sample(Network()));

        viewModel.Selected.ShouldBe((NetworkFocusPresentationViewModel.InputLayer, 0));
        viewModel.SelectionText.ShouldBe(UiText.Format("{0} drives {1} and {2} most.", _along, _frontKnee, _rearKnee));
    }

    [Fact]
    public void Update_WithANetworkThatDoesNotFitThePorts_ShowsWaitingState()
    {
        var viewModel = new NetworkFocusPresentationViewModel();
        viewModel.Configure(_labels, []);
        var notifications = 0;
        viewModel.PropertyChanged += (_, _) => notifications++;

        viewModel.Update(Sample(NeuralNetwork.FromGenome([2, 1], [1.0, 1.0, 0.0], Activation.Tanh)));

        viewModel.HasNetwork.ShouldBeFalse();
        viewModel.Layers.ShouldBeEmpty();
        viewModel.Edges.ShouldBeEmpty();
        viewModel.Summary.ShouldBe(UiText.Plain("Waiting for a live network"));
        viewModel.SelectionText.ShouldBe(UiText.Plain("Start training to see the live network."));
        notifications.ShouldBe(1);
    }

    [Fact]
    public void Update_WithALiveSampleOfOtherPorts_ShowsWaitingState()
    {
        var viewModel = new NetworkFocusPresentationViewModel();
        viewModel.Configure(_labels, []);

        viewModel.Update(NetworkPortValues.Sample(new NetworkPortLayout([NetworkPort.Input(1, "along")], []), NeuralNetwork.FromGenome([1, 0], [], Activation.Tanh), [0.5]));

        viewModel.HasNetwork.ShouldBeFalse();
        viewModel.Layers.ShouldBeEmpty();
        viewModel.Edges.ShouldBeEmpty();
        viewModel.Summary.ShouldBe(UiText.Plain("Waiting for a live network"));
    }

    [Fact]
    public void Update_WithNothingToDrive_LeavesOutTheLegendAndWarns()
    {
        var viewModel = new NetworkFocusPresentationViewModel();
        viewModel.Configure(new NetworkPortLabels([_along], []), []);

        viewModel.Update(NetworkPortValues.Sample(new NetworkPortLayout([NetworkPort.Input(1, "along")], []), NeuralNetwork.FromGenome([1, 0], [], Activation.Tanh), [0.5]));

        viewModel.HasNetwork.ShouldBeTrue();
        viewModel.Edges.ShouldBeEmpty();
        viewModel.Summary.ShouldBe(UiText.Format(
            "{0} → {1}.",
            UiText.Counted("{0} input", "{0} inputs", 1),
            UiText.Counted("{0} output", "{0} outputs", 0)));
        viewModel.SelectionText.ShouldBe(UiText.Plain("Warning, no powered parts added! There is nothing to train"));
    }

    [Fact]
    public void NoViewModel_OffersNetworkEditing()
    {
        string[] editing = ["SetNetworkShape", "NetworkShape", "NetworkSetup", "HiddenLayers", "NeuronsPerLayer"];
        var members = typeof(NetworkFocusPresentationViewModel).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(NetworkFocusPresentationViewModel).Namespace)
            .SelectMany(type => type.GetMembers().Select(member => $"{type.Name}.{member.Name}"));

        members.ShouldNotContain(member => editing.Any(name => member.EndsWith($".{name}", StringComparison.Ordinal)));
    }
}
