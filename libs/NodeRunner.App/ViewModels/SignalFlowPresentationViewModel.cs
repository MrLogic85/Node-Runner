using System.ComponentModel;
using System.Diagnostics;
using NodeRunner.Domain;
using NodeRunner.ML.Networks;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The SignalFlow column's four stages at rest: 1 Inputs, 2 Network, 3 Outputs, 4 Distance.
/// Inputs shows a bar per input, Network its size, Outputs a dial per driven part and Distance the
/// distance, the speed and a track strip towards the best ever.
/// </summary>
public sealed class SignalFlowPresentationViewModel : INotifyPropertyChanged
{
    // The "+N" chip works like a text ellipsis: a card shows every meter when they all fit its
    // row, and otherwise the ones that fit beside the chip. The counts follow the card's row width.

    /// <summary>The most inputs the Inputs card shows as bars with no <see cref="MoreInputs"/> chip.</summary>
    public const int InputBarsThatFit = 11;

    /// <summary>How many bars the Inputs card shows beside <see cref="MoreInputs"/>, with more than <see cref="InputBarsThatFit"/> inputs.</summary>
    public const int InputBarsBesideChip = 8;

    /// <summary>The most driven parts the Outputs card shows as dials with no <see cref="MoreOutputs"/> chip.</summary>
    public const int OutputDialsThatFit = 4;

    /// <summary>How many dials the Outputs card shows beside <see cref="MoreOutputs"/>, with more than <see cref="OutputDialsThatFit"/> driven parts.</summary>
    public const int OutputDialsBesideChip = 3;

    /// <summary>
    /// The smallest <see cref="NetworkSize"/> shortened to thousands, such as "45.2k parameters";
    /// below it the count is exact, so adding a part shows exactly how many parameters it adds.
    /// </summary>
    public const int ShortenedParametersFrom = 10_000;

    private Meters _inputs = Meters.Empty();
    private Meters _outputs = Meters.Empty();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>How many inputs the network takes in, such as "12"; null with none.</summary>
    public UiText? InputsNote { get; private set; }

    /// <summary>
    /// The range of each input bar: the first inputs in port order, as many as fit. It
    /// changes only with the port layout, together with <see cref="InputsNote"/>, <see cref="MoreInputs"/>
    /// and <see cref="HasNoInputs"/>, so one notification covers them all and a view builds its bars then.
    /// </summary>
    public IReadOnlyList<PortRange> InputBars => _inputs.Ranges;

    /// <summary>
    /// What each input bar shows, in <see cref="InputBars"/> order; NaN with no live network. It
    /// changes with every sample, and is notified only when a value changed.
    /// </summary>
    public IReadOnlyList<double> InputValues => _inputs.Values;

    /// <summary>How many inputs have no bar, such as "+4"; null when every input has one.</summary>
    public UiText? MoreInputs { get; private set; }

    /// <summary>
    /// Whether the visible creature has no inputs, so the Inputs card says so rather than collapse;
    /// false with no creature. It is notified as <see cref="InputBars"/>.
    /// </summary>
    public bool HasNoInputs { get; private set; }

    /// <summary>
    /// How many learnable parameters the network has, such as "91 parameters", or from
    /// <see cref="ShortenedParametersFrom"/> "45.2k parameters", "123k parameters" or "1.2M parameters";
    /// null with no creature. It changes only with the port layout.
    /// </summary>
    public UiText? NetworkSize { get; private set; }

    /// <summary>How many output ports the network sets, such as "7"; null with none.</summary>
    public UiText? OutputsNote { get; private set; }

    /// <summary>
    /// The range of each output dial: one per driven part in port order, showing the part's drive,
    /// its first output that is not a Strength. It changes only with the port layout, together with
    /// <see cref="OutputsNote"/>, <see cref="MoreOutputs"/> and <see cref="HasNoOutputs"/>, so one
    /// notification covers them all.
    /// </summary>
    public IReadOnlyList<PortRange> OutputDials => _outputs.Ranges;

    /// <summary>
    /// What each output dial shows, in <see cref="OutputDials"/> order; NaN with no live network. It
    /// changes with every sample, and is notified only when a value changed.
    /// </summary>
    public IReadOnlyList<double> OutputValues => _outputs.Values;

    /// <summary>How many driven parts have no dial, such as "+2"; null when every part has one.</summary>
    public UiText? MoreOutputs { get; private set; }

    /// <summary>
    /// Whether the visible creature has no driven parts, so the Outputs card says so rather than
    /// collapse; false with no creature. It is notified as <see cref="OutputDials"/>.
    /// </summary>
    public bool HasNoOutputs { get; private set; }

    /// <summary>
    /// How far the visible creature has got in this try, such as "12.4 m", or "Crash 3.4 m" once a
    /// beam has torn and the distance froze; null when no try runs, so the card has no body.
    /// </summary>
    public UiText? DistanceReadout { get; private set; }

    /// <summary>Whether <see cref="DistanceReadout"/> says the distance froze at a tear, so the card shows it in danger.</summary>
    public bool DistanceTorn { get; private set; }

    /// <summary>
    /// How fast the visible creature moves forward now, signed, such as "−0.8 m/s"; null when no try
    /// runs or after a tear.
    /// </summary>
    public UiText? SpeedReadout { get; private set; }

    /// <summary>
    /// How much of the track strip the distance fills, 0…1. The track's full length is the farther
    /// of the best ever and the distance, so the bar is full while the try is past the best; it is
    /// 0 when that length is 0.
    /// </summary>
    public double TrackFill { get; private set; }

    /// <summary>Where the best ever stands on the track strip, 0…1: at 0 with no best ever, or when the full length is 0.</summary>
    public double TrackMarker { get; private set; }

    /// <summary>The track strip's full length, such as "25.0 m"; null when no try runs.</summary>
    public UiText? TrackEnd { get; private set; }

    /// <summary>
    /// The longest <see cref="TrackEnd"/> the card keeps room for, "999.9 m", so the strip keeps
    /// its width while the track grows; a longer track widens the end label.
    /// </summary>
    public static UiText TrackEndRoom { get; } = Metres.WithUnit(999.9 * Metres.WorldUnitsPerMetre);

    /// <summary>
    /// Refreshes the Distance card from this tick. When a text on it or <see cref="DistanceTorn"/>
    /// changed, <see cref="DistanceReadout"/> is notified for the whole card; when only the strip
    /// moved, <see cref="TrackFill"/> is notified for <see cref="TrackFill"/> and <see cref="TrackMarker"/>.
    /// </summary>
    /// <param name="distance">How far the visible creature has got, in world units; NaN when no try runs.</param>
    /// <param name="speed">How fast its centre moves forward now, in world units per second; NaN with no speed to show.</param>
    /// <param name="best">The best ever distance on this map, in world units, as the arena's best marker shows it; NaN until there is one.</param>
    /// <param name="crashed">Whether a beam of the visible creature has torn, so <paramref name="distance"/> froze there.</param>
    public void Update(double distance, double speed, double best, bool crashed = false)
    {
        var running = double.IsFinite(distance);
        var torn = crashed && running;
        var distanceReadout = !running ? null
            : torn ? TrainingPresentationViewModel.CrashText(distance)
            : Metres.WithUnit(distance);
        var speedReadout = running && !torn && double.IsFinite(speed) ? Metres.SpeedWithUnit(speed) : null;
        var (fill, marker, full) = running ? Track(distance, best) : (0, 0, 0);
        var trackEnd = running ? Metres.WithUnit(full) : null;
        var textChanged = !Equals(DistanceReadout, distanceReadout)
            || DistanceTorn != torn
            || !Equals(SpeedReadout, speedReadout)
            || !Equals(TrackEnd, trackEnd);
        var trackChanged = !TrackFill.Equals(fill) || !TrackMarker.Equals(marker);
        DistanceReadout = distanceReadout;
        DistanceTorn = torn;
        SpeedReadout = speedReadout;
        TrackFill = fill;
        TrackMarker = marker;
        TrackEnd = trackEnd;
        if (textChanged)
        {
            Notify(nameof(DistanceReadout));
        }
        else if (trackChanged)
        {
            Notify(nameof(TrackFill));
        }
    }

    // NaN, or no best yet, counts as 0 m, so the bar is full and the marker sits at its left end.
    private static (double Fill, double Marker, double Full) Track(double distance, double best)
    {
        var bestSoFar = double.IsFinite(best) ? Math.Max(0, best) : 0;
        var full = Math.Max(bestSoFar, distance);
        return full > 0 ? (Math.Clamp(distance / full, 0, 1), bestSoFar / full, full) : (0, 0, 0);
    }

    /// <summary>
    /// Shows one sample of the visible creature's ports. A stage's bars or dials are rebuilt only
    /// when its port layout changes; otherwise only the values that changed are notified. Each
    /// change is notified by name, so a view applies only what changed.
    /// </summary>
    /// <param name="creature">The visible creature, whose part kinds give each bar and dial its range; null with none.</param>
    /// <param name="values">The sample, taken with <paramref name="creature"/>'s port layout.</param>
    public void ShowPorts(CreatureDef? creature, NetworkPortValues values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var inputs = creature is null ? [] : values.Layout.Inputs;
        var inputValues = values.IsLive ? values.Inputs : null;
        var noInputs = creature is not null && inputs.Count == 0;
        var laidOut = false;
        if (!_inputs.Matches(inputs) || noInputs != HasNoInputs)
        {
            laidOut = true;
            HasNoInputs = noInputs;
            _inputs = LayOutInputs(creature, inputs);
            _inputs.Read(inputValues);
            Notify(nameof(InputBars));
        }
        else if (_inputs.Read(inputValues))
        {
            Notify(nameof(InputValues));
        }

        var outputs = creature is null ? [] : values.Layout.Outputs;
        var outputValues = values.IsLive ? values.Outputs : null;
        var noOutputs = creature is not null && outputs.Count == 0;
        if (!_outputs.Matches(outputs) || noOutputs != HasNoOutputs)
        {
            laidOut = true;
            HasNoOutputs = noOutputs;
            _outputs = LayOutOutputs(creature, outputs);
            _outputs.Read(outputValues);
            Notify(nameof(OutputDials));
        }
        else if (_outputs.Read(outputValues))
        {
            Notify(nameof(OutputValues));
        }

        if (laidOut)
        {
            ShowNetworkSize(creature is null ? null : Parameters(DirectNetwork.GenomeLength(values.Layout)));
        }
    }

    /// <summary>A network size of <paramref name="count"/> learnable parameters, as <see cref="NetworkSize"/> words it.</summary>
    public static UiText Parameters(int count) =>
        count < ShortenedParametersFrom ? UiText.Counted("{0} parameter", "{0} parameters", count)
        : count < 999_500 ? UiText.Format("{0}k parameters", Shortened(count / 1e3))
        : UiText.Format("{0}M parameters", Shortened(count / 1e6));

    // One decimal while the rounded value stays under 100, so 99,960 reads 100k, not 100.0k.
    private static FixedNumber Shortened(double value) => new(value, value < 99.95 ? 1 : 0);

    private void ShowNetworkSize(UiText? networkSize)
    {
        if (!Equals(networkSize, NetworkSize))
        {
            NetworkSize = networkSize;
            Notify(nameof(NetworkSize));
        }
    }

    private Meters LayOutInputs(CreatureDef? creature, IReadOnlyList<NetworkPort> ports)
    {
        InputsNote = ports.Count > 0 ? UiText.Number(ports.Count) : null;
        var shown = Shown(ports.Count, InputBarsThatFit, InputBarsBesideChip);
        MoreInputs = ports.Count > shown ? UiText.Format("+{0}", ports.Count - shown) : null;
        return Meters.Of(creature, ports, [.. Enumerable.Range(0, shown)]);
    }

    // Every driven part has exactly one output that is not a Strength, so each gets one dial.
    private Meters LayOutOutputs(CreatureDef? creature, IReadOnlyList<NetworkPort> ports)
    {
        var drives = Enumerable.Range(0, ports.Count)
            .Where(index => ports[index].Signal != PortSignal.Strength)
            .DistinctBy(index => ports[index].PartId)
            .ToArray();
        var shown = Shown(drives.Length, OutputDialsThatFit, OutputDialsBesideChip);
        OutputsNote = ports.Count > 0 ? UiText.Number(ports.Count) : null;
        MoreOutputs = drives.Length > shown ? UiText.Format("+{0}", drives.Length - shown) : null;
        return Meters.Of(creature, ports, drives[..shown]);
    }

    private static int Shown(int count, int fit, int besideChip) => count <= fit ? count : besideChip;

    private void Notify(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    // The bars or dials of one stage: the ports laid out, and for each meter the index of its port
    // in the sample, its range and its value.
    private sealed class Meters
    {
        private readonly int[] _sampleIndices;
        private readonly double[] _values;

        private Meters(IReadOnlyList<NetworkPort> ports, int[] sampleIndices, PortRange[] ranges)
        {
            Ports = ports;
            _sampleIndices = sampleIndices;
            _values = new double[sampleIndices.Length];
            Array.Fill(_values, double.NaN);
            Ranges = Array.AsReadOnly(ranges);
            Values = Array.AsReadOnly(_values);
        }

        // Each stage has its own, since Matches keeps the newest equal port list on it.
        public static Meters Empty() => new([], [], []);

        public IReadOnlyList<NetworkPort> Ports { get; private set; }

        public IReadOnlyList<PortRange> Ranges { get; }

        public IReadOnlyList<double> Values { get; }

        public static Meters Of(CreatureDef? creature, IReadOnlyList<NetworkPort> ports, int[] sampleIndices) =>
            new(ports, sampleIndices, [.. sampleIndices.Select(index => RangeOf(creature, ports[index]))]);

        /// <summary>
        /// Whether these meters show <paramref name="ports"/>. Another shadow brings an equal list of
        /// its own; keeping it lets the next samples match by reference, without comparing the ports.
        /// </summary>
        public bool Matches(IReadOnlyList<NetworkPort> ports)
        {
            if (!ReferenceEquals(ports, Ports) && !ports.SequenceEqual(Ports))
            {
                return false;
            }

            Ports = ports;
            return true;
        }

        // Copies the sample into the values, NaN with no live network; true when one changed.
        public bool Read(IReadOnlyList<double>? sample)
        {
            var changed = false;
            for (var i = 0; i < _values.Length; i++)
            {
                var value = sample is null ? double.NaN : sample[_sampleIndices[i]];
                if (!value.Equals(_values[i]))
                {
                    _values[i] = value;
                    changed = true;
                }
            }

            return changed;
        }

        // The layout comes from the creature, so its parts are always there; a release build that
        // somehow misses one draws a centred meter, which clamps any value, rather than stop the refresh.
        private static PortRange RangeOf(CreatureDef? creature, NetworkPort port)
        {
            var part = creature?.FindPart(port.PartId);
            Debug.Assert(part is not null, $"Network part {port.PartId} is not on the creature.");
            return part is null ? PortRange.MinusOneToOne : NetworkPortDisplay.Range(part.PartKind, port);
        }
    }
}
