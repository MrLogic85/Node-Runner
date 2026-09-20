namespace NodeRunner.App.ViewModels;

public sealed record ConstructionBuildPanelPresentation(
    string InputSummary,
    string MotorRelationSummary,
    string ValidationLine,
    bool CanStartTraining,
    string? DisabledReason)
{
    public const string Title = "Brain it will get";

    public const string TeachingNote = "Sees sensor values, decides joint targets, twists beams, then scores distance.";

    public static ConstructionBuildPanelPresentation Sample { get; } = new(
        "2 cores -> 12 core sensor values; 3 motor relations -> 6 motor-relation sensor values; 18 inputs total",
        "3 motor relations can twist",
        "Ready: 18 inputs -> 3 outputs",
        CanStartTraining: true,
        DisabledReason: null);
}
