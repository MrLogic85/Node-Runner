using System.ComponentModel;
using NodeRunner.Domain;
using NodeRunner.ML;

namespace NodeRunner.App.ViewModels;

public sealed class BrainFocusPresentationViewModel : INotifyPropertyChanged
{
    private static readonly BrainFocusLayerPresentation[] _emptyLayers = [];
    private static readonly BrainFocusEdgePresentation[] _emptyEdges = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<BrainFocusLayerPresentation> Layers { get; private set; } = _emptyLayers;

    public IReadOnlyList<BrainFocusEdgePresentation> Edges { get; private set; } = _emptyEdges;

    public bool HasNetwork { get; private set; }

    public string Summary { get; private set; } = "Waiting for a live brain";

    public int SelectedLayerIndex { get; private set; } = 1;

    public int SelectedNeuronIndex { get; private set; }

    public string SelectedNeuronLabel { get; private set; } = "No neuron selected";

    public string SelectedNeuronSummary { get; private set; } = "Start training to inspect live activations.";

    public void Update(
        NeuralNetwork? brain,
        IReadOnlyList<SensorReading> sensors,
        IReadOnlyList<MotorReading> motors)
    {
        ArgumentNullException.ThrowIfNull(sensors);
        ArgumentNullException.ThrowIfNull(motors);

        if (brain is null || sensors.Count != brain.LayerSizes[0])
        {
            Clear();
            return;
        }

        var input = sensors.Select(sensor => sensor.Value).ToArray();
        var activations = brain.CaptureActivations(input);
        var weights = brain.Weights;
        Layers = activations.Select((layer, layerIndex) => ToLayer(layerIndex, activations.Length, layer, sensors, motors)).ToArray();
        Edges = weights.SelectMany((layerWeights, layerIndex) => ToEdges(layerWeights, layerIndex, brain.LayerSizes[layerIndex])).ToArray();
        HasNetwork = true;
        Summary = $"{brain.LayerSizes[0]} inputs -> {brain.LayerSizes[^2]} hidden -> {brain.LayerSizes[^1]} outputs";
        ClampSelection();
        UpdateSelectedSummary();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public void SelectNeuron(int layerIndex, int neuronIndex)
    {
        if (layerIndex < 0 || layerIndex >= Layers.Count || neuronIndex < 0 || neuronIndex >= Layers[layerIndex].Neurons.Count)
        {
            return;
        }

        SelectedLayerIndex = layerIndex;
        SelectedNeuronIndex = neuronIndex;
        UpdateSelectedSummary();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedNeuronIndex)));
    }

    private void Clear()
    {
        Layers = _emptyLayers;
        Edges = _emptyEdges;
        HasNetwork = false;
        Summary = "Waiting for a live brain";
        SelectedLayerIndex = 1;
        SelectedNeuronIndex = 0;
        SelectedNeuronLabel = "No neuron selected";
        SelectedNeuronSummary = "Start training to inspect live activations.";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    private static BrainFocusLayerPresentation ToLayer(
        int layerIndex,
        int layerCount,
        IReadOnlyList<double> activations,
        IReadOnlyList<SensorReading> sensors,
        IReadOnlyList<MotorReading> motors)
    {
        var title = layerIndex switch
        {
            0 => "Inputs",
            _ when layerIndex == layerCount - 1 => "Outputs",
            _ => "Hidden",
        };
        var neurons = activations
            .Select((activation, neuronIndex) => new BrainFocusNeuronPresentation(
                layerIndex,
                neuronIndex,
                LabelFor(layerIndex, neuronIndex, sensors, motors),
                activation,
                Math.Clamp(Math.Abs(activation), 0, 1)))
            .ToArray();
        return new BrainFocusLayerPresentation(title, neurons);
    }

    private static string LabelFor(
        int layerIndex,
        int neuronIndex,
        IReadOnlyList<SensorReading> sensors,
        IReadOnlyList<MotorReading> motors)
    {
        if (layerIndex == 0 && neuronIndex < sensors.Count)
        {
            var sensor = sensors[neuronIndex];
            return $"{sensor.GroupKind} {sensor.GroupIndex} {sensor.Name}";
        }

        if (layerIndex > 1 && neuronIndex < motors.Count)
        {
            return $"Motor {motors[neuronIndex].GroupIndex} target";
        }

        return $"Hidden {neuronIndex + 1}";
    }

    private static IEnumerable<BrainFocusEdgePresentation> ToEdges(double[] weights, int layerIndex, int inputCount)
    {
        var outputCount = weights.Length / inputCount;
        for (var output = 0; output < outputCount; output++)
        {
            for (var input = 0; input < inputCount; input++)
            {
                var weight = weights[(output * inputCount) + input];
                yield return new BrainFocusEdgePresentation(
                    layerIndex,
                    input,
                    layerIndex + 1,
                    output,
                    weight,
                    Math.Clamp(Math.Abs(weight) / 2.0, 0, 1));
            }
        }
    }

    private void ClampSelection()
    {
        if (Layers.Count == 0)
        {
            SelectedLayerIndex = 1;
            SelectedNeuronIndex = 0;
            return;
        }

        SelectedLayerIndex = Math.Clamp(SelectedLayerIndex, 0, Layers.Count - 1);
        SelectedNeuronIndex = Math.Clamp(SelectedNeuronIndex, 0, Layers[SelectedLayerIndex].Neurons.Count - 1);
    }

    private void UpdateSelectedSummary()
    {
        if (Layers.Count == 0)
        {
            SelectedNeuronLabel = "No neuron selected";
            SelectedNeuronSummary = "Start training to inspect live activations.";
            return;
        }

        var neuron = Layers[SelectedLayerIndex].Neurons[SelectedNeuronIndex];
        SelectedNeuronLabel = neuron.Label;
        SelectedNeuronSummary = $"{Layers[SelectedLayerIndex].Title} neuron {neuron.Index + 1} activation {neuron.Activation:0.00}";
    }
}
