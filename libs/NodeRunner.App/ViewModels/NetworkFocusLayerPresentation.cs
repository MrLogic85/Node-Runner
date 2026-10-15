namespace NodeRunner.App.ViewModels;

public sealed record NetworkFocusLayerPresentation(
    UiText Title,
    IReadOnlyList<NetworkFocusNeuronPresentation> Neurons);
