using System.ComponentModel;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>Presentation-only training state consumed by the Training screen.</summary>
public sealed class TrainingPresentationViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ITrainingProgressSource? _source;
    private readonly ShadowStripPresentation _strip = new();
    private int _generation;
    private int _shadowCount;
    private double _bestFitness = double.NegativeInfinity;
    private double _bestShownDistance = double.NaN;
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
        ApplySourceState();
    }

    /// <summary>
    /// Simulate's presentation (#702): nothing races or learns, so the best marker stays at
    /// <paramref name="training"/>'s best on <paramref name="mapId"/>, or is hidden without one.
    /// </summary>
    public static TrainingPresentationViewModel Saved(TrainingStateDef training, string mapId)
    {
        ArgumentNullException.ThrowIfNull(training);
        return new TrainingPresentationViewModel { _bestShownDistance = training.BestOn(mapId)?.FrontDistance ?? double.NaN };
    }

    public int Generation => _generation;
    public int ShadowCount => _shadowCount;
    public double BestFitness => _bestFitness;

    /// <summary>Where the best marker stands (#725): the furthest any latest run's front has ended on this map. NaN until known.</summary>
    public double BestShownDistance => _bestShownDistance;
    public double MeanFitness => _meanFitness;
    public int BestGeneration => _bestGeneration;
    public bool IsTrialActive => _isTrialActive;
    public IReadOnlyList<double> CompletedFitness => _completedFitness;

    /// <summary>
    /// The generation racing now, counted from 1 like <see cref="BestGeneration"/> (#387), or the
    /// last finished one when none is racing.
    /// </summary>
    public UiText GenerationText => UiText.Format("Generation {0}", _isTrialActive ? _generation + 1 : _generation);

    /// <summary>The best marker's flag (#388), such as "Best 4.2 m", or null until there is a best.</summary>
    public UiText? BestMarkerText => double.IsFinite(_bestShownDistance)
        ? UiText.Format("Best {0}", Metres.WithUnit(_bestShownDistance))
        : null;

    /// <summary>The followed shadow's 1-based number, or 0 without a training source.</summary>
    public int FollowedShadow => _source is null ? 0 : _source.FollowedShadow + 1;

    /// <summary>
    /// Every shadow's live standing for the shadow strip (#387), read from the source on each call
    /// because distances change every physics tick. The leader is marked here only; it never takes
    /// the camera (#385).
    /// </summary>
    public IReadOnlyList<ShadowStanding> Shadows
    {
        get
        {
            if (_source is null)
            {
                return [];
            }

            var distances = _source.ShadowDistances;
            var leader = Leader(distances);
            return distances
                .Select((distance, index) => new ShadowStanding(
                    index + 1,
                    distance,
                    index == _source.FollowedShadow,
                    index == leader,
                    index == 0 && _source.HasPreviousBest))
                .ToArray();
        }
    }

    /// <summary>The shadow strip (#387): which shadows it shows and how full their bars are.</summary>
    public ShadowStripView Strip => _strip.View(Shadows, _generation);

    /// <summary>How many places the shadow strip has; the strip sets it to as many as fit (#791).</summary>
    public int StripPlaces
    {
        get => _strip.Places;
        set => _strip.Places = value;
    }

    /// <summary>Ranks the strip by distance so far and shows its first page.</summary>
    public void SortShadows() => _strip.Sort(Shadows, _generation);

    /// <summary>Pages the strip toward the worse shadows.</summary>
    public void ShowWorseShadows() => _strip.PageWorse();

    /// <summary>Pages the strip back toward the better shadows.</summary>
    public void ShowBetterShadows() => _strip.PageBetter();

    /// <summary>Follows shadow <paramref name="number"/> (1-based) until another is picked.</summary>
    public void Follow(int number)
    {
        if (_source is null)
        {
            throw new InvalidOperationException("There is no training to follow a shadow in.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        _source.Follow(number - 1);
    }

    /// <summary>The zero-based shadow that has travelled furthest so far, or -1 when none is running.</summary>
    public static int Leader(IReadOnlyList<double> distances)
    {
        ArgumentNullException.ThrowIfNull(distances);
        var leader = -1;
        for (var i = 0; i < distances.Count; i++)
        {
            if (double.IsFinite(distances[i]) && (leader < 0 || distances[i] > distances[leader]))
            {
                leader = i;
            }
        }

        return leader;
    }

    public void Update(
        int generation,
        int shadowCount,
        double bestFitness,
        double bestShownDistance,
        double meanFitness,
        int bestGeneration,
        bool isTrialActive,
        IReadOnlyList<double> completedFitness)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(generation);
        ArgumentOutOfRangeException.ThrowIfNegative(shadowCount);
        ArgumentOutOfRangeException.ThrowIfNegative(bestGeneration);
        ArgumentNullException.ThrowIfNull(completedFitness);

        _generation = generation;
        _shadowCount = shadowCount;
        _bestFitness = bestFitness;
        _bestShownDistance = bestShownDistance;
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
        _source.Dispose();
    }

    private void OnProgressChanged()
    {
        ApplySourceState();
    }

    private void ApplySourceState()
    {
        Update(
            _source!.Generation,
            _source.ShadowCount,
            _source.BestFitness,
            _source.BestShownDistance,
            _source.MeanFitness,
            _source.BestGeneration,
            _source.IsTrialActive,
            _source.CompletedFitness);
    }
}
