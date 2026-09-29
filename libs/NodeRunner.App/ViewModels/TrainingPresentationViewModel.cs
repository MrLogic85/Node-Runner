using System.ComponentModel;

namespace NodeRunner.App.ViewModels;

/// <summary>Presentation-only training state consumed by the Simulate surface.</summary>
public sealed class TrainingPresentationViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ITrainingProgressSource? _source;
    private int _generation;
    private int _candidate;
    private int _population;
    private double _bestFitness = double.NegativeInfinity;
    private double _meanFitness;
    private int _bestGeneration;
    private bool _isTrialActive;
    private double[] _completedFitness = [];
    private bool _disposed;

    public event PropertyChangedEventHandler? PropertyChanged;

    public TrainingPresentationViewModel()
    {
    }

    public TrainingPresentationViewModel(ITrainingProgressSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        _source = source;
        _source.ProgressChanged += OnProgressChanged;
        _source.NewBestFound += OnNewBestFound;
        ApplySourceState();
    }

    public int Generation => _generation;
    public int Candidate => _candidate;
    public int Population => _population;
    public double BestFitness => _bestFitness;
    public double MeanFitness => _meanFitness;
    public int BestGeneration => _bestGeneration;
    public bool IsTrialActive => _isTrialActive;
    public IReadOnlyList<double> CompletedFitness => _completedFitness;

    public string GenerationText => _isTrialActive
        ? $"Generation {_generation} · try {_candidate} of {_population}"
        : $"Generation {_generation} · session complete";

    public string BestFitnessText => double.IsNegativeInfinity(_bestFitness)
        ? "Best: —"
        : $"Best: {_bestFitness:0.0} (gen {_bestGeneration})";

    public string MeanFitnessText => $"Mean: {_meanFitness:0.0}";

    public void Update(
        int generation,
        int candidate,
        int population,
        double bestFitness,
        double meanFitness,
        int bestGeneration,
        bool isTrialActive,
        IReadOnlyList<double> completedFitness)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(generation);
        ArgumentOutOfRangeException.ThrowIfNegative(candidate);
        ArgumentOutOfRangeException.ThrowIfNegative(population);
        ArgumentOutOfRangeException.ThrowIfNegative(bestGeneration);
        ArgumentNullException.ThrowIfNull(completedFitness);

        _generation = generation;
        _candidate = candidate;
        _population = population;
        _bestFitness = bestFitness;
        _meanFitness = meanFitness;
        _bestGeneration = bestGeneration;
        _isTrialActive = isTrialActive;
        _completedFitness = completedFitness.ToArray();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public void Dispose()
    {
        if (_source is null || _disposed)
        {
            return;
        }

        _disposed = true;
        _source.ProgressChanged -= OnProgressChanged;
        _source.NewBestFound -= OnNewBestFound;
        _source.Dispose();
    }

    private void OnProgressChanged()
    {
        ApplySourceState();
    }

    private void OnNewBestFound()
    {
        _bestGeneration = _source!.Generation;
        ApplySourceState();
    }

    private void ApplySourceState()
    {
        Update(
            _source!.Generation,
            _source.CurrentCandidate,
            _source.PopulationSize,
            _source.BestFitness,
            _source.MeanFitness,
            _bestGeneration,
            _source.IsTrialActive,
            _source.CompletedFitness);
    }
}
