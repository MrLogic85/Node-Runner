namespace NodeRunner.App.ViewModels;

public sealed record BuildPanelPresentation(
    string InputSummary,
    string MotorRelationSummary,
    bool CanStartTraining,
    string ReadinessText,
    int InputCount = 0,
    int OutputCount = 0)
{
    public const string Title = "Brain it will get";

    public const string TeachingNote = "Sees sensor values, decides joint targets, twists beams, then scores distance.";

    public static BuildPanelPresentation Sample { get; } = new(
        "2 cores: 12 sensors; 3 motor relations: 6 sensors; 18 inputs total",
        "3 motor relations can twist",
        CanStartTraining: true,
        ReadinessText: "Ready to train",
        InputCount: 18,
        OutputCount: 3);
}
