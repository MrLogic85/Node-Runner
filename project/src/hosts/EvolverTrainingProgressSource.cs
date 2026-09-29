using NodeRunner.App.ViewModels;
using NodeRunner.Sim;

namespace NodeRunner.Hosts;

public sealed class EvolverTrainingProgressSource : ITrainingProgressSource
{
    private readonly Evolver _evolver;

    public EvolverTrainingProgressSource(Evolver evolver)
    {
        ArgumentNullException.ThrowIfNull(evolver);

        _evolver = evolver;
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

    public bool IsTrialActive => _evolver.IsTrialActive;

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
