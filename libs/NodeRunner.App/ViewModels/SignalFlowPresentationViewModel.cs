using System.ComponentModel;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The SignalFlow column's four stages at rest: 1 Inputs, 2 Network, 3 Outputs, 4 Distance. Each
/// stage shows a short note; the pictures on the cards come with #196.
/// </summary>
public sealed class SignalFlowPresentationViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>How many readings the network senses, such as "9 readings"; null with none.</summary>
    public UiText? InputsNote { get; private set; }

    /// <summary>The parts the network drives, such as "1 part" or "2 parts"; null with none.</summary>
    public UiText? OutputsNote { get; private set; }

    /// <summary>
    /// How far the visible creature has got in this try, such as "12.4 m", or "Crash 3.4 m" once a
    /// beam has torn and the distance froze (#1257); null when no try runs.
    /// </summary>
    public UiText? DistanceNote { get; private set; }

    /// <summary>Refreshes the stage notes from this tick's network.</summary>
    /// <param name="readings">How many readings the visible creature's network takes in.</param>
    /// <param name="parts">How many parts the network drives (Servos, Motors and Pistons); a part can have several outputs, so the note counts parts.</param>
    /// <param name="distance">How far the visible creature has got, in world units; NaN when no try runs.</param>
    /// <param name="crashed">Whether a beam of the visible creature has torn, so <paramref name="distance"/> froze there (#1257).</param>
    public void Update(int readings, int parts, double distance, bool crashed = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(readings);
        ArgumentOutOfRangeException.ThrowIfNegative(parts);

        var inputsNote = readings > 0 ? UiText.Counted("{0} reading", "{0} readings", readings) : null;
        var outputsNote = parts > 0 ? UiText.Counted("{0} part", "{0} parts", parts) : null;
        var distanceNote = !double.IsFinite(distance) ? null
            : crashed ? TrainingPresentationViewModel.CrashText(distance)
            : Metres.WithUnit(distance);
        if (Equals(InputsNote, inputsNote) && Equals(OutputsNote, outputsNote) && Equals(DistanceNote, distanceNote))
        {
            return;
        }

        // The distance changes every tick and the counts rarely, so a distance-only change says so.
        var onlyDistance = Equals(InputsNote, inputsNote) && Equals(OutputsNote, outputsNote);
        InputsNote = inputsNote;
        OutputsNote = outputsNote;
        DistanceNote = distanceNote;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(onlyDistance ? nameof(DistanceNote) : null));
    }
}
