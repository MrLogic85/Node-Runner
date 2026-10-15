namespace NodeRunner.App.ViewModels;

/// <summary>
/// One neuron in NetworkFocus. <see cref="IsHighlighted"/> marks the selected neuron and the ones
/// its sentence names.
/// </summary>
public sealed record NetworkFocusNeuronPresentation(
    int LayerIndex,
    int Index,
    UiText Label,
    double Activation,
    double ActivationFill,
    bool IsSelected = false,
    bool IsHighlighted = false);
