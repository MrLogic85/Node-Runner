using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using NodeRunner.Domain;
using NodeRunner.ML;

namespace NodeRunner.App.ViewModels;

/// <summary>One port and the value it had in a <see cref="NetworkPortValues"/> sample, or null with no live network.</summary>
public readonly record struct NetworkPortValue(NetworkPort Port, double? Value);

/// <summary>
/// One sample of the live network's ports: what each input read and each output set in one
/// refresh, with the connection weights of that moment. The host samples once per refresh and
/// every view of the network (NetworkFocus, the part callout) reads the same sample, so they always
/// agree and none of them runs the network again. Values are keyed by <see cref="NetworkPort"/>, never
/// by a label. Immutable. See docs/TRAINING_LOOP.md → "The Training scene".
/// </summary>
public sealed class NetworkPortValues
{
    private readonly Dictionary<NetworkPort, double> _byPort;

    private NetworkPortValues(NetworkPortLayout layout, double[]? inputs, double[]? outputs, double[]? weights)
    {
        Layout = layout;
        Inputs = inputs is null ? null : Array.AsReadOnly(inputs);
        Outputs = outputs is null ? null : Array.AsReadOnly(outputs);
        Weights = weights is null ? null : Array.AsReadOnly(weights);
        _byPort = [];
        if (inputs is not null && outputs is not null)
        {
            for (var i = 0; i < inputs.Length; i++)
            {
                _byPort[layout.Inputs[i]] = inputs[i];
            }

            for (var i = 0; i < outputs.Length; i++)
            {
                _byPort[layout.Outputs[i]] = outputs[i];
            }
        }
    }

    /// <summary>No ports and no live network.</summary>
    public static NetworkPortValues Empty { get; } = new(NetworkPortLayout.Empty, null, null, null);

    /// <summary>The ports sampled, in runtime order.</summary>
    public NetworkPortLayout Layout { get; }

    /// <summary>What each input read, in <see cref="NetworkPortLayout.Inputs"/> order; null with no live network.</summary>
    public ReadOnlyCollection<double>? Inputs { get; }

    /// <summary>What each output set, in <see cref="NetworkPortLayout.Outputs"/> order; null with no live network.</summary>
    public ReadOnlyCollection<double>? Outputs { get; }

    /// <summary>
    /// The direct network's connection weights, gene <c>output · inputs + input</c>; null with no
    /// live network. Hidden layers are #549.
    /// </summary>
    public ReadOnlyCollection<double>? Weights { get; }

    /// <summary>Whether a network that fits <see cref="Layout"/> was running when sampled.</summary>
    [MemberNotNullWhen(true, nameof(Inputs), nameof(Outputs), nameof(Weights))]
    public bool IsLive => Inputs is not null && Outputs is not null && Weights is not null;

    /// <summary>
    /// Runs <paramref name="network"/> once on <paramref name="inputs"/>, given in
    /// <paramref name="layout"/>'s input order. With no network, or one or inputs that do not fit
    /// the layout, the sample has the layout but no values.
    /// </summary>
    public static NetworkPortValues Sample(NetworkPortLayout layout, NeuralNetwork? network, IReadOnlyList<double> inputs)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(inputs);

        if (network is null
            || network.LayerSizes is not [var networkInputs, var networkOutputs]
            || networkInputs != layout.Inputs.Count
            || networkOutputs != layout.Outputs.Count
            || inputs.Count != layout.Inputs.Count)
        {
            return new NetworkPortValues(layout, null, null, null);
        }

        var activations = network.CaptureActivations([.. inputs]);
        return new NetworkPortValues(layout, activations[0], activations[1], network.Weights[0]);
    }

    /// <summary>The value <paramref name="port"/> had, or null when it was not sampled live.</summary>
    public double? ValueOf(NetworkPort port) => _byPort.TryGetValue(port, out var value) ? value : null;

    /// <summary>One part's ports with their values: its inputs, then its outputs, each in port order.</summary>
    public IReadOnlyList<NetworkPortValue> Of(int partId) =>
        Layout.Inputs
            .Concat(Layout.Outputs)
            .Where(port => port.PartId == partId)
            .Select(port => new NetworkPortValue(port, ValueOf(port)))
            .ToArray();
}
