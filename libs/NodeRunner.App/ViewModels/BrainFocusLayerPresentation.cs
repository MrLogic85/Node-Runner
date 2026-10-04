namespace NodeRunner.App.ViewModels;

public sealed record BrainFocusLayerPresentation(
    UiText Title,
    IReadOnlyList<BrainFocusNeuronPresentation> Neurons);
