using System.ComponentModel;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

public sealed class UnlockProgressPresentationViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title { get; private set; } = "Unlock";

    public string Detail { get; private set; } = "Reach 50.0 m to unlock one extra core slot";

    public double Progress { get; private set; }

    public bool IsUnlocked { get; private set; }

    public void Update(ProgressionDef progression, double bestFitness, double threshold)
    {
        ArgumentNullException.ThrowIfNull(progression);
        if (!double.IsFinite(threshold) || threshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold));
        }

        var best = double.IsNegativeInfinity(bestFitness) ? 0 : Math.Max(0, bestFitness);
        var title = progression.ExtraCoreUnlocked ? "Extra core unlocked" : "Next unlock";
        var detail = progression.ExtraCoreUnlocked
            ? $"Extra core slot available · earned generation {progression.ExtraCoreUnlockedAtGeneration}"
            : $"Reach {threshold:0.0} m to unlock one extra core slot · {Math.Min(best, threshold):0.0}/{threshold:0.0} m";
        var progress = progression.ExtraCoreUnlocked ? 1 : Math.Clamp(best / threshold, 0, 1);

        if (Title == title && Detail == detail && Progress.Equals(progress) && IsUnlocked == progression.ExtraCoreUnlocked)
        {
            return;
        }

        Title = title;
        Detail = detail;
        Progress = progress;
        IsUnlocked = progression.ExtraCoreUnlocked;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
