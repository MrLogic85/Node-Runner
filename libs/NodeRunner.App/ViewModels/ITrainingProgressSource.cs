namespace NodeRunner.App.ViewModels;

public interface ITrainingProgressSource : IDisposable
{
    event Action? ProgressChanged;

    int Generation { get; }

    int ShadowCount { get; }

    /// <summary>The best score ever reached; it never goes down (#479).</summary>
    double BestFitness { get; }

    /// <summary>The generation that reached <see cref="BestFitness"/>, 0 before any has.</summary>
    int BestGeneration { get; }

    /// <summary>The furthest any latest run's front has ended on this map (#725), whichever run holds the score; NaN until known.</summary>
    double BestShownDistance { get; }

    double MeanFitness { get; }

    IReadOnlyList<double> CompletedFitness { get; }

    bool IsTrialActive { get; }

    /// <summary>The followed shadow, zero-based.</summary>
    int FollowedShadow { get; }

    /// <summary>Whether shadow 0 runs the previous best.</summary>
    bool HasPreviousBest { get; }

    /// <summary>Every shadow's front distance so far this trial (#725), NaN for one that is not running.</summary>
    IReadOnlyList<double> ShadowDistances { get; }

    /// <summary>Follows the zero-based <paramref name="shadow"/> until another is picked.</summary>
    void Follow(int shadow);
}
