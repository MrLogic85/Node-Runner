namespace NodeRunner.App.ViewModels;

public interface ITrainingProgressSource : IDisposable
{
    event Action? ProgressChanged;

    event Action? NewBestFound;

    int Generation { get; }

    int ShadowCount { get; }

    double BestFitness { get; }

    double MeanFitness { get; }

    IReadOnlyList<double> CompletedFitness { get; }

    bool IsTrialActive { get; }

    /// <summary>The followed shadow, zero-based.</summary>
    int FollowedShadow { get; }

    /// <summary>Whether shadow 0 runs the previous best.</summary>
    bool HasPreviousBest { get; }

    /// <summary>Every shadow's distance so far this trial, NaN for one that is not running.</summary>
    IReadOnlyList<double> ShadowDistances { get; }

    /// <summary>Follows the zero-based <paramref name="shadow"/> until another is picked.</summary>
    void Follow(int shadow);
}
