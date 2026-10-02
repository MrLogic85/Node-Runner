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
    /// The least free length between a beam's two joint discs (#593, #622): room for the largest
    /// sensor picture with a 4-unit gap on each side.
    /// </summary>
    public const double MinimumBeamGap = SensorPicture.LargestSize + (2 * _sensorGap);

    private const double _sensorGap = 4;

    /// <summary>True when the beam or Piston between <paramref name="a"/> and <paramref name="b"/> leaves less than <see cref="MinimumBeamGap"/> between their discs.</summary>
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
            if (!IsAttached(creature, nodeId))
            {
                problems.Add($"Node {i + 1} has nothing attached. Connect it with a beam or a piston, or remove it.");
            }
        }

        foreach (var beam in creature.Beams)
        {
            AddLengthProblem(creature, "beam", beam.NodeA, beam.NodeB, problems);
        }

        foreach (var piston in creature.Pistons)
        {
            AddLengthProblem(creature, "piston", piston.NodeA, piston.NodeB, problems);
        }

        return problems;
    }

    /// <summary>Whether a beam or a Piston (#451) holds the node to the rest of the creature.</summary>
    public static bool IsAttached(CreatureDef creature, int nodeId)
    {
        ArgumentNullException.ThrowIfNull(creature);
        return creature.Beams.Any(beam => beam.NodeA == nodeId || beam.NodeB == nodeId)
            || creature.Pistons.Any(piston => piston.NodeA == nodeId || piston.NodeB == nodeId);
    }

    private static void AddLengthProblem(CreatureDef creature, string kind, int nodeA, int nodeB, List<string> problems)
    {
        var indexA = creature.NodeIndexOf(nodeA);
        var indexB = creature.NodeIndexOf(nodeB);
        if (creature.Nodes[indexA].Position == creature.Nodes[indexB].Position)
        {
            problems.Add($"The {kind} between node {indexA + 1} and node {indexB + 1} has zero length. Move one of the nodes apart.");
        }
        else if (IsTooShort(creature.Nodes[indexA], creature.Nodes[indexB]))
        {
            problems.Add($"The {kind} between node {indexA + 1} and node {indexB + 1} is too short. Move one of the nodes apart.");
        }
    }

    /// <summary>True when the creature can be simulated and has something for its brain to drive: a Piston (joints are passive, #450).</summary>
    public static bool CanTrain(CreatureDef creature) =>
        Problems(creature).Count == 0
        && BrainPorts.Of(creature).Outputs.Count > 0;
}
