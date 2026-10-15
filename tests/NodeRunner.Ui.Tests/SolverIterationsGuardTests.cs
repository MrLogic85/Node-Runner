namespace NodeRunner.Ui.Tests;

/// <summary>The 2D solver converges enough to keep creature joints pinned (#1245).</summary>
public sealed class SolverIterationsGuardTests
{
    private const int _godotDefaultIterations = 16;
    private const int _minimumIterations = 24;

    [Fact]
    public void The_project_solves_creature_joints_with_at_least_24_iterations()
    {
        SolverIterations(ProjectGodot.Lines).ShouldBeGreaterThanOrEqualTo(
            _minimumIterations,
            "Fewer solver iterations let beams stretch and rigid triangles flip (#1245; why: .claude/rules/creature.md rule 3).");
    }

    [Fact]
    public void Guard_ReadsTheSettingFromThePhysicsSection() =>
        SolverIterations(["[physics]", "", "2d/solver/solver_iterations=24", "[rendering]"]).ShouldBe(24);

    [Fact]
    public void Guard_FallsBackToGodotsDefaultWhenTheSettingIsMissing() =>
        SolverIterations(["[physics]", "2d/default_gravity=980", "[rendering]", "2d/solver/solver_iterations=24"])
            .ShouldBe(_godotDefaultIterations);

    private static int SolverIterations(IEnumerable<string> projectLines) =>
        ProjectGodot.Setting(projectLines, "physics", "2d/solver/solver_iterations") is { } value
            ? int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)
            : _godotDefaultIterations;
}
