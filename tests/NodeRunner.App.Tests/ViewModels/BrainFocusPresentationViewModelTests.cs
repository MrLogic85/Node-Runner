using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.ML;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BrainFocusPresentationViewModelTests
{
    [Fact]
    public void Update_WithLiveNetwork_BuildsLayersEdgesAndSelection()
    {
        var viewModel = new BrainFocusPresentationViewModel();
        var notifications = 0;
        viewModel.PropertyChanged += (_, _) => notifications++;
        var network = NeuralNetwork.FromGenome(
            new[] { 2, 2, 1 },
            new[] { 1.0, -1.0, 0.5, 0.25, 0.0, 0.1, -0.75, 1.25, 0.2 },
            Activation.Tanh);

        viewModel.Update(
            network,
            [
                new SensorReading("Core", 1, "Ray down", 0.5),
                new SensorReading("Motor relation", 1, "angle", -0.25),
            ],
            [new MotorReading(1, 0.4, 12)]);

        viewModel.HasNetwork.ShouldBeTrue();
        viewModel.Summary.ShouldBe("2 inputs -> 2 hidden -> 1 outputs");
        viewModel.Layers.Select(layer => layer.Title).ShouldBe(["Inputs", "Hidden", "Outputs"]);
        viewModel.Layers[0].Neurons.Select(neuron => neuron.Label).ShouldBe(["Core 1 Ray down", "Motor relation 1 angle"]);
        viewModel.Layers[2].Neurons[0].Label.ShouldBe("Motor 1 target");
        viewModel.Edges.Count.ShouldBe(6);
        viewModel.Edges[0].Weight.ShouldBe(1.0);
        viewModel.Edges[0].Strength.ShouldBe(0.5);
        viewModel.SelectedNeuronLabel.ShouldBe("Hidden 1");
        viewModel.SelectedNeuronSummary.ShouldStartWith("Hidden neuron 1 activation");
        notifications.ShouldBe(1);
    }

    [Fact]
    public void SelectNeuron_UpdatesSelectedExplanation()
    {
        var viewModel = new BrainFocusPresentationViewModel();
        viewModel.Update(
            NeuralNetwork.FromGenome(new[] { 1, 1 }, [2.0, 0.0], Activation.Tanh),
            [new SensorReading("Core", 1, "Ray down", 0.5)],
            [new MotorReading(1, 0.2, 4)]);

        viewModel.SelectNeuron(0, 0);

        viewModel.SelectedLayerIndex.ShouldBe(0);
        viewModel.SelectedNeuronIndex.ShouldBe(0);
        viewModel.SelectedNeuronLabel.ShouldBe("Core 1 Ray down");
        viewModel.SelectedNeuronSummary.ShouldBe("Inputs neuron 1 activation 0.50");
    }

    [Fact]
    public void Update_WithMismatchedInputs_ShowsWaitingState()
    {
        var viewModel = new BrainFocusPresentationViewModel();

        viewModel.Update(
            NeuralNetwork.FromGenome(new[] { 2, 1 }, [1.0, 1.0, 0.0], Activation.Tanh),
            [new SensorReading("Core", 1, "Ray down", 0.5)],
            []);

        viewModel.HasNetwork.ShouldBeFalse();
        viewModel.Layers.ShouldBeEmpty();
        viewModel.Edges.ShouldBeEmpty();
        viewModel.Summary.ShouldBe("Waiting for a live brain");
    }
}
