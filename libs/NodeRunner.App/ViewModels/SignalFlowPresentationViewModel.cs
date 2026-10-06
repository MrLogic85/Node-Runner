using System.ComponentModel;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The SignalFlow column's four stages at rest: 1 Senses, 2 Brain, 3 Outputs, 4 Distance. Each
/// stage shows a short note; the pictures on the cards come with #196. Above them, the time left in
/// the run (#715).
/// </summary>
public sealed class SignalFlowPresentationViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>How many readings the brain senses, such as "9 readings"; null with none.</summary>
    public UiText? SensesNote { get; private set; }

    /// <summary>The parts the brain drives, such as "1 piston" or "2 pistons"; null with none.</summary>
    public UiText? OutputsNote { get; private set; }

    /// <summary>How far the visible creature has got in this try, such as "12.4 m"; null when no try runs.</summary>
    public UiText? DistanceNote { get; private set; }

    /// <summary>
    /// The whole seconds left in the visible creature's run (#715), such as "4 s", rounded up so the
    /// last second shows "1 s"; null when the run has no time limit or no run is going.
    /// </summary>
    public UiText? TimeLeft { get; private set; }

    /// <summary>Refreshes the stage notes from this tick's brain.</summary>
    /// <param name="readings">How many readings the visible creature's brain takes in.</param>
    /// <param name="pistons">How many Pistons the brain drives; a Piston drives two outputs, so the note counts parts.</param>
    /// <param name="distance">How far the visible creature has got, in world units; NaN when no try runs.</param>
    /// <param name="secondsLeft">The time left in the visible creature's run; NaN with no time limit or no run.</param>
    public void Update(int readings, int pistons, double distance, double secondsLeft = double.NaN)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(readings);
        ArgumentOutOfRangeException.ThrowIfNegative(pistons);
        if (secondsLeft < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(secondsLeft), secondsLeft, "The time left cannot be negative.");
        }

        var sensesNote = readings > 0 ? UiText.Counted("{0} reading", "{0} readings", readings) : null;
        // Pistons are the only output part; a new kind needs its own counted text beside this one.
        var outputsNote = pistons > 0 ? UiText.Counted("{0} piston", "{0} pistons", pistons) : null;
        var distanceNote = double.IsFinite(distance) ? Metres.WithUnit(distance) : null;
        var timeLeft = double.IsFinite(secondsLeft) ? UiText.Format("{0} s", (int)Math.Ceiling(secondsLeft)) : null;
        var countsChanged = !Equals(SensesNote, sensesNote) || !Equals(OutputsNote, outputsNote);
        var distanceChanged = !Equals(DistanceNote, distanceNote);
        var timeLeftChanged = !Equals(TimeLeft, timeLeft);
        SensesNote = sensesNote;
        OutputsNote = outputsNote;
        DistanceNote = distanceNote;
        TimeLeft = timeLeft;

        // The distance and time left change every tick and the counts rarely, so a change to only
        // those names each one; a count change names none, so everything is shown again.
        if (countsChanged)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
            return;
        }

        if (distanceChanged)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DistanceNote)));
        }

        if (timeLeftChanged)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TimeLeft)));
        }
    }
}
