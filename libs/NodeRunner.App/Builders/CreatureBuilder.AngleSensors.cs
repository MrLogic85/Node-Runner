using System.Diagnostics.CodeAnalysis;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Builders;

/// <summary>
/// The Angle sensors: joint sensors that fill a joint's hinge-part slot and take their
/// sides by the Servo's rules. They have inputs only and apply no force, so they are not
/// joint motors. Build places them as it does a Brake.
/// </summary>
public sealed partial class CreatureBuilder
{
    /// <summary>Why an Angle sensor dropped on anything but a joint is refused.</summary>
    public static UiText AngleSensorsGoOnAJointReason { get; } = UiText.Plain("Angle sensors go on a joint");

    /// <summary>Why an Angle sensor cannot read yet: it needs two sides at its joint.</summary>
    public static UiText AngleSensorNeedsTwoSidesReason { get; } = UiText.Plain("An Angle sensor needs two links, or a link and a Wheel");

    private readonly List<AngleSensorDef> _angleSensors = [];

    public IReadOnlyList<AngleSensorDef> AngleSensors => _angleSensors;

    /// <summary>Whether an Angle sensor can sit on this node; if not, <paramref name="reason"/> says why.</summary>
    public bool CanAddAngleSensor(int nodeId, [NotNullWhen(false)] out UiText? reason)
    {
        if (!HasNode(nodeId))
        {
            reason = AngleSensorsGoOnAJointReason;
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

    /// <summary>Adds an Angle sensor with its sides picked as a new Servo's are (<see cref="JointMotor.DefaultSides"/>).</summary>
    public int AddAngleSensor(int nodeId)
    {
        var (fixedSide, targetSide) = HasNode(nodeId) ? JointMotor.DefaultSides(LinksAt(nodeId), WheelAt(nodeId)) : (null, null);
        return AddAngleSensor(nodeId, fixedSide, targetSide);
    }

    /// <summary>Adds an Angle sensor with the given Fixed and Target sides, each at the joint or empty.</summary>
    public int AddAngleSensor(int nodeId, int? fixedLinkId, int? targetLinkId)
    {
        if (!CanAddAngleSensor(nodeId, out var reason))
        {
            throw new ArgumentException(reason.Message);
        }

        foreach (var linkId in new[] { fixedLinkId, targetLinkId }.OfType<int>())
        {
            ValidateServoSide(linkId, nodeId);
        }

        var id = AllocatePartId();
        _angleSensors.Add(new AngleSensorDef(id, nodeId, fixedLinkId, targetLinkId));
        return id;
    }

    /// <summary>
    /// Changes the Angle sensor's fixed or target side, as <see cref="SetServoLink"/> does a Servo's:
    /// picking the other role swaps them, and it keeps its id.
    /// </summary>
    public void SetAngleSensorLink(int angleSensorId, bool fixedRole, int linkId) =>
        SetSide(_angleSensors[AngleSensorIndexOf(angleSensorId)], fixedRole, linkId);

    public void RemoveAngleSensor(int angleSensorId) => RemovePart(_angleSensors[AngleSensorIndexOf(angleSensorId)]);

    public int AngleSensorIndexOf(int angleSensorId) => IndexOf(_angleSensors, angleSensorId, nameof(angleSensorId), "Angle sensor id must point to an existing angle sensor.");
}
