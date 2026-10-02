using NodeRunner.App.ViewModels;

namespace NodeRunner.Ui.Tests;

public sealed class MetreScaleTests
{
    private const string _gravitySetting = "physics/2d/default_gravity=";
    private const double _earthGravity = 9.8;
    private const double _godotDefaultGravity2D = 980;

    [Fact]
    public void The_project_gravity_is_earths_at_the_metre_scale()
    {
        var gravityLine = File.ReadLines(Path.Combine(ThemeFile.ProjectRoot, "project.godot"))
            .FirstOrDefault(line => line.StartsWith(_gravitySetting, StringComparison.Ordinal));
        var gravity = gravityLine is null
            ? _godotDefaultGravity2D
            : double.Parse(gravityLine[_gravitySetting.Length..], System.Globalization.CultureInfo.InvariantCulture);

        gravity.ShouldBe(_earthGravity * Metres.WorldUnitsPerMetre, tolerance: 1e-9);
    }
}
