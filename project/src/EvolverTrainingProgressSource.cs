using NodeRunner.App.ViewModels;
using NodeRunner.Sim;

namespace NodeRunner;

public sealed class EvolverTrainingProgressSource : ITrainingProgressSource
{
    private readonly Evolver _evolver;
    private readonly Func<string> _profileProvider;

    public EvolverTrainingProgressSource(Evolver evolver, Func<string> profileProvider)
    {
        ArgumentNullException.ThrowIfNull(evolver);
        ArgumentNullException.ThrowIfNull(profileProvider);

        _evolver = evolver;
        _profileProvider = profileProvider;
        _evolver.TrainingProgressChanged += OnProgressChanged;
        _evolver.NewBestFound += OnNewBestFound;
    }

    public event Action? ProgressChanged;

    public event Action? NewBestFound;

    public int Generation => _evolver.Generation;

    public int CurrentCandidate => _evolver.CurrentCandidate;

    public int PopulationSize => _evolver.PopulationSize;

    public double BestFitness => _evolver.BestFitness;

    public double MeanFitness => _evolver.MeanFitness;

    public IReadOnlyList<double> CompletedFitness => _evolver.CompletedFitness;

    public int CompletedCandidateCount => _evolver.CompletedCandidateCount;

    public bool IsTrialActive => _evolver.IsTrialActive;

    public string Profile => _profileProvider();

    private void OnProgressChanged()
    {
        ProgressChanged?.Invoke();
    }

    private void OnNewBestFound()
    {
        NewBestFound?.Invoke();
    }

    public void Dispose()
    {
        _evolver.TrainingProgressChanged -= OnProgressChanged;
        _evolver.NewBestFound -= OnNewBestFound;
    }
}
