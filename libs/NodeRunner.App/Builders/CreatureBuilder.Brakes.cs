using NodeRunner.Domain;

namespace NodeRunner.App.Builders;

/// <summary>
/// The Brakes (#1039): passive joint parts that fill a joint's motor-or-brake slot and take their
/// sides by the Servo's rules (#1118). They have no brain ports, so they are not joint motors.
/// </summary>
public sealed partial class CreatureBuilder
{
    private readonly List<BrakeDef> _brakes = [];

    public IReadOnlyList<BrakeDef> Brakes => _brakes;

    // Build's slider range for a Brake's finite Strength, 20–400 N·m in world torque units; #1041 moves it
    // to a PartParameters slider whose last step is Locked.
    private const double _minBuildStrength = 200000;
    private const double _maxBuildStrength = 4000000;

    /// <summary>Adds a Brake with its sides picked as a new Servo's are (<see cref="JointMotor.DefaultSides"/>).</summary>
    public int AddBrake(int nodeId)
    {
        var (fixedSide, targetSide) = HasNode(nodeId) ? JointMotor.DefaultSides(LinksAt(nodeId), WheelAt(nodeId)) : (null, null);
        return AddBrake(nodeId, fixedSide, targetSide);
    }

    /// <summary>Adds a Brake with the given Fixed and Target sides, each at the joint or empty.</summary>
    public int AddBrake(int nodeId, int? fixedLinkId, int? targetLinkId)
    {
        NodeIndexOf(nodeId);
        if (HasMotorOrBrake(nodeId))
        {
            throw new ArgumentException(OneMotorOrBrakePerJointReason.Message);
        }

        foreach (var linkId in new[] { fixedLinkId, targetLinkId }.OfType<int>())
        {
            ValidateServoSide(linkId, nodeId);
        }

        var id = AllocatePartId();
        _brakes.Add(new BrakeDef(id, nodeId, fixedLinkId, targetLinkId));
        return id;
    }

    /// <summary>Sets a Brake's Strength, in world torque units, or <see cref="BrakeDef.LockedStrength"/>.</summary>
    public void SetBrakeStrength(int brakeId, double strength)
    {
        var index = BrakeIndexOf(brakeId);
        _brakes[index] = _brakes[index].WithStrength(strength);
    }

    /// <summary>Removes a Brake by id.</summary>
    public void RemoveBrake(int brakeId) => _brakes.RemoveAt(BrakeIndexOf(brakeId));

    public int BrakeIndexOf(int brakeId)
    {
        var index = _brakes.FindIndex(brake => brake.Id == brakeId);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(brakeId), "Brake id must point to an existing brake.");
        }

        return index;
    }

    private static bool IsWithinBuildLimits(BrakeDef brake) =>
        brake.Locked || (brake.Strength >= _minBuildStrength && brake.Strength <= _maxBuildStrength);

    private bool RenameBrake(int partId, string? name)
    {
        var index = _brakes.FindIndex(brake => brake.Id == partId);
        if (index < 0)
        {
            return false;
        }

        _brakes[index] = _brakes[index].WithName(name);
        return true;
    }
}
