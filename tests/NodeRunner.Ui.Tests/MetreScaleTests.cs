using NodeRunner.App.ViewModels;

namespace NodeRunner.Ui.Tests;

public sealed class MetreScaleTests
{
    private const string GravitySetting = "physics/2d/default_gravity=";
    private const double EarthGravity = 9.8;
    private const double GodotDefaultGravity2D = 980;

    [Fact]
    public void The_project_gravity_is_earths_at_the_metre_scale()
    {
        var gravityLine = File.ReadLines(Path.Combine(ThemeFile.ProjectRoot, "project.godot"))
            .FirstOrDefault(line => line.StartsWith(GravitySetting, StringComparison.Ordinal));
        var gravity = gravityLine is null
            ? GodotDefaultGravity2D
            : double.Parse(gravityLine[GravitySetting.Length..], System.Globalization.CultureInfo.InvariantCulture);

        gravity.ShouldBe(EarthGravity * Metres.WorldUnitsPerMetre, tolerance: 1e-9);
    }
}
