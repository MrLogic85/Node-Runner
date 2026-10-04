using NodeRunner.App.Builders;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// What a part setting is (#704), as data. <see cref="MultiEditable"/> settings can take one value
/// on several selected parts; the panel shows those with a <see cref="Slider"/>, and the canvas
/// sets the rest, like a Camera's aim.
/// </summary>
public sealed record PartParameter(PartParameterId Id, bool MultiEditable, ParameterScale? Slider)
{
    public bool InPanel => Slider is not null;
}

/// <summary>
/// How a panel slider shows a setting: its label, its range and step in shown units, its readout
/// with the unit for one value ("{0} N") and for a span of values ("{0}–{1} N"), and how shown
/// units convert from and to world units, like <see cref="Metres"/>.
/// </summary>
public sealed record ParameterScale(
    UiText Label,
    SettingRange Range,
    int Decimals,
    string Readout,
    string SpanReadout,
    Func<double, double> Shown,
    Func<double, double> World)
{
    public FixedNumber Number(double shown) => new(shown, Decimals);
}

/// <summary>A slider's range and step, in the units its readout shows.</summary>
public sealed record SettingRange(double Min, double Max, double Step)
{
    /// <summary>Where <paramref name="value"/> sits on the slider, 0…1.</summary>
    public double Position(double value) => Math.Clamp((value - Min) / (Max - Min), 0, 1);

    /// <summary>One step as a share of the slider, 0…1.</summary>
    public double PositionStep => Step / (Max - Min);

    /// <summary>The value at slider <paramref name="position"/>, on a whole step.</summary>
    public double ValueAt(double position)
    {
        var steps = Math.Round(Math.Clamp(position, 0, 1) * (Max - Min) / Step);
        return Math.Round(Min + (steps * Step), 6);
    }
}

/// <summary>
/// A panel slider over the selected parts' values of one setting (#704). <see cref="Low"/> and
/// <see cref="High"/> are the lowest and highest shown values at 0…1; they differ when the parts'
/// values do. <see cref="Step"/> is one whole step at 0…1 (#711).
/// </summary>
public sealed record ParameterSlider(PartParameterId Id, UiText Label, UiText Readout, double Low, double High, double Step)
{
    public bool ValuesDiffer => Low != High;
}

/// <summary>
/// Every part setting (#704). A Piston's Max strength is in N, its Stroke ±% of its built length
/// and its Max speed in m/s (#451); a Camera's aim is turned on the canvas (#594).
/// </summary>
public static class PartParameters
{
    // A newton is a kilogram metre per second squared, so world force converts like world speed.
    public static PartParameter Strength { get; } = new(
        PartParameterId.Strength, MultiEditable: true, new(UiText.Plain("Max strength"), new(20, 400, 10), 0, "{0} N", "{0}–{1} N", Metres.FromWorldUnits, ToWorld));

    public static PartParameter Stroke { get; } = new(
        PartParameterId.Stroke, MultiEditable: true, new(UiText.Plain("Stroke"), new(10, 50, 5), 0, "±{0}%", "±{0}–{1}%", value => value * 100, value => value / 100));

    public static PartParameter MaxSpeed { get; } = new(
        PartParameterId.MaxSpeed, MultiEditable: true, new(UiText.Plain("Max speed"), new(0.5, 4, 0.1), 1, "{0} m/s", "{0}–{1} m/s", Metres.FromWorldUnits, ToWorld));

    public static PartParameter Aim { get; } = new(PartParameterId.Aim, MultiEditable: false, Slider: null);

    public static PartParameter Of(PartParameterId id) => id switch
    {
        PartParameterId.Strength => Strength,
        PartParameterId.Stroke => Stroke,
        PartParameterId.MaxSpeed => MaxSpeed,
        PartParameterId.Aim => Aim,
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
}
