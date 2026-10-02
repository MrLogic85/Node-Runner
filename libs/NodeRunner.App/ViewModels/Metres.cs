using System.Globalization;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The metre the player reads (#670). The sim measures in world units; text converts at the
/// display boundary, so fitness and saves never change with it.
/// </summary>
public static class Metres
{
    /// <summary>
    /// World units in one metre. Godot has no 2D metre, but its default 2D gravity, which the
    /// project uses, is 980 units/s²: Earth's 9.8 m/s² at 100 units per metre.
    /// </summary>
    public const double WorldUnitsPerMetre = 100;

    public static double FromWorldUnits(double worldUnits) => worldUnits / WorldUnitsPerMetre;

    /// <summary>
    /// A world-unit quantity at metre scale to one decimal, without a unit, such as "2.5": a length
    /// becomes metres and a speed in units/s becomes m/s.
    /// </summary>
    public static string Format(double worldUnits) =>
        FromWorldUnits(worldUnits).ToString("0.0", CultureInfo.InvariantCulture);

    /// <summary>A world-unit length in metres with its unit, such as "2.5 m".</summary>
    public static string FormatWithUnit(double worldUnits) => $"{Format(worldUnits)} m";
}
