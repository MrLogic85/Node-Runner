using NodeRunner.Domain;

namespace NodeRunner.App.Lifecycle;

/// <summary>
/// Whether a drawn creature can be simulated and trained. Any drawing can be saved
/// (<see cref="CreatureDef"/>); these rules only stop training. The problems are worded for the
/// player, and Build's readiness line shortens them.
/// </summary>
public static class CreatureReadiness
{
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
            if (!creature.Beams.Any(beam => beam.NodeA == i || beam.NodeB == i))
            {
                problems.Add($"Node {i} has no beams attached. Connect it with a beam or remove it.");
            }
        }

        foreach (var beam in creature.Beams)
        {
            if (creature.Nodes[beam.NodeA].Position == creature.Nodes[beam.NodeB].Position)
            {
                problems.Add($"The beam between node {beam.NodeA} and node {beam.NodeB} has zero length. Move one of the nodes apart.");
            }
        }

        return problems;
    }

    /// <summary>True when the creature can be simulated and has a motor relation for its brain to drive.</summary>
    public static bool CanTrain(CreatureDef creature) =>
        Problems(creature).Count == 0
        && MotorTopology.BuildNodeConnections(creature).Any(connection => connection.IsMotorized);
}
