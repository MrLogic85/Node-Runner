namespace NodeRunner.App.ViewModels;

public sealed record BrainFocusNeuronPresentation(
    int LayerIndex,
    int Index,
    string Label,
    double Activation,
    double ActivationFill);
