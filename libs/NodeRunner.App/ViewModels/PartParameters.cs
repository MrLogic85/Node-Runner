using NodeRunner.App.Builders;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// What a part setting is (#704), as data. <see cref="MultiEditable"/> settings can take one value
/// on several selected parts; the panel shows those with a <see cref="Slider"/>, and the canvas
/// sets the rest, like a Camera's aim. <see cref="Advanced"/> settings sit in the panel's
/// Advanced section (#903); the flag belongs to the setting, so it is the same on every part.
/// A setting that <see cref="ChangesPorts"/> adds or removes brain ports, so a locked creation
/// keeps it (#578, #896). <see cref="NoEffectReason"/> says why a setting that can do nothing
/// as set now, like a one-ray Camera's Spread, shows disabled (#578).
/// </summary>
public sealed record PartParameter(PartParameterId Id, bool MultiEditable, ParameterScale? Slider, bool Advanced, bool ChangesPorts = false, UiText? NoEffectReason = null)
{
    public bool InPanel => Slider is not null;
}

/// <summary>
/// How a panel slider shows a setting: its label, the one line shown while its slider is touched (#867), its range
/// and step in shown units, its readout with the unit for one value ("{0} N") and for a span of
/// values ("{0}–{1} N"), and how shown units convert from and to world units, like <see cref="Metres"/>.
/// </summary>
public sealed record ParameterScale(
    UiText Label,
    UiText Help,
    SettingRange Range,
    int Decimals,
    string Readout,
    string SpanReadout,
    Func<double, double> Shown,
    Func<double, double> World)
{
    public FixedNumber Number(double shown) => new(shown, Decimals);
}

/// <summary>
/// A slider's range and step, in the units its readout shows. With <see cref="Stops"/> it snaps to
/// those values instead, spread evenly along the slider whatever the gaps between them (#801).
/// </summary>
public sealed record SettingRange(double Min, double Max, double Step)
{
    /// <summary>The values a stepped slider snaps to, strictly increasing; null for an even step.</summary>
    public IReadOnlyList<double>? Stops { get; private init; }

    /// <summary>A slider that snaps to <paramref name="stops"/>, given in strictly increasing order.</summary>
    public static SettingRange Of(params double[] stops)
    {
        ArgumentNullException.ThrowIfNull(stops);
        if (stops.Length < 2)
        {
            throw new ArgumentException("A stepped slider needs at least two stops.", nameof(stops));
        }

        for (var i = 1; i < stops.Length; i++)
        {
            if (!(stops[i] > stops[i - 1]))
            {
                throw new ArgumentException("A stepped slider's stops must strictly increase.", nameof(stops));
            }
        }

        return new(stops[0], stops[^1], 0) { Stops = [.. stops] };
    }

    /// <summary>Where <paramref name="value"/> sits on the slider, 0…1; between two stops it sits between theirs.</summary>
    public double Position(double value)
    {
        if (Stops is null)
        {
            return Math.Clamp((value - Min) / (Max - Min), 0, 1);
        }

        var clamped = Math.Clamp(value, Min, Max);
        var upper = 1;
        while (upper < Stops.Count - 1 && Stops[upper] < clamped)
        {
            upper++;
        }

        var lower = Stops[upper - 1];
        return (upper - 1 + ((clamped - lower) / (Stops[upper] - lower))) / (Stops.Count - 1);
    }

    /// <summary>Whether shown value <paramref name="value"/> lies on the slider, allowing for rounding in unit conversions.</summary>
    public bool Allows(double value) => value >= Min - _rounding && value <= Max + _rounding;

    private const double _rounding = 1e-6;

    /// <summary>One step as a share of the slider, 0…1.</summary>
    public double PositionStep => Stops is null ? Step / (Max - Min) : 1.0 / (Stops.Count - 1);

    /// <summary>The value at slider <paramref name="position"/>, on a whole step.</summary>
    public double ValueAt(double position)
    {
        if (Stops is not null)
        {
            return Stops[(int)Math.Round(Math.Clamp(position, 0, 1) * (Stops.Count - 1))];
        }

        var steps = Math.Round(Math.Clamp(position, 0, 1) * (Max - Min) / Step);
        return Math.Round(Min + (steps * Step), 6);
    }
}

/// <summary>
/// A panel slider over the selected parts' values of one setting (#704). <see cref="Low"/> and
/// <see cref="High"/> are the lowest and highest shown values at 0…1; they differ when the parts'
/// values do. <see cref="Step"/> is one whole step at 0…1 (#711). A <see cref="Disabled"/> slider
/// is shown but can't be moved: a setting a locked creation keeps (<see cref="Locked"/>), whose tap
/// says why like the tray's locked rows (#896), or one that does nothing on these parts
/// (<see cref="NoEffect"/>), like a one-ray Camera's spread (#578).
/// </summary>
public sealed record ParameterSlider(PartParameterId Id, UiText Label, UiText Readout, double Low, double High, double Step, bool Locked = false, bool NoEffect = false)
{
    public bool Disabled => Locked || NoEffect;

    public bool ValuesDiffer => Low != High;

    public bool Advanced => PartParameters.Of(Id).Advanced;
}

/// <summary>
/// Every part setting (#704). A Piston's Max strength is in N, its Stroke % of its shortest gap between its joints' edges (#835),
/// its Start position % of its travel (#870), its Max speed in m/s (#451) and its Rise time in s
/// (#801); a Spring's Stiffness is in N/m, its Damping in N·s/m (#453, #801), and its Stroke and
/// Coil length in % like a Piston's Stroke and Start position (#835); a Wheel's Radius is in m and its
/// Grip in % (#129); a Camera's aim is turned on the canvas (#594), and its Rays snap to 1, 3 or 5,
/// its Spread is in degrees and its Range in m (#578).
/// </summary>
public static class PartParameters
{
    // A newton is a kilogram metre per second squared, so world force converts like world speed.
    public static PartParameter Strength { get; } = new(
        PartParameterId.Strength, MultiEditable: true, new(UiText.Plain("Max strength"), UiText.Plain("The most force it can push or pull with"), new(20, 400, 10), 0, "{0} N", "{0}–{1} N", Metres.FromWorldUnits, ToWorld), Advanced: false);

    public static PartParameter ServoStrength { get; } = new(
        PartParameterId.ServoStrength, MultiEditable: true, new(UiText.Plain("Max strength"), UiText.Plain("The most force it can turn with"), new(5, 200, 5), 0, "{0} N·m", "{0}–{1} N·m", value => value / 10000, value => value * 10000), Advanced: false);

    public static PartParameter Stroke { get; } = new(
        PartParameterId.Stroke, MultiEditable: true, new(UiText.Plain("Stroke"), UiText.Plain("How far it moves in and out"), new(10, 100, 5), 0, "{0}%", "{0}–{1}%", value => value * 100, value => value / 100), Advanced: false);

    public static PartParameter Range { get; } = new(
        PartParameterId.Range, MultiEditable: true, new(UiText.Plain("Range"), UiText.Plain("How far it can turn"), new(20, 360, 5), 0, "{0}°", "{0}–{1}°", RadiansToDegrees, DegreesToRadians), Advanced: false);

    public static PartParameter StartPosition { get; } = new(
        PartParameterId.StartPosition, MultiEditable: true, new(UiText.Plain("Start position"), UiText.Plain("Where it starts in its stroke"), new(0, 100, 5), 0, "{0}%", "{0}–{1}%", value => value * 100, value => value / 100), Advanced: true);

    public static PartParameter ServoStartPosition { get; } = new(
        PartParameterId.ServoStartPosition, MultiEditable: true, new(UiText.Plain("Start position"), UiText.Plain("Where it starts in its rotation"), new(0, 100, 5), 0, "{0}%", "{0}–{1}%", value => value * 100, value => value / 100), Advanced: true);

    public static PartParameter MaxSpeed { get; } = new(
        PartParameterId.MaxSpeed, MultiEditable: true, new(UiText.Plain("Max speed"), UiText.Plain("How fast it's allowed to move"), new(0.5, 4, 0.1), 1, "{0} m/s", "{0}–{1} m/s", Metres.FromWorldUnits, ToWorld), Advanced: true);

    public static PartParameter AngularMaxSpeed { get; } = new(
        PartParameterId.AngularMaxSpeed, MultiEditable: true, new(UiText.Plain("Max speed"), UiText.Plain("How fast it's allowed to move"), new(30, 720, 15), 0, "{0}°/s", "{0}–{1}°/s", RadiansToDegrees, DegreesToRadians), Advanced: true);

    public static PartParameter RiseTime { get; } = new(
        PartParameterId.RiseTime, MultiEditable: true, new(UiText.Plain("Rise time"), UiText.Plain("How quickly it reaches full force"), SettingRange.Of(0.1, 0.2, 0.5, 1), 1, "{0} s", "{0}–{1} s", value => value, value => value), Advanced: true);

    // World force per world unit is N/m: both scale by world units per metre, which cancel.
    public static PartParameter Stiffness { get; } = new(
        PartParameterId.Stiffness, MultiEditable: true, new(UiText.Plain("Stiffness"), UiText.Plain("How hard it springs back"), new(SpringDef.SoftestStiffness, SpringDef.StiffestStiffness, 50), 0, "{0} N/m", "{0}–{1} N/m", value => value, value => value), Advanced: false);

    // World force per world speed is N·s/m, for the same reason.
    public static PartParameter Damping { get; } = new(
        PartParameterId.Damping, MultiEditable: true, new(UiText.Plain("Damping"), UiText.Plain("How quickly it stops bouncing"), new(0, 100, 1), 0, "{0} N·s/m", "{0}–{1} N·s/m", value => value, value => value), Advanced: true);

    // In whole percent, as a new Spring's stops sit at a third and two thirds of it (#835).
    public static PartParameter CoilLength { get; } = new(
        PartParameterId.CoilLength, MultiEditable: true, new(UiText.Plain("Coil length"), UiText.Plain("The length the coil wants to be"), new(SpringDef.MinCoilLength * 100, SpringDef.MaxCoilLength * 100, 1), 0, "{0}%", "{0}–{1}%", value => value * 100, value => value / 100), Advanced: true);

    public static PartParameter WheelRadius { get; } = new(
        PartParameterId.WheelRadius, MultiEditable: true, new(UiText.Plain("Radius"), UiText.Plain("Bigger wheels roll over bumps but weigh more"), new(Metres.FromWorldUnits(WheelDef.MinRadius), Metres.FromWorldUnits(WheelDef.MaxRadius), 0.1), 1, "{0} m", "{0}–{1} m", Metres.FromWorldUnits, ToWorld), Advanced: false);

    public static PartParameter Grip { get; } = new(
        PartParameterId.Grip, MultiEditable: true, new(UiText.Plain("Grip"), UiText.Plain("How well the tyre holds the ground. Low grip slides"), new(0, 100, 10), 0, "{0}%", "{0}–{1}%", value => value * 100, value => value / 100), Advanced: false);

    public static PartParameter Aim { get; } = new(PartParameterId.Aim, MultiEditable: false, Slider: null, Advanced: false);

    // Odd counts, so the centre ray is the aim; more rays add brain ports.
    public static PartParameter Rays { get; } = new(
        PartParameterId.Rays, MultiEditable: true, new(UiText.Plain("Rays"), UiText.Plain("How many rays it looks along"), SettingRange.Of(1, 3, 5), 0, "{0}", "{0}–{1}", value => value, value => value), Advanced: false, ChangesPorts: true);

    // The angle between the outer rays.
    public static PartParameter Spread { get; } = new(
        PartParameterId.Spread, MultiEditable: true, new(UiText.Plain("Spread"), UiText.Plain("How wide its rays fan out"), new(15, 90, 5), 0, "{0}°", "{0}–{1}°", RadiansToDegrees, DegreesToRadians), Advanced: false,
        NoEffectReason: UiText.Plain("One ray has no spread."));

    // A Worm's beam is 0.7 m and the build area 12 m wide; the default 2.2 m is on a step.
    public static PartParameter CameraRange { get; } = new(
        PartParameterId.CameraRange, MultiEditable: true, new(UiText.Plain("Range"), UiText.Plain("How far it can see"), new(1, 4, 0.1), 1, "{0} m", "{0}–{1} m", Metres.FromWorldUnits, ToWorld), Advanced: false);

    public static PartParameter Of(PartParameterId id) => id switch
    {
        PartParameterId.Strength => Strength,
        PartParameterId.ServoStrength => ServoStrength,
        PartParameterId.Stroke => Stroke,
        PartParameterId.Range => Range,
        PartParameterId.StartPosition => StartPosition,
        PartParameterId.ServoStartPosition => ServoStartPosition,
        PartParameterId.MaxSpeed => MaxSpeed,
        PartParameterId.AngularMaxSpeed => AngularMaxSpeed,
        PartParameterId.RiseTime => RiseTime,
        PartParameterId.Aim => Aim,
        PartParameterId.Stiffness => Stiffness,
        PartParameterId.Damping => Damping,
        PartParameterId.CoilLength => CoilLength,
        PartParameterId.WheelRadius => WheelRadius,
        PartParameterId.Grip => Grip,
        PartParameterId.Rays => Rays,
        PartParameterId.Spread => Spread,
        PartParameterId.CameraRange => CameraRange,
        _ => throw new ArgumentOutOfRangeException(nameof(id)),
    };

    /// <summary>The panel slider for <paramref name="id"/> over the parts' <paramref name="values"/>, in world units.</summary>
    public static ParameterSlider SliderOver(PartParameterId id, IReadOnlyCollection<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var scale = ScaleOf(id);
        var low = values.Min(scale.Shown);
        var high = values.Max(scale.Shown);
        var shownLow = scale.Number(low);
        var shownHigh = scale.Number(high);
        // One value when both ends read the same.
        return shownLow == shownHigh
            ? new ParameterSlider(id, scale.Label, UiText.Format(scale.Readout, shownHigh), scale.Range.Position(high), scale.Range.Position(high), scale.Range.PositionStep)
            : new ParameterSlider(id, scale.Label, UiText.Format(scale.SpanReadout, shownLow, shownHigh), scale.Range.Position(low), scale.Range.Position(high), scale.Range.PositionStep);
    }

    /// <summary>The value of <paramref name="id"/>, in world units, at slider <paramref name="position"/>.</summary>
    public static double ValueAt(PartParameterId id, double position)
    {
        var scale = ScaleOf(id);
        return scale.World(scale.Range.ValueAt(position));
    }

    private static ParameterScale ScaleOf(PartParameterId id) =>
        Of(id).Slider ?? throw new ArgumentOutOfRangeException(nameof(id), "This setting has no panel slider.");

    private static double ToWorld(double metres) => metres * Metres.WorldUnitsPerMetre;

    private static double RadiansToDegrees(double radians) => radians * 180 / Math.PI;

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}
