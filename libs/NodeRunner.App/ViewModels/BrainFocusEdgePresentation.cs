namespace NodeRunner.App.ViewModels;

public sealed record BrainFocusEdgePresentation(
    int FromLayerIndex,
    int FromNeuronIndex,
    int ToLayerIndex,
    int ToNeuronIndex,
    double Weight,
    double Strength);
