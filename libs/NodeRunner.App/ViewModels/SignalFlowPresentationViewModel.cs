using System.ComponentModel;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The SignalFlow column's four stages at rest: 1 Senses, 2 Brain, 3 Outputs, 4 Distance. Each
/// stage shows a short note; the pictures on the cards come with #196.
/// </summary>
public sealed class SignalFlowPresentationViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>How many readings the brain senses, such as "9 readings"; null with none.</summary>
    public UiText? SensesNote { get; private set; }

    /// <summary>The parts the brain drives, such as "1 motor" or "2 motors"; null with none.</summary>
    public UiText? OutputsNote { get; private set; }

    /// <summary>How far the visible creature has got in this try, such as "12.4 m"; null when no try runs.</summary>
    public UiText? DistanceNote { get; private set; }

    /// <summary>Refreshes the stage notes from this tick's brain.</summary>
    /// <param name="readings">How many readings the visible creature's brain takes in.</param>
    /// <param name="pistons">How many motors the brain drives; each has two outputs, so the note counts parts.</param>
    /// <param name="distance">How far the visible creature has got, in world units; NaN when no try runs.</param>
    public void Update(int readings, int pistons, double distance)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(readings);
        ArgumentOutOfRangeException.ThrowIfNegative(pistons);

        var sensesNote = readings > 0 ? UiText.Counted("{0} reading", "{0} readings", readings) : null;
        var outputsNote = pistons > 0 ? UiText.Counted("{0} motor", "{0} motors", pistons) : null;
        var distanceNote = double.IsFinite(distance) ? Metres.WithUnit(distance) : null;
        if (Equals(SensesNote, sensesNote) && Equals(OutputsNote, outputsNote) && Equals(DistanceNote, distanceNote))
        {
            return;
        }

        // The distance changes every tick and the counts rarely, so a distance-only change says so.
        var onlyDistance = Equals(SensesNote, sensesNote) && Equals(OutputsNote, outputsNote);
        SensesNote = sensesNote;
        OutputsNote = outputsNote;
        DistanceNote = distanceNote;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(onlyDistance ? nameof(DistanceNote) : null));
    }
}
