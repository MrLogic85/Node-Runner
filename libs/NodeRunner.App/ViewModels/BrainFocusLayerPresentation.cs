namespace NodeRunner.App.ViewModels;

public sealed record BrainFocusLayerPresentation(
    string Title,
    IReadOnlyList<BrainFocusNeuronPresentation> Neurons);
