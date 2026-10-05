using NodeRunner.App.ViewModels;
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
    /// The least free length between a beam's two joint rings (#593, #622): room for the largest
    /// sensor picture with a 4-unit gap on each side.
    /// </summary>
    public const double MinimumBeamGap = SensorPicture.LargestSize + (2 * _sensorGap);

    private const double _sensorGap = 4;

    /// <summary>True when the beam or link between <paramref name="a"/> and <paramref name="b"/> leaves less than <see cref="MinimumBeamGap"/> between their rings.</summary>
    public static bool IsTooShort(NodeDef a, NodeDef b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var dx = b.Position.X - a.Position.X;
        var dy = b.Position.Y - a.Position.Y;
        return Math.Sqrt((dx * dx) + (dy * dy)) - a.Radius - b.Radius < MinimumBeamGap;
    }

    /// <summary>Why the creature cannot be simulated yet; empty when it can.</summary>
    public static IReadOnlyList<UiText> Problems(CreatureDef creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        var problems = new List<UiText>();
        if (creature.Nodes.Count == 0)
        {
            problems.Add(UiText.Plain("Add at least one node before training this creation."));
        }

        for (var i = 0; i < creature.Nodes.Count; i++)
        {
            var nodeId = creature.Nodes[i].Id;
            if (!IsAttached(creature, nodeId))
            {
                problems.Add(UiText.Format("Node {0} has nothing attached. Connect it with a beam, a piston or a spring, or remove it.", i + 1));
            }
        }

        foreach (var beam in creature.Beams)
        {
            AddLengthProblem(creature, CreatureElementKind.Beam, beam.NodeA, beam.NodeB, problems);
        }

        foreach (var piston in creature.Pistons)
        {
            AddLengthProblem(creature, CreatureElementKind.Piston, piston.NodeA, piston.NodeB, problems);
        }

        foreach (var spring in creature.Springs)
        {
            AddLengthProblem(creature, CreatureElementKind.Spring, spring.NodeA, spring.NodeB, problems);
        }

        return problems;
    }

    /// <summary>Whether a beam or a link, a Piston (#451) or a Spring (#453), holds the node to the rest of the creature.</summary>
    public static bool IsAttached(CreatureDef creature, int nodeId)
    {
        ArgumentNullException.ThrowIfNull(creature);
        return creature.Beams.Any(beam => beam.NodeA == nodeId || beam.NodeB == nodeId)
            || creature.Pistons.Any(piston => piston.NodeA == nodeId || piston.NodeB == nodeId)
            || creature.Springs.Any(spring => spring.NodeA == nodeId || spring.NodeB == nodeId);
    }

    private static void AddLengthProblem(CreatureDef creature, CreatureElementKind kind, int nodeA, int nodeB, List<UiText> problems)
    {
        var indexA = creature.NodeIndexOf(nodeA);
        var indexB = creature.NodeIndexOf(nodeB);
        var a = indexA + 1;
        var b = indexB + 1;
        if (creature.Nodes[indexA].Position == creature.Nodes[indexB].Position)
        {
            problems.Add(kind switch
            {
                CreatureElementKind.Piston => UiText.Format("The piston between node {0} and node {1} has zero length. Move one of the nodes apart.", a, b),
                CreatureElementKind.Spring => UiText.Format("The spring between node {0} and node {1} has zero length. Move one of the nodes apart.", a, b),
                _ => UiText.Format("The beam between node {0} and node {1} has zero length. Move one of the nodes apart.", a, b),
            });
        }
        else if (IsTooShort(creature.Nodes[indexA], creature.Nodes[indexB]))
        {
            problems.Add(kind switch
            {
                CreatureElementKind.Piston => UiText.Format("The piston between node {0} and node {1} is too short. Move one of the nodes apart.", a, b),
                CreatureElementKind.Spring => UiText.Format("The spring between node {0} and node {1} is too short. Move one of the nodes apart.", a, b),
                _ => UiText.Format("The beam between node {0} and node {1} is too short. Move one of the nodes apart.", a, b),
            });
        }
    }

    /// <summary>
    /// True when the creature can be simulated. It needs no powered part (#845): without one its
    /// brain has nothing to drive and it stands still, and Train setup warns about that.
    /// </summary>
    public static bool CanTrain(CreatureDef creature) => Problems(creature).Count == 0;
}
