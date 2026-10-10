using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using NodeRunner.Domain;
using NodeRunner.ML;

namespace NodeRunner.App.ViewModels;

/// <summary>One port and the value it had in a <see cref="BrainPortValues"/> sample, or null with no live brain.</summary>
public readonly record struct BrainPortValue(BrainPort Port, double? Value);

/// <summary>
/// One sample of the live brain's ports (#1064): what each sense read and each output set in one
/// refresh, with the connection weights of that moment. The host samples once per refresh and
/// every view of the brain (BrainFocus, the part callout) reads the same sample, so they always
/// agree and none of them runs the brain again. Values are keyed by <see cref="BrainPort"/>, never
/// by a label. Immutable. See docs/TRAINING_LOOP.md → "World view".
/// </summary>
public sealed class BrainPortValues
{
    private readonly Dictionary<BrainPort, double> _byPort;

    private BrainPortValues(BrainPortLayout layout, double[]? inputs, double[]? outputs, double[]? weights)
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

    /// <summary>No ports and no live brain.</summary>
    public static BrainPortValues Empty { get; } = new(BrainPortLayout.Empty, null, null, null);

    /// <summary>The ports sampled, in runtime order.</summary>
    public BrainPortLayout Layout { get; }

    /// <summary>What each sense read, in <see cref="BrainPortLayout.Inputs"/> order; null with no live brain.</summary>
    public ReadOnlyCollection<double>? Inputs { get; }

    /// <summary>What each output set, in <see cref="BrainPortLayout.Outputs"/> order; null with no live brain.</summary>
    public ReadOnlyCollection<double>? Outputs { get; }

    /// <summary>
    /// The direct brain's connection weights, gene <c>output · inputs + input</c>; null with no
    /// live brain. Hidden layers are #549.
    /// </summary>
    public ReadOnlyCollection<double>? Weights { get; }

    /// <summary>Whether a brain that fits <see cref="Layout"/> was running when sampled.</summary>
    [MemberNotNullWhen(true, nameof(Inputs), nameof(Outputs), nameof(Weights))]
    public bool IsLive => Inputs is not null && Outputs is not null && Weights is not null;

    /// <summary>
    /// Runs <paramref name="brain"/> once on <paramref name="inputs"/>, given in
    /// <paramref name="layout"/>'s input order. With no brain, or one or inputs that do not fit
    /// the layout, the sample has the layout but no values.
    /// </summary>
    public static BrainPortValues Sample(BrainPortLayout layout, NeuralNetwork? brain, IReadOnlyList<double> inputs)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(inputs);

        if (brain is null
            || brain.LayerSizes is not [var brainInputs, var brainOutputs]
            || brainInputs != layout.Inputs.Count
            || brainOutputs != layout.Outputs.Count
            || inputs.Count != layout.Inputs.Count)
        {
            return new BrainPortValues(layout, null, null, null);
        }

        var activations = brain.CaptureActivations([.. inputs]);
        return new BrainPortValues(layout, activations[0], activations[1], brain.Weights[0]);
    }

    /// <summary>The value <paramref name="port"/> had, or null when it was not sampled live.</summary>
    public double? ValueOf(BrainPort port) => _byPort.TryGetValue(port, out var value) ? value : null;

    /// <summary>One part's ports with their values: its senses, then its outputs, each in port order.</summary>
    public IReadOnlyList<BrainPortValue> Of(int partId) =>
        Layout.Inputs
            .Concat(Layout.Outputs)
            .Where(port => port.PartId == partId)
            .Select(port => new BrainPortValue(port, ValueOf(port)))
            .ToArray();
}
