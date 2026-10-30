using System.ComponentModel;
namespace NodeRunner.App.ViewModels;

/// <summary>
/// NetworkFocus on the direct network: an Inputs column and an Outputs column, the enabled
/// connections between them and live activations. Nothing is selected at first; tapping an
/// output names the inputs that drive it most, and tapping an input names the outputs it drives
/// most. Its look is in docs/UI_DIRECTION.md → "NetworkFocus"; hidden layers are #549.
/// </summary>
public sealed class NetworkFocusPresentationViewModel : INotifyPropertyChanged
{
    public const int InputLayer = 0;
    public const int OutputLayer = 1;

    private static readonly UiText _waitingSummary = UiText.Plain("Waiting for a live network");
    private static readonly UiText _waitingSelection = UiText.Plain("Start training to see the live network.");
    private static readonly UiText _noSelection = UiText.Plain("Tap an input or an output to see what drives what.");
    private static readonly UiText _nothingToDrive = UiText.Plain("Warning, no powered parts added! There is nothing to train");

    private NetworkPortLabels _labels = NetworkPortLabels.Empty;
    private HashSet<int> _disabledGenes = [];
    private double[][] _activations = [];
    private (int Input, int Output, double Weight)[] _connections = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<NetworkFocusLayerPresentation> Layers { get; private set; } = [];

    public IReadOnlyList<NetworkFocusEdgePresentation> Edges { get; private set; } = [];

    public bool HasNetwork { get; private set; }

    /// <summary>The network's size and how to read it, or that no network runs yet.</summary>
    public UiText Summary { get; private set; } = _waitingSummary;

    /// <summary>The tapped neuron as (layer, index), or null with none.</summary>
    public (int Layer, int Index)? Selected { get; private set; }

    public UiText SelectionText { get; private set; } = _waitingSelection;

    /// <summary>
    /// Names the ports and leaves out the disabled connections, given as indices into the direct
    /// network's genome (<c>DirectNetwork.DisabledGenes</c>). Clears the selection.
    /// </summary>
    public void Configure(NetworkPortLabels labels, IEnumerable<int> disabledGenes)
    {
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(disabledGenes);
        _labels = labels;
        _disabledGenes = [.. disabledGenes];
        Selected = null;
        Clear();
    }

    /// <summary>
    /// Shows one sample of the live network, the same one the part callout reads; a sample
    /// with no live network, or one that does not fit the configured ports, shows none.
    /// </summary>
    public void Update(NetworkPortValues values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var inputCount = _labels.Inputs.Count;
        var outputCount = _labels.Outputs.Count;
        if (!values.IsLive || values.Inputs.Count != inputCount || values.Outputs.Count != outputCount)
        {
            Clear();
            return;
        }

        var (inputValues, outputValues, weights) = (values.Inputs, values.Outputs, values.Weights);

        _activations = [[.. inputValues], [.. outputValues]];
        var connections = new List<(int, int, double)>();
        for (var output = 0; output < outputCount; output++)
        {
            for (var input = 0; input < inputCount; input++)
            {
                var gene = (output * inputCount) + input;
                if (!_disabledGenes.Contains(gene))
                {
                    connections.Add((input, output, weights[gene]));
                }
            }
        }

        _connections = [.. connections];
        HasNetwork = true;
        var inputs = UiText.Counted("{0} input", "{0} inputs", inputCount);
        var outputs = UiText.Counted("{0} output", "{0} outputs", outputCount);

        // With nothing to drive there are no lines to read, so the legend is left out.
        Summary = outputCount == 0
            ? UiText.Format("{0} → {1}.", inputs, outputs)
            : UiText.Format("{0} → {1}. Solid blue: pushes up. Dashed red: pushes down. Thicker: stronger.", inputs, outputs);
        Present();
    }

    /// <summary>Selects a neuron; tapping the selected one again clears the selection.</summary>
    public void SelectNeuron(int layerIndex, int neuronIndex)
    {
        if (layerIndex < 0 || layerIndex >= Layers.Count || neuronIndex < 0 || neuronIndex >= Layers[layerIndex].Neurons.Count)
        {
            return;
        }

        Selected = Selected == (layerIndex, neuronIndex) ? null : (layerIndex, neuronIndex);
        Present();
    }

    /// <summary>Back to the unselected view, for a tap on empty space.</summary>
    public void ClearSelection()
    {
        if (Selected is null)
        {
            return;
        }

        Selected = null;
        Present();
    }

    private void Clear()
    {
        _activations = [];
        _connections = [];
        Layers = [];
        Edges = [];
        HasNetwork = false;
        Summary = _waitingSummary;
        SelectionText = _waitingSelection;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    private void Present()
    {
        var named = new HashSet<(int Layer, int Index)>();
        SelectionText = Selected switch
        {
            null when !_labels.HasPoweredOutputs => _nothingToDrive,
            null => _noSelection,
            (OutputLayer, var output) => DrivenBy(output, named),
            (_, var input) => Drives(input, named),
        };

        Layers =
        [
            Column(UiText.Plain("Inputs"), InputLayer, _labels.Inputs, named),
            Column(UiText.Plain("Outputs"), OutputLayer, _labels.Outputs, named),
        ];
        Edges = _connections
            .Select(connection => new NetworkFocusEdgePresentation(
                InputLayer,
                connection.Input,
                OutputLayer,
                connection.Output,
                connection.Weight,
                Math.Clamp(Math.Abs(connection.Weight) / 2.0, 0, 1),
                Selected == (InputLayer, connection.Input) || Selected == (OutputLayer, connection.Output)))
            .ToArray();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    private NetworkFocusLayerPresentation Column(UiText title, int layer, IReadOnlyList<UiText> labels, HashSet<(int Layer, int Index)> named) =>
        new(
            title,
            _activations[layer]
                .Select((activation, index) => new NetworkFocusNeuronPresentation(
                    layer,
                    index,
                    labels[index],
                    activation,
                    Math.Clamp(Math.Abs(activation), 0, 1),
                    Selected == (layer, index),
                    Selected == (layer, index) || named.Contains((layer, index))))
                .ToArray());

    // "Rear knee: length is driven most by Front knee: speed and Accel: along."
    private UiText DrivenBy(int output, HashSet<(int Layer, int Index)> named)
    {
        var drivers = Strongest(_connections.Where(connection => connection.Output == output), connection => connection.Input);
        named.UnionWith(drivers.Select(input => (InputLayer, input)));
        var name = _labels.Outputs[output];
        return drivers switch
        {
            [] => UiText.Format("{0} is not driven by any input yet.", name),
            [var only] => UiText.Format("{0} is driven most by {1}.", name, _labels.Inputs[only]),
            [var first, var second, ..] => UiText.Format("{0} is driven most by {1} and {2}.", name, _labels.Inputs[first], _labels.Inputs[second]),
        };
    }

    // "Accel: along drives Rear knee: length most." or "… drives … and … most."
    private UiText Drives(int input, HashSet<(int Layer, int Index)> named)
    {
        var driven = Strongest(_connections.Where(connection => connection.Input == input), connection => connection.Output);
        named.UnionWith(driven.Select(output => (OutputLayer, output)));
        var name = _labels.Inputs[input];
        return driven switch
        {
            [] => UiText.Format("{0} does not drive any output yet.", name),
            [var only] => UiText.Format("{0} drives {1} most.", name, _labels.Outputs[only]),
            [var first, var second, ..] => UiText.Format("{0} drives {1} and {2} most.", name, _labels.Outputs[first], _labels.Outputs[second]),
        };
    }

    private static int[] Strongest(IEnumerable<(int Input, int Output, double Weight)> connections, Func<(int Input, int Output, double Weight), int> other) =>
        connections
            .Where(connection => connection.Weight != 0)
            .OrderByDescending(connection => Math.Abs(connection.Weight))
            .Take(2)
            .Select(other)
            .ToArray();
}
