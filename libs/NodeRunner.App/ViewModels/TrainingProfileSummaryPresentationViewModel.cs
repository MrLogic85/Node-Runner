using System.ComponentModel;
using System.Globalization;

namespace NodeRunner.App.ViewModels;

public sealed class TrainingProfileSummaryPresentationViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Detail { get; private set; } = "8 candidates · 10s · 10% mutation · uniform genes";

    public void Update(
        int populationSize,
        int trialDurationSeconds,
        double mutationRate,
        string crossoverDescription)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(populationSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(trialDurationSeconds);
        if (!double.IsFinite(mutationRate) || mutationRate is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(mutationRate));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(crossoverDescription);

        var mutation = (mutationRate * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
        var detail = $"{populationSize} candidates · {trialDurationSeconds}s · {mutation} mutation · {crossoverDescription}";
        if (Detail == detail)
        {
            return;
        }

        Detail = detail;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
