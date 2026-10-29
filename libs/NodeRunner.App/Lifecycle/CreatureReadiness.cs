using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Mechanics;

namespace NodeRunner.App.Lifecycle;

/// <summary>
/// Whether a drawn creature can be simulated and trained. Any drawing can be saved
/// (<see cref="CreatureDef"/>); these rules only stop training. The problems are worded for the
/// player, and Build's readiness chip shortens them.
/// </summary>
public static class CreatureReadiness
{
    /// <summary>
    /// The least free length a link leaves between what is drawn over its two ends, kept small by the
    /// owner's choice so links can be short; a large picture centred in it then reaches under the
    /// hub or motor at each end.
    /// </summary>
    public const double MinimumLinkGap = 26;

    /// <summary>
    /// What a Piston or Spring adds to <see cref="MinimumLength"/>, so its three-part
    /// telescope shows past a Servo's housing at each end. The owner chose it.
    /// </summary>
    public const double TelescopeExtra = 16;

    /// <summary>
    /// The shortest a link of <paramref name="kind"/> can be, centre to centre, between joints whose
    /// <see cref="CreatureDef.CoveredRadius(int, IEnumerable{IJointMotorDef})"/> are
    /// <paramref name="coveredA"/> and <paramref name="coveredB"/>: <see cref="MinimumLinkGap"/>
    /// plus what each joint covers, plus what the link type adds: nothing for a beam,
    /// <see cref="TelescopeExtra"/> for a Piston or Spring.
    /// </summary>
    public static double MinimumLength(CreatureElementKind kind, double coveredA, double coveredB) =>
        coveredA + coveredB + MinimumLinkGap + PartKinds.OfLink(kind, () => 0.0, () => TelescopeExtra, () => TelescopeExtra);

    /// <summary>True when the non-zero link of <paramref name="kind"/> between two nodes is shorter than its <see cref="MinimumLength"/>, given the creature's joint motors.</summary>
    public static bool IsTooShort(CreatureElementKind kind, NodeDef a, NodeDef b, IEnumerable<IJointMotorDef> jointMotors)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        ArgumentNullException.ThrowIfNull(jointMotors);
        return a.Position != b.Position
            && Length(a.Position, b.Position) < MinimumLength(kind, CreatureDef.CoveredRadius(a.Id, jointMotors), CreatureDef.CoveredRadius(b.Id, jointMotors));
    }

    /// <summary>True when the link of <paramref name="kind"/> between these two of the creature's nodes is shorter than its <see cref="MinimumLength"/>.</summary>
    public static bool IsTooShort(CreatureDef creature, CreatureElementKind kind, int nodeA, int nodeB)
    {
        ArgumentNullException.ThrowIfNull(creature);
        return IsTooShort(kind, creature.Nodes[creature.NodeIndexOf(nodeA)], creature.Nodes[creature.NodeIndexOf(nodeB)], creature.JointMotors);
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

        var pieces = Pieces.Of(creature.Nodes, LinkRef.All(creature.Beams, creature.Pistons, creature.Springs)).Count;
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

        foreach (var part in creature.HingeParts)
        {
            if (IsMissingALink(part))
            {
                var name = PartNames.Display(creature.Parts, part.Id);
                problems.Add(!JointMotor.HasTwoSides(creature.LinksAt(part.NodeId), creature.WheelAt(part.NodeId) is not null)
                    ? UiText.Format("{0} needs two links, or a link and a Wheel, at its joint. Connect another link or add a Wheel there, or delete it.", name)
                    : UiText.Format("{0} is missing a side. Pick its Fixed and Target parts or delete it.", name));
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

    /// <summary>A motor, Brake or Angle sensor without both its Fixed and Target side, a link or its Wheel, cannot train.</summary>
    public static bool IsMissingALink(IHingePartDef part)
    {
        ArgumentNullException.ThrowIfNull(part);
        return part.FixedLinkId is null || part.TargetLinkId is null;
    }

    /// <summary>Whether a beam or a link, a Piston or a Spring, holds the node to the rest of the creature.</summary>
    public static bool IsAttached(CreatureDef creature, int nodeId)
    {
        ArgumentNullException.ThrowIfNull(creature);
        return creature.LinksAt(nodeId).Count > 0;
    }

    private static void AddLengthProblem(CreatureDef creature, CreatureElementKind kind, int nodeA, int nodeB, List<UiText> problems)
    {
        var indexA = creature.NodeIndexOf(nodeA);
        var indexB = creature.NodeIndexOf(nodeB);
        var a = indexA + 1;
        var b = indexB + 1;
        if (creature.Nodes[indexA].Position == creature.Nodes[indexB].Position)
        {
            problems.Add(PartKinds.OfLink(
                kind,
                () => UiText.Format("The beam between joint {0} and joint {1} has zero length. Move one of the joints apart.", a, b),
                () => UiText.Format("The piston between joint {0} and joint {1} has zero length. Move one of the joints apart.", a, b),
                () => UiText.Format("The spring between joint {0} and joint {1} has zero length. Move one of the joints apart.", a, b)));
        }
        else if (IsTooShort(creature, kind, nodeA, nodeB))
        {
            problems.Add(PartKinds.OfLink(
                kind,
                () => UiText.Format("The beam between joint {0} and joint {1} is too short. Move one of the joints apart.", a, b),
                () => UiText.Format("The piston between joint {0} and joint {1} is too short. Move one of the joints apart.", a, b),
                () => UiText.Format("The spring between joint {0} and joint {1} is too short. Move one of the joints apart.", a, b)));
        }
    }

    /// <summary>
    /// True when the creature can be simulated. It needs no powered part: without one its
    /// network has nothing to drive and it stands still, and Train setup warns about that.
    /// </summary>
    public static bool CanTrain(CreatureDef creature) => Problems(creature).Count == 0;

    private static double Length(Vector2D a, Vector2D b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
