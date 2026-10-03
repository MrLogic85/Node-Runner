using System.Globalization;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>One slider row in Part settings: its label, its readout and its thumb at 0…1.</summary>
public sealed record PartSlider(string Label, string Readout, double Position);

/// <summary>
/// A slider several selected parts share (#704). <see cref="Low"/> and <see cref="High"/> are the
/// lowest and highest shown values at 0…1; they differ when the parts' values do.
/// </summary>
public sealed record SharedSlider(PistonSetting Id, string Label, string Readout, double Low, double High)
{
    public bool ValuesDiffer => Low != High;
}

/// <summary>A Piston's three sliders in Part settings (#451).</summary>
public sealed record PistonSettingsPresentation(PartSlider Strength, PartSlider Stroke, PartSlider MaxSpeed);

/// <summary>A slider's range and step, in the units its readout shows.</summary>
public sealed record SettingRange(double Min, double Max, double Step)
{
    /// <summary>Where <paramref name="value"/> sits on the slider, 0…1.</summary>
    public double Position(double value) => Math.Clamp((value - Min) / (Max - Min), 0, 1);

    /// <summary>The value at slider <paramref name="position"/>, on a whole step.</summary>
    public double ValueAt(double position)
    {
        var steps = Math.Round(Math.Clamp(position, 0, 1) * (Max - Min) / Step);
        return Math.Round(Min + (steps * Step), 6);
    }
}

/// <summary>A Piston setting with a slider (#451); several selected Pistons share it (#704).</summary>
public enum PistonSetting
{
    Strength,
    Stroke,
    MaxSpeed,
}

/// <summary>
/// The Piston's Part settings sliders (#451): Max strength in N, Stroke as ±% of its built length
/// and Max speed in m/s. The sim stores world units (<see cref="PistonDef"/>); these convert at the
/// display boundary, like <see cref="Metres"/>.
/// </summary>
public static class PistonSettings
{
    public static SettingRange Strength { get; } = new(20, 400, 10);

    public static SettingRange Stroke { get; } = new(10, 50, 5);

    public static SettingRange MaxSpeed { get; } = new(0.5, 4, 0.1);

    public const string Note = "The brain pushes it out and pulls it in, within its stroke.";

    public static PistonSettingsPresentation For(PistonDef piston)
    {
        ArgumentNullException.ThrowIfNull(piston);
        PartSlider Slider(PistonSetting setting)
        {
            var display = DisplayOf(setting);
            var shown = display.Shown(ValueOf(piston, setting));
            return new PartSlider(display.Label, display.Readout(shown), display.Range.Position(shown));
        }

        return new PistonSettingsPresentation(Slider(PistonSetting.Strength), Slider(PistonSetting.Stroke), Slider(PistonSetting.MaxSpeed));
    }

    /// <summary>The slider for <paramref name="setting"/> over several Pistons' <paramref name="values"/>, in world units.</summary>
    public static SharedSlider Shared(PistonSetting setting, IReadOnlyCollection<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var display = DisplayOf(setting);
        var low = values.Min(display.Shown);
        var high = values.Max(display.Shown);
        var (lowText, highText) = (display.Text(low), display.Text(high));
        return lowText == highText
            ? new SharedSlider(setting, display.Label, display.Readout(high), display.Range.Position(high), display.Range.Position(high))
            : new SharedSlider(setting, display.Label, $"{display.Prefix}{lowText}–{highText}{display.Unit}", display.Range.Position(low), display.Range.Position(high));
    }

    /// <summary>The value of <paramref name="setting"/>, in world units, at slider <paramref name="position"/>.</summary>
    public static double ValueAt(PistonSetting setting, double position)
    {
        var display = DisplayOf(setting);
        return display.World(display.Range.ValueAt(position));
    }

    /// <summary><paramref name="piston"/>'s <paramref name="setting"/>, in world units.</summary>
    public static double ValueOf(PistonDef piston, PistonSetting setting)
    {
        ArgumentNullException.ThrowIfNull(piston);
        return setting switch
        {
            PistonSetting.Strength => piston.Strength,
            PistonSetting.Stroke => piston.Stroke,
            PistonSetting.MaxSpeed => piston.MaxSpeed,
            _ => throw new ArgumentOutOfRangeException(nameof(setting)),
        };
    }

    // A newton is a kilogram metre per second squared, so world force converts like world speed.
    private static Display DisplayOf(PistonSetting setting) => setting switch
    {
        PistonSetting.Strength => new("Max strength", Strength, string.Empty, "0", " N", Metres.FromWorldUnits, ToWorld),
        PistonSetting.Stroke => new("Stroke", Stroke, "±", "0", "%", value => value * 100, value => value / 100),
        PistonSetting.MaxSpeed => new("Max speed", MaxSpeed, string.Empty, "0.0", " m/s", Metres.FromWorldUnits, ToWorld),
        _ => throw new ArgumentOutOfRangeException(nameof(setting)),
    };

    private static double ToWorld(double metres) => metres * Metres.WorldUnitsPerMetre;

    private sealed record Display(
        string Label, SettingRange Range, string Prefix, string Format, string Unit, Func<double, double> Shown, Func<double, double> World)
    {
        public string Text(double shown) => shown.ToString(Format, CultureInfo.InvariantCulture);

        public string Readout(double shown) => $"{Prefix}{Text(shown)}{Unit}";
    }
}
