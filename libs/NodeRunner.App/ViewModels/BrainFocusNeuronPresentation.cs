namespace NodeRunner.App.ViewModels;

/// <summary>
/// One neuron in BrainFocus. <see cref="IsHighlighted"/> marks the selected neuron and the ones
/// its sentence names.
/// </summary>
public sealed record BrainFocusNeuronPresentation(
    int LayerIndex,
    int Index,
    UiText Label,
    double Activation,
    double ActivationFill,
    bool IsSelected = false,
    bool IsHighlighted = false);
