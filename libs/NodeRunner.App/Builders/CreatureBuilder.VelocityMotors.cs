using System.Diagnostics.CodeAnalysis;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Builders;

/// <summary>
/// The Velocity motors (#454): joint motors that share a joint's hinge-part slot with a Servo and a Brake
/// and take their sides by the Servo's rules (#1118).
/// </summary>
public sealed partial class CreatureBuilder
{
    /// <summary>Why a Velocity motor dropped on anything but a joint is refused (#454).</summary>
    public static UiText VelocityMotorsGoOnAJointReason { get; } = UiText.Plain("Motors go on a joint");

    /// <summary>Why a Velocity motor cannot work yet: it needs two sides at its joint (#1118).</summary>
    public static UiText VelocityMotorNeedsTwoSidesReason { get; } = UiText.Plain("A Motor needs two links, or a link and a Wheel");

    private static readonly PartParameterId[] _velocityMotorParameters = [PartParameterId.MotorStrength, PartParameterId.MotorMaxSpeed, PartParameterId.RiseTime];

    private readonly List<VelocityMotorDef> _velocityMotors = [];

    public IReadOnlyList<VelocityMotorDef> VelocityMotors => _velocityMotors;

    /// <summary>Every joint motor, the Servos, the Velocity motors, then the Steppers, as <see cref="CreatureDef.JointMotors"/> lists them.</summary>
    public IReadOnlyList<IJointMotorDef> JointMotors => [.. _servos, .. _velocityMotors, .. _steppers];

    /// <summary>Whether a Velocity motor can sit on this node; if not, <paramref name="reason"/> says why.</summary>
    public bool CanAddVelocityMotor(int nodeId, [NotNullWhen(false)] out UiText? reason)
    {
        if (!HasNode(nodeId))
        {
            reason = VelocityMotorsGoOnAJointReason;
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

    /// <summary>Adds a Velocity motor with its sides picked as a new Servo's are (<see cref="JointMotor.DefaultSides"/>).</summary>
    public int AddVelocityMotor(int nodeId)
    {
        var (fixedSide, targetSide) = HasNode(nodeId) ? JointMotor.DefaultSides(LinksAt(nodeId), WheelAt(nodeId)) : (null, null);
        return AddVelocityMotor(nodeId, fixedSide, targetSide);
    }

    /// <summary>Adds a Velocity motor with the given Fixed and Target sides, each at the joint or empty.</summary>
    public int AddVelocityMotor(int nodeId, int? fixedLinkId, int? targetLinkId)
    {
        if (!CanAddVelocityMotor(nodeId, out var reason))
        {
            throw new ArgumentException(reason.Message);
        }

        foreach (var linkId in new[] { fixedLinkId, targetLinkId }.OfType<int>())
        {
            ValidateServoSide(linkId, nodeId);
        }

        var id = AllocatePartId();
        _velocityMotors.Add(new VelocityMotorDef(id, nodeId, fixedLinkId, targetLinkId));
        return id;
    }

    /// <summary>
    /// Changes the Velocity motor's fixed or target side, as <see cref="SetServoLink"/> does a
    /// Servo's: picking the other role swaps them, and it keeps its id.
    /// </summary>
    public void SetVelocityMotorLink(int velocityMotorId, bool fixedRole, int linkId) =>
        SetSide(_velocityMotors, VelocityMotorIndexOf(velocityMotorId), fixedRole, linkId, (motor, fixedLink, targetLink) => motor.WithLinks(fixedLink, targetLink));

    /// <summary>Removes a Velocity motor by id.</summary>
    public void RemoveVelocityMotor(int velocityMotorId) => _velocityMotors.RemoveAt(VelocityMotorIndexOf(velocityMotorId));

    public int VelocityMotorIndexOf(int velocityMotorId)
    {
        var index = _velocityMotors.FindIndex(motor => motor.Id == velocityMotorId);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(velocityMotorId), "Velocity motor id must point to an existing velocity motor.");
        }

        return index;
    }

    // Whether the joint's hinge-part slot is taken (#1044).
    private bool HasHingePart(int nodeId) => HingeParts.Any(part => part.NodeId == nodeId);

    private double VelocityMotorParameterValue(int velocityMotorId, PartParameterId parameter)
    {
        var motor = _velocityMotors[VelocityMotorIndexOf(velocityMotorId)];
        return parameter switch
        {
            PartParameterId.MotorStrength => motor.Strength,
            PartParameterId.MotorMaxSpeed => motor.MaxSpeed,
            PartParameterId.RiseTime => motor.RiseTime,
            _ => throw new ArgumentOutOfRangeException(nameof(parameter)),
        };
    }

    private void SetVelocityMotorParameter(int velocityMotorId, PartParameterId parameter, double value)
    {
        var index = VelocityMotorIndexOf(velocityMotorId);
        var motor = _velocityMotors[index];
        _velocityMotors[index] = parameter switch
        {
            PartParameterId.MotorStrength => motor.WithSettings(value, motor.MaxSpeed, motor.RiseTime),
            PartParameterId.MotorMaxSpeed => motor.WithSettings(motor.Strength, value, motor.RiseTime),
            PartParameterId.RiseTime => motor.WithSettings(motor.Strength, motor.MaxSpeed, value),
            _ => throw new ArgumentOutOfRangeException(nameof(parameter)),
        };
    }
}
