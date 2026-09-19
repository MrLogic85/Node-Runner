using System.ComponentModel;

namespace NodeRunner.App.ViewModels;

/// <summary>Presentation-only training state consumed by the Watch surface.</summary>
public sealed class TrainingPresentationViewModel : INotifyPropertyChanged
{
    private int _generation;
    private int _candidate;
    private int _population;
    private double _bestFitness = double.NegativeInfinity;
    private double _meanFitness;
    private string _profile = "Standard";

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Generation => _generation;
    public int Candidate => _candidate;
    public int Population => _population;
    public double BestFitness => _bestFitness;
    public double MeanFitness => _meanFitness;
    public string Profile => _profile;

    public void Update(int generation, int candidate, int population, double bestFitness, double meanFitness, string profile)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(generation);
        ArgumentOutOfRangeException.ThrowIfNegative(candidate);
        ArgumentOutOfRangeException.ThrowIfNegative(population);
        ArgumentNullException.ThrowIfNull(profile);
        _generation = generation;
        _candidate = candidate;
        _population = population;
        _bestFitness = bestFitness;
        _meanFitness = meanFitness;
        _profile = profile;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
