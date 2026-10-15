namespace NodeRunner.App.ViewModels;

/// <summary>One enabled connection in NetworkFocus. <see cref="IsHighlighted"/> marks the ones touching the selected neuron.</summary>
public sealed record NetworkFocusEdgePresentation(
    int FromLayerIndex,
    int FromNeuronIndex,
    int ToLayerIndex,
    int ToNeuronIndex,
    double Weight,
    double Strength,
    bool IsHighlighted = false);
