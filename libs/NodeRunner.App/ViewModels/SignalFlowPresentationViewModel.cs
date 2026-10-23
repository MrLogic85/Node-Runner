using System.ComponentModel;
using System.Diagnostics;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The SignalFlow column's four stages at rest: 1 Inputs, 2 Network, 3 Outputs, 4 Distance.
/// Inputs shows a bar per input, Outputs a short note and Distance a readout in its body; the
/// other pictures on the cards come with #196.
/// </summary>
public sealed class SignalFlowPresentationViewModel : INotifyPropertyChanged
{
    /// <summary>The most inputs the Inputs card shows as bars; the rest are counted in <see cref="MoreInputs"/>.</summary>
    public const int MaxInputBars = 8;

    private IReadOnlyList<NetworkPort> _inputPorts = [];
    private double[] _inputValues = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>How many inputs the network takes in, such as "12"; null with none.</summary>
    public UiText? InputsNote { get; private set; }

    /// <summary>
    /// The range of each input bar: the first <see cref="MaxInputBars"/> inputs in port order. It
    /// changes only with the port layout, together with <see cref="InputsNote"/> and
    /// <see cref="MoreInputs"/>, so one notification covers all three and a view builds its bars then.
    /// </summary>
    public IReadOnlyList<PortRange> InputBars { get; private set; } = [];

    /// <summary>
    /// What each input bar shows, in <see cref="InputBars"/> order; NaN with no live network. It
    /// changes with every sample, and is notified only when a value changed.
    /// </summary>
    public IReadOnlyList<double> InputValues { get; private set; } = [];

    /// <summary>How many inputs have no bar, such as "+4"; null when every input has one.</summary>
    public UiText? MoreInputs { get; private set; }

    /// <summary>The parts the network drives, such as "1 part" or "2 parts"; null with none.</summary>
    public UiText? OutputsNote { get; private set; }

    /// <summary>
    /// How far the visible creature has got in this try, such as "12.4 m", or "Crash 3.4 m" once a
    /// beam has torn and the distance froze; null when no try runs, so the card has no body.
    /// </summary>
    public UiText? DistanceReadout { get; private set; }

    /// <summary>
    /// Whether <see cref="DistanceReadout"/> says the distance froze at a tear, so the card shows it
    /// in danger. It changes only together with the readout, so one notification covers both.
    /// </summary>
    public bool DistanceTorn { get; private set; }

    /// <summary>Refreshes the Outputs note and the distance readout from this tick.</summary>
    /// <param name="parts">How many parts the network drives (Servos, Motors and Pistons); a part can have several outputs, so the note counts parts.</param>
    /// <param name="distance">How far the visible creature has got, in world units; NaN when no try runs.</param>
    /// <param name="crashed">Whether a beam of the visible creature has torn, so <paramref name="distance"/> froze there.</param>
    public void Update(int parts, double distance, bool crashed = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(parts);

        var outputsNote = parts > 0 ? UiText.Counted("{0} part", "{0} parts", parts) : null;
        var distanceReadout = !double.IsFinite(distance) ? null
            : crashed ? TrainingPresentationViewModel.CrashText(distance)
            : Metres.WithUnit(distance);
        var distanceTorn = crashed && distanceReadout is not null;

        // Each change is notified by name, so a view applies only what changed.
        if (!Equals(OutputsNote, outputsNote))
        {
            OutputsNote = outputsNote;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OutputsNote)));
        }

        if (!Equals(DistanceReadout, distanceReadout))
        {
            DistanceReadout = distanceReadout;
            DistanceTorn = distanceTorn;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DistanceReadout)));
        }
    }

    /// <summary>
    /// Shows one sample of the visible creature's inputs. The bars are rebuilt only when the port
    /// layout changes; otherwise only the values that changed are notified.
    /// </summary>
    /// <param name="creature">The visible creature, whose part kinds give each bar its range; null with none.</param>
    /// <param name="values">The sample, taken with <paramref name="creature"/>'s port layout.</param>
    public void ShowInputs(CreatureDef? creature, NetworkPortValues values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var ports = creature is null ? [] : values.Layout.Inputs;
        if (!ReferenceEquals(ports, _inputPorts) && !ports.SequenceEqual(_inputPorts))
        {
            LayOutInputs(creature, ports);
            ReadInputs(values);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InputBars)));
            return;
        }

        _inputPorts = ports;
        if (ReadInputs(values))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InputValues)));
        }
    }

    private void LayOutInputs(CreatureDef? creature, IReadOnlyList<NetworkPort> ports)
    {
        _inputPorts = ports;
        InputBars = creature is null ? [] : [.. ports.Take(MaxInputBars).Select(port => RangeOf(creature, port))];
        _inputValues = new double[InputBars.Count];
        Array.Fill(_inputValues, double.NaN);
        InputValues = Array.AsReadOnly(_inputValues);
        InputsNote = ports.Count > 0 ? UiText.Number(ports.Count) : null;
        MoreInputs = ports.Count > MaxInputBars ? UiText.Format("+{0}", ports.Count - MaxInputBars) : null;
    }

    // The layout comes from the creature, so its parts are always there; a release build that
    // somehow misses one draws a centred bar, which clamps any value, rather than stop the refresh.
    private static PortRange RangeOf(CreatureDef creature, NetworkPort port)
    {
        var part = creature.FindPart(port.PartId);
        Debug.Assert(part is not null, $"Input part {port.PartId} is not on the creature.");
        return part is null ? PortRange.MinusOneToOne : NetworkPortDisplay.Range(part.PartKind, port);
    }

    // Copies the sample into the bars' values; true when one changed.
    private bool ReadInputs(NetworkPortValues values)
    {
        var changed = false;
        for (var i = 0; i < _inputValues.Length; i++)
        {
            var value = values.IsLive ? values.Inputs[i] : double.NaN;
            if (!value.Equals(_inputValues[i]))
            {
                _inputValues[i] = value;
                changed = true;
            }
        }

        return changed;
    }
}
