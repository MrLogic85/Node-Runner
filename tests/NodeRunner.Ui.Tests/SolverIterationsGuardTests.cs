namespace NodeRunner.Ui.Tests;

/// <summary>The 2D solver converges enough to keep creature joints pinned (#1245).</summary>
public sealed class SolverIterationsGuardTests
{
    private const int _godotDefaultIterations = 16;
    private const int _minimumIterations = 24;

    [Fact]
    public void The_project_solves_creature_joints_with_at_least_24_iterations()
    {
        var lines = File.ReadLines(Path.Combine(ThemeFile.ProjectRoot, "project.godot"));

        SolverIterations(lines).ShouldBeGreaterThanOrEqualTo(
            _minimumIterations,
            "Fewer solver iterations let beams stretch and rigid triangles flip (#1245; why: project/src/creature/AGENTS.md rule 3).");
    }

    [Fact]
    public void Guard_ReadsTheSettingFromThePhysicsSection() =>
        SolverIterations(["[physics]", "", "2d/solver/solver_iterations=24", "[rendering]"]).ShouldBe(24);

    [Fact]
    public void Guard_FallsBackToGodotsDefaultWhenTheSettingIsMissing() =>
        SolverIterations(["[physics]", "2d/default_gravity=980", "[rendering]", "2d/solver/solver_iterations=24"])
            .ShouldBe(_godotDefaultIterations);

    private static int SolverIterations(IEnumerable<string> projectLines)
    {
        const string key = "2d/solver/solver_iterations=";
        var section = "";
        foreach (var line in projectLines)
        {
            if (line.StartsWith('['))
                section = line;
            else if (section == "[physics]" && line.StartsWith(key, StringComparison.Ordinal))
                return int.Parse(line[key.Length..], System.Globalization.CultureInfo.InvariantCulture);
        }
        return _godotDefaultIterations;
    }
}
