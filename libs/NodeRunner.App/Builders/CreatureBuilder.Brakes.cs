using System.Diagnostics.CodeAnalysis;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Builders;

/// <summary>
/// The Brakes: passive joint parts that fill a joint's hinge-part slot and take their
/// sides by the Servo's rules. They have no network ports, so they are not joint motors.
/// </summary>
public sealed partial class CreatureBuilder
{
    /// <summary>Why a Brake dropped on anything but a joint is refused.</summary>
    public static UiText BrakesGoOnAJointReason { get; } = UiText.Plain("Brakes go on a joint");

    /// <summary>Why a Brake cannot work yet: it needs two sides at its joint.</summary>
    public static UiText BrakeNeedsTwoSidesReason { get; } = UiText.Plain("A Brake needs two links, or a link and a Wheel");

    private static readonly PartParameterId[] _brakeParameters = [PartParameterId.BrakeStrength];

    private readonly List<BrakeDef> _brakes = [];

    public IReadOnlyList<BrakeDef> Brakes => _brakes;

    /// <summary>What fills the joints' hinge-part slots, as <see cref="CreatureDef.HingeParts"/> lists them.</summary>
    public IReadOnlyList<IHingePartDef> HingeParts => [.. JointMotors, .. _brakes, .. _angleSensors];

    /// <summary>Whether a Brake can sit on this node; if not, <paramref name="reason"/> says why.</summary>
    public bool CanAddBrake(int nodeId, [NotNullWhen(false)] out UiText? reason)
    {
        if (!HasNode(nodeId))
        {
            reason = BrakesGoOnAJointReason;
            return false;
        }

        if (HasHingePart(nodeId))
        {
            reason = OneHingePartPerJointReason;
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>Adds a Brake with its sides picked as a new Servo's are (<see cref="JointMotor.DefaultSides"/>).</summary>
    public int AddBrake(int nodeId)
    {
        var (fixedSide, targetSide) = HasNode(nodeId) ? JointMotor.DefaultSides(LinksAt(nodeId), WheelAt(nodeId)) : (null, null);
        return AddBrake(nodeId, fixedSide, targetSide);
    }

    /// <summary>Adds a Brake with the given Fixed and Target sides, each at the joint or empty.</summary>
    public int AddBrake(int nodeId, int? fixedLinkId, int? targetLinkId)
    {
        if (!CanAddBrake(nodeId, out var reason))
        {
            throw new ArgumentException(reason.Message);
        }

        foreach (var linkId in new[] { fixedLinkId, targetLinkId }.OfType<int>())
        {
            ValidateServoSide(linkId, nodeId);
        }

        var id = AllocatePartId();
        _brakes.Add(new BrakeDef(id, nodeId, fixedLinkId, targetLinkId));
        return id;
    }

    public int BrakeIndexOf(int brakeId) => IndexOf(_brakes, brakeId, nameof(brakeId), "Brake id must point to an existing brake.");
}
