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

    /// <summary>True when the non-zero beam or link between two nodes leaves less than <see cref="MinimumBeamGap"/> between their rings.</summary>
    public static bool IsTooShort(NodeDef a, NodeDef b, double radiusA, double radiusB)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return a.Position != b.Position && FreeLength(a.Position, b.Position, radiusA, radiusB) < MinimumBeamGap;
    }

    /// <summary>True when the free length between two nodes' effective rings is too small.</summary>
    public static bool IsTooShort(CreatureDef creature, int nodeA, int nodeB)
    {
        ArgumentNullException.ThrowIfNull(creature);
        var a = creature.Nodes[creature.NodeIndexOf(nodeA)];
        var b = creature.Nodes[creature.NodeIndexOf(nodeB)];
        return IsTooShort(a, b, creature.NodeRadius(nodeA), creature.NodeRadius(nodeB));
    }

    /// <summary>Why the creature cannot be simulated yet; empty when it can.</summary>
    public static IReadOnlyList<UiText> Problems(CreatureDef creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        var problems = new List<UiText>();
        if (creature.Nodes.Count == 0)
        {
            problems.Add(UiText.Plain("Add at least one joint before training this creation."));
        }

        for (var i = 0; i < creature.Nodes.Count; i++)
        {
            var nodeId = creature.Nodes[i].Id;
            if (!IsAttached(creature, nodeId))
            {
                problems.Add(UiText.Format("Joint {0} has nothing attached. Connect it with a link or remove it.", i + 1));
            }
        }

        var pieces = Pieces(creature.Nodes, LinkRef.All(creature.Beams, creature.Pistons, creature.Springs)).Count;
        if (pieces > 1)
        {
            problems.Add(UiText.Counted(
                "{0} piece of the creation is not connected to the rest. Connect it with a link or remove it.",
                "{0} pieces of the creation are not connected to each other. Connect them with links or remove all but one.",
                pieces));
        }

        foreach (var beam in creature.Beams)
        {
            AddLengthProblem(creature, CreatureElementKind.Beam, beam.NodeA, beam.NodeB, problems);
        }

        foreach (var servo in creature.Servos)
        {
            if (IsMissingALink(servo))
            {
                var name = PartNames.Display(creature.Nodes, creature.Beams, creature.Sensors, creature.Servos, creature.Pistons, creature.Springs, servo.Id);
                problems.Add(!ServoDef.HasTwoLinks(creature.LinksAt(servo.NodeId))
                    ? UiText.Format("{0} needs two links at its joint. Connect another link there or delete it.", name)
                    : UiText.Format("{0} is missing a link. Pick two links at its joint or delete it.", name));
            }
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

    /// <summary>A Servo without both its Fixed and Target link cannot train.</summary>
    public static bool IsMissingALink(ServoDef servo)
    {
        ArgumentNullException.ThrowIfNull(servo);
        return servo.FixedLinkId is null || servo.TargetLinkId is null;
    }

    /// <summary>Whether a beam or a link, a Piston (#451) or a Spring (#453), holds the node to the rest of the creature.</summary>
    public static bool IsAttached(CreatureDef creature, int nodeId)
    {
        ArgumentNullException.ThrowIfNull(creature);
        return creature.LinksAt(nodeId).Count > 0;
    }

    /// <summary>
    /// The pieces <paramref name="links"/> join <paramref name="nodes"/> into (#930), each its node
    /// ids in node order and listed by its first node. Loose joints are in none, as they have their
    /// own problem; Servos and sensors join nothing new.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<int>> Pieces(IReadOnlyList<NodeDef> nodes, IEnumerable<LinkRef> links)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(links);
        var neighbours = nodes.ToDictionary(node => node.Id, _ => new List<int>());
        foreach (var link in links)
        {
            neighbours[link.NodeA].Add(link.NodeB);
            neighbours[link.NodeB].Add(link.NodeA);
        }

        var pieceOf = new Dictionary<int, int>();
        var pieceCount = 0;
        foreach (var node in nodes)
        {
            if (neighbours[node.Id].Count == 0 || pieceOf.ContainsKey(node.Id))
            {
                continue;
            }

            pieceOf[node.Id] = pieceCount;
            var pending = new Stack<int>([node.Id]);
            while (pending.TryPop(out var id))
            {
                foreach (var next in neighbours[id])
                {
                    if (pieceOf.TryAdd(next, pieceCount))
                    {
                        pending.Push(next);
                    }
                }
            }

            pieceCount++;
        }

        var pieces = Enumerable.Range(0, pieceCount).Select(_ => new List<int>()).ToList();
        foreach (var node in nodes.Where(node => pieceOf.ContainsKey(node.Id)))
        {
            pieces[pieceOf[node.Id]].Add(node.Id);
        }

        return pieces;
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
                CreatureElementKind.Piston => UiText.Format("The piston between joint {0} and joint {1} has zero length. Move one of the joints apart.", a, b),
                CreatureElementKind.Spring => UiText.Format("The spring between joint {0} and joint {1} has zero length. Move one of the joints apart.", a, b),
                _ => UiText.Format("The beam between joint {0} and joint {1} has zero length. Move one of the joints apart.", a, b),
            });
        }
        else if (IsTooShort(creature, nodeA, nodeB))
        {
            problems.Add(kind switch
            {
                CreatureElementKind.Piston => UiText.Format("The piston between joint {0} and joint {1} is too short. Move one of the joints apart.", a, b),
                CreatureElementKind.Spring => UiText.Format("The spring between joint {0} and joint {1} is too short. Move one of the joints apart.", a, b),
                _ => UiText.Format("The beam between joint {0} and joint {1} is too short. Move one of the joints apart.", a, b),
            });
        }
    }

    /// <summary>
    /// True when the creature can be simulated. It needs no powered part (#845): without one its
    /// brain has nothing to drive and it stands still, and Train setup warns about that.
    /// </summary>
    public static bool CanTrain(CreatureDef creature) => Problems(creature).Count == 0;

    private static double FreeLength(Vector2D a, Vector2D b, double radiusA, double radiusB)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt((dx * dx) + (dy * dy)) - radiusA - radiusB;
    }
}
