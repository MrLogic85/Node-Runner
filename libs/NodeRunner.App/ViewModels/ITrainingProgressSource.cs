namespace NodeRunner.App.ViewModels;

public interface ITrainingProgressSource : IDisposable
{
    event Action? ProgressChanged;

    event Action? NewBestFound;

    int Generation { get; }

    int CurrentShadow { get; }

    int ShadowCount { get; }

    double BestFitness { get; }

    double MeanFitness { get; }

    IReadOnlyList<double> CompletedFitness { get; }

    bool IsTrialActive { get; }
}
