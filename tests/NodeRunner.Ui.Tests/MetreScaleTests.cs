using System.Globalization;
using NodeRunner.App.ViewModels;

namespace NodeRunner.Ui.Tests;

public sealed class MetreScaleTests
{
    private const double _earthGravity = 9.8;
    private const double _godotDefaultGravity2D = 980;

    [Fact]
    public void The_project_gravity_is_earths_at_the_metre_scale() =>
        Gravity(ProjectGodot.Lines).ShouldBe(_earthGravity * Metres.WorldUnitsPerMetre, tolerance: 1e-9);

    [Fact]
    public void Guard_ReadsTheGravityFromThePhysicsSection() =>
        Gravity(["[physics]", "", "2d/default_gravity=500", "[rendering]"]).ShouldBe(500);

    [Fact]
    public void Guard_IgnoresTheSameKeyInAnotherSection() =>
        Gravity(["[rendering]", "2d/default_gravity=500", "[physics]", "2d/solver/solver_iterations=24"])
            .ShouldBe(_godotDefaultGravity2D);

    private static double Gravity(IEnumerable<string> projectLines) =>
        ProjectGodot.Setting(projectLines, "physics", "2d/default_gravity") is { } value
            ? double.Parse(value, CultureInfo.InvariantCulture)
            : _godotDefaultGravity2D;
}
