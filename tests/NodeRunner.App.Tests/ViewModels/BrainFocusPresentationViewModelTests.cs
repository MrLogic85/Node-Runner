using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.ML;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BrainFocusPresentationViewModelTests
{
    private static readonly BrainPortLabels _labels = new(
        ["Accelerometer: along", "Front knee: speed", "Camera: centre"],
        ["Rear knee", "Front knee"]);

    // Weights by output, then input: Rear knee <- (0.2, -1.5, 0.9); Front knee <- (1.0, 0.0, -0.1).
    private static NeuralNetwork Brain() =>
        NeuralNetwork.FromGenome([3, 2], [0.2, -1.5, 0.9, 1.0, 0.0, -0.1, 0.0, 0.0], Activation.Tanh);

    private static SensorReading[] Readings() =>
    [
        new("Accelerometer", 1, "along", 0.5),
        new("Piston", 1, "speed", -0.25),
        new("Camera", 1, "centre", 1),
    ];

    private static BrainFocusPresentationViewModel Live(params int[] disabledGenes)
    {
        var viewModel = new BrainFocusPresentationViewModel();
        viewModel.Configure(_labels, disabledGenes);
        viewModel.Update(Brain(), Readings());
        return viewModel;
    }

    [Fact]
    public void Update_ShowsInputsAndOutputsByPortName_WithNothingSelected()
    {
        var viewModel = Live();

        viewModel.HasNetwork.ShouldBeTrue();
        viewModel.Summary.ShouldBe("3 senses → 2 outputs");
        viewModel.Layers.Select(layer => layer.Title).ShouldBe(["Senses", "Outputs"]);
        viewModel.Layers[0].Neurons.Select(neuron => neuron.Label).ShouldBe(_labels.Inputs);
        viewModel.Layers[1].Neurons.Select(neuron => neuron.Label).ShouldBe(_labels.Outputs);
        viewModel.Layers[0].Neurons[0].Activation.ShouldBe(0.5);
        viewModel.Layers[1].Neurons[0].Activation.ShouldBe(Math.Tanh((0.2 * 0.5) + (-1.5 * -0.25) + 0.9), 1e-12);
        viewModel.Selected.ShouldBeNull();
        viewModel.SelectionText.ShouldBe("Tap a sense or an output to see what drives what.");
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
    public void SelectOutput_NamesTheSensesThatDriveItMost()
    {
        var viewModel = Live();

        viewModel.SelectNeuron(BrainFocusPresentationViewModel.OutputLayer, 0);

        viewModel.SelectionText.ShouldBe("Rear knee is driven most by Front knee:\u00A0speed and Camera:\u00A0centre.");
        viewModel.Layers[1].Neurons[0].IsSelected.ShouldBeTrue();
        viewModel.Layers[0].Neurons.Select(neuron => neuron.IsHighlighted).ShouldBe([false, true, true]);
        viewModel.Edges.Where(edge => edge.IsHighlighted).Select(edge => edge.ToNeuronIndex).ShouldAllBe(output => output == 0);
        viewModel.Edges.Count(edge => edge.IsHighlighted).ShouldBe(3);
    }

    [Fact]
    public void SelectSense_NamesTheOutputsItDrivesMost_SkippingZeroAndDisabledConnections()
    {
        var viewModel = Live();

        viewModel.SelectNeuron(BrainFocusPresentationViewModel.InputLayer, 1);

        viewModel.SelectionText.ShouldBe("Front knee:\u00A0speed drives Rear knee most.");
        viewModel.Layers[1].Neurons.Select(neuron => neuron.IsHighlighted).ShouldBe([true, false]);
    }

    [Fact]
    public void SelectOutput_WithEveryConnectionDisabled_SaysNothingDrivesIt()
    {
        var viewModel = Live(0, 1, 2);

        viewModel.SelectNeuron(BrainFocusPresentationViewModel.OutputLayer, 0);

        viewModel.SelectionText.ShouldBe("Rear knee is not driven by any sense yet.");
    }

    [Fact]
    public void SelectingTheSelectedNeuronAgain_ClearsTheSelection()
    {
        var viewModel = Live();
        viewModel.SelectNeuron(BrainFocusPresentationViewModel.OutputLayer, 1);

        viewModel.SelectNeuron(BrainFocusPresentationViewModel.OutputLayer, 1);

        viewModel.Selected.ShouldBeNull();
        viewModel.Edges.ShouldAllBe(edge => !edge.IsHighlighted);
    }

    [Fact]
    public void ClearSelection_ReturnsToTheUnselectedHint()
    {
        var viewModel = Live();
        viewModel.SelectNeuron(BrainFocusPresentationViewModel.InputLayer, 1);

        viewModel.ClearSelection();

        viewModel.Selected.ShouldBeNull();
        viewModel.SelectionText.ShouldBe("Tap a sense or an output to see what drives what.");
        viewModel.Layers.SelectMany(layer => layer.Neurons).ShouldAllBe(neuron => !neuron.IsHighlighted);
    }

    [Fact]
    public void Selection_SurvivesTheNextUpdate()
    {
        var viewModel = Live();
        viewModel.SelectNeuron(BrainFocusPresentationViewModel.InputLayer, 0);

        viewModel.Update(Brain(), Readings());

        viewModel.Selected.ShouldBe((BrainFocusPresentationViewModel.InputLayer, 0));
        viewModel.SelectionText.ShouldBe("Accelerometer:\u00A0along drives Front knee and Rear knee most.");
    }

    [Fact]
    public void Update_WithABrainThatDoesNotFitThePorts_ShowsWaitingState()
    {
        var viewModel = new BrainFocusPresentationViewModel();
        viewModel.Configure(_labels, []);
        var notifications = 0;
        viewModel.PropertyChanged += (_, _) => notifications++;

        viewModel.Update(NeuralNetwork.FromGenome([2, 1], [1.0, 1.0, 0.0], Activation.Tanh), Readings());

        viewModel.HasNetwork.ShouldBeFalse();
        viewModel.Layers.ShouldBeEmpty();
        viewModel.Edges.ShouldBeEmpty();
        viewModel.Summary.ShouldBe("Waiting for a live brain");
        notifications.ShouldBe(1);
    }

    [Fact]
    public void NoViewModel_OffersBrainEditing()
    {
        string[] editing = ["SetBrainShape", "BrainShape", "BrainSetup", "HiddenLayers", "NeuronsPerLayer"];
        var members = typeof(BrainFocusPresentationViewModel).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(BrainFocusPresentationViewModel).Namespace)
            .SelectMany(type => type.GetMembers().Select(member => $"{type.Name}.{member.Name}"));

        members.ShouldNotContain(member => editing.Any(name => member.EndsWith($".{name}", StringComparison.Ordinal)));
    }
}
