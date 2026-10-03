using System.Globalization;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// One stepped slider row: its label, its readout, its thumb at 0…1 and the distance between two
/// whole steps on that scale, so the thumb stops only where the value does (#711).
/// </summary>
public sealed record PartSlider(string Label, string Readout, double Position, double Step);

/// <summary>A Piston's three sliders in Part settings (#451).</summary>
public sealed record PistonSettingsPresentation(PartSlider Strength, PartSlider Stroke, PartSlider MaxSpeed);

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
        var strength = Newtons(piston.Strength);
        var stroke = piston.Stroke * 100;
        var maxSpeed = Metres.FromWorldUnits(piston.MaxSpeed);
        return new PistonSettingsPresentation(
            new PartSlider("Max strength", $"{strength.ToString("0", CultureInfo.InvariantCulture)} N", Strength.Position(strength), Strength.PositionStep),
            new PartSlider("Stroke", $"±{stroke.ToString("0", CultureInfo.InvariantCulture)}%", Stroke.Position(stroke), Stroke.PositionStep),
            new PartSlider("Max speed", $"{maxSpeed.ToString("0.0", CultureInfo.InvariantCulture)} m/s", MaxSpeed.Position(maxSpeed), MaxSpeed.PositionStep));
    }

    /// <summary>The Max strength, in world units, at slider <paramref name="position"/>.</summary>
    public static double StrengthAt(double position) => Strength.ValueAt(position) * Metres.WorldUnitsPerMetre;

    /// <summary>The Stroke, as a share of the built length, at slider <paramref name="position"/>.</summary>
    public static double StrokeAt(double position) => Stroke.ValueAt(position) / 100;

    /// <summary>The Max speed, in world units per second, at slider <paramref name="position"/>.</summary>
    public static double MaxSpeedAt(double position) => MaxSpeed.ValueAt(position) * Metres.WorldUnitsPerMetre;

    // A newton is a kilogram metre per second squared; world forces are in units per second squared.
    private static double Newtons(double worldForce) => worldForce / Metres.WorldUnitsPerMetre;
}
