using NodeRunner.Domain;

namespace NodeRunner.App.Lifecycle;

/// <summary>
/// Whether a drawn creature can be simulated and trained. Any drawing can be saved
/// (<see cref="CreatureDef"/>); these rules only stop training. The problems are worded for the
/// player, and Build's readiness line shortens them.
/// </summary>
public static class CreatureReadiness
{
    /// <summary>
    /// The least free length between a beam's two joint discs (#593): room for a 22-unit sensor
    /// drawing with a 4-unit gap on each side.
    /// </summary>
    public const double MinimumBeamGap = 30;

    /// <summary>True when the beam between <paramref name="a"/> and <paramref name="b"/> leaves less than <see cref="MinimumBeamGap"/> between their discs.</summary>
    public static bool IsTooShort(NodeDef a, NodeDef b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var dx = b.Position.X - a.Position.X;
        var dy = b.Position.Y - a.Position.Y;
        return Math.Sqrt((dx * dx) + (dy * dy)) - a.Radius - b.Radius < MinimumBeamGap;
    }

    /// <summary>Why the creature cannot be simulated yet; empty when it can.</summary>
    public static IReadOnlyList<string> Problems(CreatureDef creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        var problems = new List<string>();
        if (creature.Nodes.Count == 0)
        {
            problems.Add("Add at least one node before training this creation.");
        }

        for (var i = 0; i < creature.Nodes.Count; i++)
        {
            var nodeId = creature.Nodes[i].Id;
            if (!creature.Beams.Any(beam => beam.NodeA == nodeId || beam.NodeB == nodeId))
            {
                problems.Add($"Node {i + 1} has no beams attached. Connect it with a beam or remove it.");
            }
        }

        foreach (var beam in creature.Beams)
        {
            var indexA = creature.NodeIndexOf(beam.NodeA);
            var indexB = creature.NodeIndexOf(beam.NodeB);
            if (creature.Nodes[indexA].Position == creature.Nodes[indexB].Position)
            {
                problems.Add($"The beam between node {indexA + 1} and node {indexB + 1} has zero length. Move one of the nodes apart.");
            }
            else if (IsTooShort(creature.Nodes[indexA], creature.Nodes[indexB]))
            {
                problems.Add($"The beam between node {indexA + 1} and node {indexB + 1} is too short. Move one of the nodes apart.");
            }
        }

        return problems;
    }

    /// <summary>True when the creature can be simulated and has a motor relation for its brain to drive.</summary>
    public static bool CanTrain(CreatureDef creature) =>
        Problems(creature).Count == 0
        && MotorTopology.BuildNodeConnections(creature).Any(connection => connection.IsMotorized);
}
