namespace NodeRunner.App.ViewModels;

public sealed record ConstructionBuildPanelPresentation(
    string InputSummary,
    string MotorRelationSummary,
    string ValidationLine,
    bool CanStartTraining,
    bool CanCompleteCreation,
    string? DisabledReason,
    int InputCount = 0,
    int OutputCount = 0)
{
    public const string Title = "Brain it will get";

    public const string TeachingNote = "Sees sensor values, decides joint targets, twists beams, then scores distance.";

    public static ConstructionBuildPanelPresentation Sample { get; } = new(
        "2 cores: 12 sensors; 3 motor relations: 6 sensors; 18 inputs total",
        "3 motor relations can twist",
        "Ready: 18 inputs -> 3 outputs",
        CanStartTraining: true,
        CanCompleteCreation: true,
        DisabledReason: null,
        InputCount: 18,
        OutputCount: 3);
}
