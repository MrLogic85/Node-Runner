namespace NodeRunner.App.ViewModels;

public interface ITrainingProgressSource : IDisposable
{
    event Action? ProgressChanged;

    event Action? NewBestFound;

    int Generation { get; }

    int CurrentCandidate { get; }

    int PopulationSize { get; }

    double BestFitness { get; }

    double MeanFitness { get; }

    IReadOnlyList<double> CompletedFitness { get; }

    int CompletedCandidateCount { get; }

    bool IsTrialActive { get; }

    string Profile { get; }
}
