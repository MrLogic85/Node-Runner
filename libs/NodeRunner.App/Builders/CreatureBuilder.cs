using System.Diagnostics.CodeAnalysis;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Mechanics;

namespace NodeRunner.App.Builders;

/// <summary>
/// Mutable, in-progress creature anatomy driven by Build-mode UI.
/// Add/move/remove nodes, beams, sensors, joint parts, Pistons and Springs here; <see cref="Build"/>
/// returns the drawing as an immutable <see cref="CreatureDef"/> for saving, and
/// <see cref="TryBuild"/> returns it only once it can be simulated. See
/// docs/CREATURE_MODEL.md for the vocabulary and docs/BUILD_MODE.md for
/// the feature this supports.
///
/// Lives in `NodeRunner.App`, not `NodeRunner.Domain`: this is mutable
/// business logic (add/remove/cascade), and Domain keeps only records and
/// a short list of stateless helpers (`.claude/rules/domain.md`).
/// </summary>
public sealed partial class CreatureBuilder
{
    /// <summary>Why a sensor cannot go on a beam that already has one.</summary>
    public static UiText OneSensorPerBeamReason { get; } = UiText.Plain("One sensor per beam");

    /// <summary>Why a hinge part, a Servo, a Stepper, a Velocity motor, a Brake or an Angle sensor, cannot be added to a joint that already has one.</summary>
    public static UiText OneHingePartPerJointReason { get; } = UiText.Plain("One Servo, Stepper, Motor, Brake or Angle sensor per joint");

    /// <summary>Why a Servo dropped on anything but a joint is refused.</summary>
    public static UiText ServosGoOnAJointReason { get; } = UiText.Plain("Servos go on a joint");

    /// <summary>Why a Wheel cannot be added to a node that already has one.</summary>
    public static UiText OneWheelPerJointReason { get; } = UiText.Plain("One wheel per joint");

    /// <summary>Why a Wheel dropped on anything but a joint is refused.</summary>
    public static UiText WheelsGoOnAJointReason { get; } = UiText.Plain("Wheels go on a joint");

    /// <summary>Why a Touch sensor cannot be added to a node that already has one.</summary>
    public static UiText OneSensorPerJointReason { get; } = UiText.Plain("One Touch sensor per joint");

    /// <summary>Why a Touch sensor dropped on anything but a joint is refused.</summary>
    public static UiText TouchSensorsGoOnAJointReason { get; } = UiText.Plain("Touch sensors go on a joint");

    /// <summary>Why a Rubber cannot be added to a node that already has one.</summary>
    public static UiText OneRubberPerJointReason { get; } = UiText.Plain("One rubber per joint");

    /// <summary>Why a Rubber dropped on anything but a joint is refused.</summary>
    public static UiText RubberGoesOnAJointReason { get; } = UiText.Plain("Rubber goes on a joint");

    /// <summary>Why a Servo cannot work yet: its joint has fewer than two sides, links or a Wheel.</summary>
    public static UiText ServoNeedsTwoSidesReason { get; } = UiText.Plain("A Servo needs two links, or a link and a Wheel");

    /// <summary>Why another beam cannot join two nodes a beam already holds rigid; a Piston or Spring replaces it instead.</summary>
    public static UiText BeamJoinsTheseNodesReason { get; } = UiText.Plain("A beam already joins these joints");

    /// <summary>Why another Piston cannot join two nodes a Piston already links; a Beam or Spring replaces it instead.</summary>
    public static UiText PistonJoinsTheseNodesReason { get; } = UiText.Plain("These joints already have a piston");

    /// <summary>Why another Spring cannot join two nodes a Spring already links; a Beam or Piston replaces it instead.</summary>
    public static UiText SpringJoinsTheseNodesReason { get; } = UiText.Plain("These joints already have a spring");

    /// <summary>Why a Piston or Spring cannot replace the beam between two nodes: the sensor on it is never deleted silently.</summary>
    public static UiText SensorSitsOnThisBeamReason { get; } = UiText.Plain("A sensor sits on this beam");

    private readonly List<NodeDef> _nodes = [];
    private readonly List<BeamDef> _beams = [];
    private readonly List<SensorDef> _sensors = [];
    private readonly List<ServoDef> _servos = [];
    private readonly List<PistonDef> _pistons = [];
    private readonly List<SpringDef> _springs = [];
    private readonly List<WheelDef> _wheels = [];
    private readonly List<TouchSensorDef> _touchSensors = [];
    private readonly List<RubberDef> _rubbers = [];
    private int _nextPartId = 1;

    public CreatureBuilder()
    {
    }

    public CreatureBuilder(CreatureDef creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        foreach (var part in creature.Parts)
        {
            ListOf(part.PartKind).Add(part);
        }

        _nextPartId = creature.NextPartId;
    }

    public IReadOnlyList<NodeDef> Nodes => _nodes;

    public IReadOnlyList<BeamDef> Beams => _beams;

    public IReadOnlyList<SensorDef> Sensors => _sensors;

    public IReadOnlyList<ServoDef> Servos => _servos;

    public IReadOnlyList<PistonDef> Pistons => _pistons;

    public IReadOnlyList<SpringDef> Springs => _springs;

    public IReadOnlyList<WheelDef> Wheels => _wheels;

    public IReadOnlyList<TouchSensorDef> TouchSensors => _touchSensors;

    public IReadOnlyList<RubberDef> Rubbers => _rubbers;

    /// <summary>Every part on a joint's slots: the <see cref="HingeParts"/>, Wheels, Touch sensors and Rubbers.</summary>
    public IReadOnlyList<IJointPartDef> JointParts => [.. HingeParts, .. _wheels, .. _touchSensors, .. _rubbers];

    /// <summary>Every part, each kind in its list order: what <see cref="Build"/> builds the creature from.</summary>
    public IReadOnlyList<IPartDef> Parts => [.. _nodes, .. _beams, .. _sensors, .. _pistons, .. _springs, .. JointParts];

    public int NextPartId => _nextPartId;

    /// <summary>Adds a node at <paramref name="position"/> on <see cref="OnWholeUnits"/> and returns its id.</summary>
    public int AddNode(Vector2D position)
    {
        var id = AllocatePartId();
        _nodes.Add(new NodeDef(id, OnWholeUnits(position)));
        return id;
    }

    /// <summary>Moves an existing node to <paramref name="position"/> on <see cref="OnWholeUnits"/>, keeping its name.</summary>
    public void MoveNode(int nodeId, Vector2D position)
    {
        var nodeIndex = NodeIndexOf(nodeId);
        var node = _nodes[nodeIndex];
        _nodes[nodeIndex] = new NodeDef(node.Id, OnWholeUnits(position), node.Name);
    }

    /// <summary>
    /// <paramref name="position"/> rounded to whole world units, 1 cm. Every joint Build places
    /// or moves sits on one, so its save and share code hold a short number rather than 17 digits.
    /// A half rounds away from 0, so a build mirrored about 0 stays mirrored.
    /// </summary>
    public static Vector2D OnWholeUnits(Vector2D position) => new(OnWholeUnit(position.X), OnWholeUnit(position.Y));

    // Adding 0 turns −0 into 0, which reads the same and is a character shorter.
    private static double OnWholeUnit(double coordinate) => Math.Round(coordinate, MidpointRounding.AwayFromZero) + 0.0;

    /// <summary>
    /// Removes a node, cascading to every beam, sensor, joint part, Piston and Spring that referenced it.
    /// </summary>
    public void RemoveNode(int nodeId)
    {
        var nodeIndex = NodeIndexOf(nodeId);
        foreach (var part in JointParts.Where(part => part.NodeId == nodeId))
        {
            ListOf(part.PartKind).Remove(part.Id);
        }

        foreach (var link in LinksAt(nodeId))
        {
            RemovePart(ExistingPart(link.Id));
        }

        _nodes.RemoveAt(nodeIndex);
    }

    /// <summary>
    /// Adds a beam between two distinct, existing nodes, in place of a Piston or Spring there, and
    /// returns its id; see <see cref="CanAddBeam"/>. Throws if either node id is invalid or the pair is refused.
    /// </summary>
    public int AddBeam(int nodeIdA, int nodeIdB)
    {
        ValidateNodeId(nodeIdA);
        ValidateNodeId(nodeIdB);
        if (!CanAddBeam(nodeIdA, nodeIdB, out var reason))
        {
            throw new ArgumentException(reason.Message);
        }

        var replaced = LinkBetween(nodeIdA, nodeIdB);
        var id = AllocatePartId();
        _beams.Add(new BeamDef(id, nodeIdA, nodeIdB));
        ReplaceLink(replaced, id);
        return id;
    }

    /// <summary>
    /// Whether <see cref="AddBeam"/> would accept this pair: two distinct, existing nodes
    /// with no beam yet; a Piston or Spring there is replaced. If not, <paramref name="reason"/> says why.
    /// </summary>
    public bool CanAddBeam(int nodeIdA, int nodeIdB, [NotNullWhen(false)] out UiText? reason)
    {
        if (!HasNode(nodeIdA) || !HasNode(nodeIdB) || nodeIdA == nodeIdB)
        {
            reason = UiText.Plain("A beam must connect two different joints.");
            return false;
        }

        reason = BeamBetween(nodeIdA, nodeIdB) is not null ? BeamJoinsTheseNodesReason : null;
        return reason is null;
    }

    /// <summary>
    /// Whether <see cref="AddPiston"/> would accept this pair: two distinct, existing nodes
    /// with no Piston yet. A beam or Spring there is replaced, unless a sensor sits on the
    /// beam; if not, <paramref name="reason"/> says why.
    /// </summary>
    public bool CanAddPiston(int nodeIdA, int nodeIdB, [NotNullWhen(false)] out UiText? reason)
    {
        if (!HasNode(nodeIdA) || !HasNode(nodeIdB) || nodeIdA == nodeIdB)
        {
            reason = UiText.Plain("A piston must connect two different joints.");
            return false;
        }

        reason = PistonBetween(nodeIdA, nodeIdB) is not null ? PistonJoinsTheseNodesReason : ReplacedLinkReason(nodeIdA, nodeIdB);
        return reason is null;
    }

    /// <summary>
    /// Whether <see cref="AddSpring"/> would accept this pair: two distinct, existing nodes with
    /// no Spring yet. A beam or Piston there is replaced, unless a sensor sits on the beam;
    /// if not, <paramref name="reason"/> says why.
    /// </summary>
    public bool CanAddSpring(int nodeIdA, int nodeIdB, [NotNullWhen(false)] out UiText? reason)
    {
        if (!HasNode(nodeIdA) || !HasNode(nodeIdB) || nodeIdA == nodeIdB)
        {
            reason = UiText.Plain("A spring must connect two different joints.");
            return false;
        }

        reason = SpringBetween(nodeIdA, nodeIdB) is not null ? SpringJoinsTheseNodesReason : ReplacedLinkReason(nodeIdA, nodeIdB);
        return reason is null;
    }

    // A link of another kind on the pair is replaced, but never a sensor's beam.
    private UiText? ReplacedLinkReason(int nodeIdA, int nodeIdB) =>
        BeamBetween(nodeIdA, nodeIdB) is { } beamId && _sensors.Any(sensor => sensor.BeamId == beamId) ? SensorSitsOnThisBeamReason : null;

    /// <summary>
    /// The Beam, Piston or Spring joining two nodes, or null. A pair has at most one, and a link of
    /// another kind placed there replaces it.
    /// </summary>
    public int? LinkBetween(int nodeIdA, int nodeIdB) =>
        BeamBetween(nodeIdA, nodeIdB) ?? PistonBetween(nodeIdA, nodeIdB) ?? SpringBetween(nodeIdA, nodeIdB);

    private int? BeamBetween(int nodeIdA, int nodeIdB) =>
        _beams.FirstOrDefault(beam => IsSamePair(beam, nodeIdA, nodeIdB))?.Id;

    private int? PistonBetween(int nodeIdA, int nodeIdB) =>
        _pistons.FirstOrDefault(piston => IsSamePair(piston.NodeA, piston.NodeB, nodeIdA, nodeIdB))?.Id;

    private int? SpringBetween(int nodeIdA, int nodeIdB) =>
        _springs.FirstOrDefault(spring => IsSamePair(spring.NodeA, spring.NodeB, nodeIdA, nodeIdB))?.Id;

    /// <summary>Adds a Piston between two nodes with the default settings, in place of a beam or Spring there, and returns its id; see <see cref="CanAddPiston"/>.</summary>
    public int AddPiston(int nodeIdA, int nodeIdB)
    {
        ValidateNodeId(nodeIdA);
        ValidateNodeId(nodeIdB);
        if (!CanAddPiston(nodeIdA, nodeIdB, out var reason))
        {
            throw new ArgumentException(reason.Message);
        }

        var replaced = LinkBetween(nodeIdA, nodeIdB);
        var id = AllocatePartId();
        _pistons.Add(new PistonDef(id, nodeIdA, nodeIdB));
        ReplaceLink(replaced, id);
        return id;
    }

    /// <summary>Adds a Spring between two nodes with the default settings, in place of a beam or Piston there, and returns its id; see <see cref="CanAddSpring"/>.</summary>
    public int AddSpring(int nodeIdA, int nodeIdB)
    {
        ValidateNodeId(nodeIdA);
        ValidateNodeId(nodeIdB);
        if (!CanAddSpring(nodeIdA, nodeIdB, out var reason))
        {
            throw new ArgumentException(reason.Message);
        }

        var replaced = LinkBetween(nodeIdA, nodeIdB);
        var id = AllocatePartId();
        _springs.Add(new SpringDef(id, nodeIdA, nodeIdB));
        ReplaceLink(replaced, id);
        return id;
    }

    // The new link takes the replaced one's place and its hinge-part roles. A replaced
    // beam never carries a sensor (ReplacedLinkReason).
    private void ReplaceLink(int? replacedId, int linkId)
    {
        if (replacedId is not { } id)
        {
            return;
        }

        ListOf(ExistingPart(id).PartKind).Remove(id);
        ReplaceSides(id, linkId);
    }

    // Each hinge part with linkId as a side gets newLinkId there instead, or no side when it is null; each
    // keeps its id (IHingePartDef.WithLinks).
    private void ReplaceSides(int linkId, int? newLinkId)
    {
        foreach (var part in HingeParts.Where(part => part.FixedLinkId == linkId || part.TargetLinkId == linkId))
        {
            Replace(part.WithLinks(
                part.FixedLinkId == linkId ? newLinkId : part.FixedLinkId,
                part.TargetLinkId == linkId ? newLinkId : part.TargetLinkId));
        }
    }

    /// <summary>
    /// Removes any part by id with what goes with it: a joint takes its links and everything on it, a beam
    /// takes its sensor, and a link or Wheel leaves each hinge part that used it as a side without that side.
    /// </summary>
    public void RemovePart(int partId) => RemovePart(ExistingPart(partId));

    private void RemovePart(IPartDef part)
    {
        if (part is NodeDef)
        {
            RemoveNode(part.Id);
            return;
        }

        ListOf(part.PartKind).Remove(part.Id);

        // Only a beam carries sensors and only a link or Wheel is a side, so for any other part these find none.
        _sensors.RemoveAll(sensor => sensor.BeamId == part.Id);
        ClearSides(part.Id);
    }

    // A deleted link or Wheel leaves each hinge part that used it as a side without that side.
    private void ClearSides(int linkId) => ReplaceSides(linkId, null);

    /// <summary>Whether a Servo can sit on this node; if not, <paramref name="reason"/> says why.</summary>
    public bool CanAddServo(int nodeId, [NotNullWhen(false)] out UiText? reason)
    {
        if (!HasNode(nodeId))
        {
            reason = ServosGoOnAJointReason;
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

    /// <summary>
    /// Whether a Wheel can sit on this node; if not, <paramref name="reason"/> says why. A
    /// joint holds one Wheel next to its hinge part.
    /// </summary>
    public bool CanAddWheel(int nodeId, [NotNullWhen(false)] out UiText? reason)
    {
        if (!HasNode(nodeId))
        {
            reason = WheelsGoOnAJointReason;
            return false;
        }

        if (_wheels.Any(wheel => wheel.NodeId == nodeId))
        {
            reason = OneWheelPerJointReason;
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>Adds a Wheel with the default settings and returns its id; see <see cref="CanAddWheel"/>.</summary>
    public int AddWheel(int nodeId)
    {
        if (!CanAddWheel(nodeId, out var reason))
        {
            throw new ArgumentException(reason.Message);
        }

        var id = AllocatePartId();
        _wheels.Add(new WheelDef(id, nodeId));
        return id;
    }

    /// <summary>
    /// Whether a Touch sensor can sit on this node; if not, <paramref name="reason"/> says
    /// why. A joint holds one, in its own joint-part slot next to a Servo and a Wheel.
    /// </summary>
    public bool CanAddTouchSensor(int nodeId, [NotNullWhen(false)] out UiText? reason)
    {
        if (!HasNode(nodeId))
        {
            reason = TouchSensorsGoOnAJointReason;
            return false;
        }

        if (_touchSensors.Any(touch => touch.NodeId == nodeId))
        {
            reason = OneSensorPerJointReason;
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>Adds a Touch sensor and returns its id; see <see cref="CanAddTouchSensor"/>.</summary>
    public int AddTouchSensor(int nodeId)
    {
        if (!CanAddTouchSensor(nodeId, out var reason))
        {
            throw new ArgumentException(reason.Message);
        }

        var id = AllocatePartId();
        _touchSensors.Add(new TouchSensorDef(id, nodeId));
        return id;
    }

    /// <summary>
    /// Whether a Rubber can sit on this node; if not, <paramref name="reason"/> says why. A
    /// joint holds one, in its own joint-part slot next to a Servo, a Wheel and a Touch sensor.
    /// </summary>
    public bool CanAddRubber(int nodeId, [NotNullWhen(false)] out UiText? reason)
    {
        if (!HasNode(nodeId))
        {
            reason = RubberGoesOnAJointReason;
            return false;
        }

        if (_rubbers.Any(rubber => rubber.NodeId == nodeId))
        {
            reason = OneRubberPerJointReason;
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>Adds a Rubber and returns its id; see <see cref="CanAddRubber"/>.</summary>
    public int AddRubber(int nodeId)
    {
        if (!CanAddRubber(nodeId, out var reason))
        {
            throw new ArgumentException(reason.Message);
        }

        var id = AllocatePartId();
        _rubbers.Add(new RubberDef(id, nodeId));
        return id;
    }

    public bool ServoNeedsTwoSides(int nodeId) => HasNode(nodeId) && !JointMotor.HasTwoSides(LinksAt(nodeId), WheelAt(nodeId) is not null);

    /// <summary>
    /// Adds a Servo with the first two links at the joint as Fixed and Target. On a joint with a
    /// Wheel and fewer than two links, the Wheel is its Target. A joint with fewer sides
    /// leaves the missing roles empty, so the Servo waits in its error state for a link.
    /// </summary>
    public int AddServo(int nodeId)
    {
        var (fixedSide, targetSide) = HasNode(nodeId) ? JointMotor.DefaultSides(LinksAt(nodeId), WheelAt(nodeId)) : (null, null);
        return AddServo(nodeId, fixedSide, targetSide);
    }

    /// <summary>Adds a Servo with the given Fixed and Target links, each at the joint or empty, as a copied Servo has.</summary>
    public int AddServo(int nodeId, int? fixedLinkId, int? targetLinkId)
    {
        if (!CanAddServo(nodeId, out var reason))
        {
            throw new ArgumentException(reason.Message);
        }

        foreach (var linkId in new[] { fixedLinkId, targetLinkId }.OfType<int>())
        {
            ValidateServoSide(linkId, nodeId);
        }

        var id = AllocatePartId();
        _servos.Add(new ServoDef(id, nodeId, fixedLinkId, targetLinkId));
        return id;
    }

    /// <summary>The Wheel on joint <paramref name="nodeId"/>, if it has one.</summary>
    public WheelDef? WheelAt(int nodeId) => _wheels.FirstOrDefault(wheel => wheel.NodeId == nodeId);

    /// <summary>The beams, Pistons and Springs touching <paramref name="nodeId"/>, in their current list order.</summary>
    public IReadOnlyList<LinkRef> LinksAt(int nodeId) => [.. Links().Where(link => link.Touches(nodeId))];

    /// <summary>The beam, Piston or Spring with <paramref name="linkId"/>.</summary>
    public LinkRef Link(int linkId) => LinkRef.Find(Links(), linkId);

    /// <summary>
    /// Adds a hinge part of <paramref name="kind"/>, a Servo, Velocity motor, Stepper, Brake or Angle sensor,
    /// with the given Fixed and Target sides, each at the joint or empty, as a copied one has.
    /// </summary>
    public int AddHingePart(PartKind kind, int nodeId, int? fixedLinkId, int? targetLinkId) => kind switch
    {
        PartKind.Servo => AddServo(nodeId, fixedLinkId, targetLinkId),
        PartKind.VelocityMotor => AddVelocityMotor(nodeId, fixedLinkId, targetLinkId),
        PartKind.Stepper => AddStepper(nodeId, fixedLinkId, targetLinkId),
        PartKind.Brake => AddBrake(nodeId, fixedLinkId, targetLinkId),
        PartKind.AngleSensor => AddAngleSensor(nodeId, fixedLinkId, targetLinkId),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only a hinge part has sides."),
    };

    /// <summary>A node's current drawn/collision radius without building a CreatureDef.</summary>
    public double NodeRadius(int nodeId)
    {
        ValidateNodeId(nodeId);
        return CreatureDef.JointRadius(nodeId, JointMotors, _wheels);
    }

    /// <summary>
    /// Changes any hinge part's fixed or target side, a link at its joint or its Wheel. Picking the
    /// other role swaps them, and the part keeps its id (<see cref="IHingePartDef.WithLinks"/>).
    /// </summary>
    public void SetHingePartLink(int hingePartId, bool fixedRole, int linkId) =>
        SetSide(ExistingPart(hingePartId) as IHingePartDef ?? throw new ArgumentOutOfRangeException(nameof(hingePartId), "Part id must point to an existing hinge part."), fixedRole, linkId);

    // Gives the hinge part linkId as its fixed or target side.
    private void SetSide(IHingePartDef part, bool fixedRole, int linkId)
    {
        ValidateServoSide(linkId, part.NodeId);
        var (fixedLink, targetLink) = PickSide(part, fixedRole, linkId);
        Replace(part.WithLinks(fixedLink, targetLink));
    }

    // A hinge part's sides once linkId takes the fixed or target role; picking the other role swaps them.
    private static (int? Fixed, int? Target) PickSide(IHingePartDef part, bool fixedRole, int linkId)
    {
        int? fixedLink = part.FixedLinkId;
        int? targetLink = part.TargetLinkId;
        if (fixedRole)
        {
            fixedLink = linkId;
            if (targetLink == linkId)
            {
                targetLink = part.FixedLinkId;
            }
        }
        else
        {
            targetLink = linkId;
            if (fixedLink == linkId)
            {
                fixedLink = part.TargetLinkId;
            }
        }

        return (fixedLink, targetLink);
    }

    /// <summary>Adds a sensor mounted on an existing beam unless that beam already has a sensor.</summary>
    public bool AddSensor(int beamId, SensorKind kind, out int sensorId, [NotNullWhen(false)] out UiText? reason)
    {
        ValidateBeamId(beamId);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Sensor kind must be defined.");
        }

        if (!CanMountSensor(beamId, movingSensorId: null, out reason))
        {
            sensorId = 0;
            return false;
        }

        sensorId = AllocatePartId();
        var beam = _beams[BeamIndexOf(beamId)];
        double? aim = kind == SensorKind.Camera
            ? SensorDef.DefaultAim(_nodes[NodeIndexOf(beam.NodeA)].Position, _nodes[NodeIndexOf(beam.NodeB)].Position)
            : null;
        _sensors.Add(new SensorDef(sensorId, beamId, kind, aim: aim));
        reason = null;
        return true;
    }

    /// <summary>
    /// Moves a sensor to another beam unless that beam already has a sensor, as
    /// <see cref="SensorMovedTo"/> shows it.
    /// </summary>
    public bool MoveSensor(int sensorId, int beamId, [NotNullWhen(false)] out UiText? reason)
    {
        var moved = SensorMovedTo(sensorId, beamId);
        if (!CanMountSensor(beamId, sensorId, out reason))
        {
            return false;
        }

        _sensors[SensorIndexOf(sensorId)] = moved;
        return true;
    }

    /// <summary>
    /// Whether a sensor can sit on <paramref name="beamId"/>: one sensor per beam, not counting
    /// <paramref name="movingSensorId"/>, the one being moved.
    /// </summary>
    public bool CanMountSensor(int beamId, int? movingSensorId, [NotNullWhen(false)] out UiText? reason)
    {
        ValidateBeamId(beamId);
        if (_sensors.Any(sensor => sensor.BeamId == beamId && sensor.Id != movingSensorId))
        {
            reason = OneSensorPerBeamReason;
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>
    /// The sensor as it would be on <paramref name="beamId"/>: the same id, so the same network
    /// ports, kind, name and settings. A Camera keeps the world direction it looks in as built, so
    /// its aim along the new beam changes.
    /// </summary>
    public SensorDef SensorMovedTo(int sensorId, int beamId)
    {
        var sensor = _sensors[SensorIndexOf(sensorId)];
        ValidateBeamId(beamId);
        double? aim = null;
        if (sensor.Aim is { } beamAim)
        {
            var (fromA, fromB) = BeamEnds(sensor.BeamId);
            var (toA, toB) = BeamEnds(beamId);
            aim = CameraRays.AimAlong(CameraRays.BeamAngle(fromA, fromB) + beamAim, toA, toB);
        }

        return sensor.OnBeam(beamId, aim);
    }

    private (Vector2D NodeA, Vector2D NodeB) BeamEnds(int beamId)
    {
        var beam = _beams[BeamIndexOf(beamId)];
        return (_nodes[NodeIndexOf(beam.NodeA)].Position, _nodes[NodeIndexOf(beam.NodeB)].Position);
    }

    /// <summary>Renames any part, keeping its id and everything else.</summary>
    public void Rename(int partId, string? name) => Replace(ExistingPart(partId).WithName(name));

    /// <summary>The current drawing, finished or not: what a saved Creation stores.</summary>
    public CreatureDef Build() => new(Parts, _nextPartId);

    /// <summary>The current drawing if it can be simulated, else the player-facing problems that stop it.</summary>
    public bool TryBuild(out CreatureDef? creature, out IReadOnlyList<UiText> errors)
    {
        var built = Build();
        errors = CreatureReadiness.Problems(built);
        creature = errors.Count == 0 ? built : null;
        return creature is not null;
    }

    public int NodeIndexOf(int nodeId) => IndexOf(_nodes, nodeId, nameof(nodeId), "Node id must point to an existing node.");

    public int BeamIndexOf(int beamId) => IndexOf(_beams, beamId, nameof(beamId), "Beam id must point to an existing beam.");

    public int SensorIndexOf(int sensorId) => IndexOf(_sensors, sensorId, nameof(sensorId), "Sensor id must point to an existing sensor.");

    public int PistonIndexOf(int pistonId) => IndexOf(_pistons, pistonId, nameof(pistonId), "Piston id must point to an existing piston.");

    public int ServoIndexOf(int servoId) => IndexOf(_servos, servoId, nameof(servoId), "Servo id must point to an existing servo.");

    public int SpringIndexOf(int springId) => IndexOf(_springs, springId, nameof(springId), "Spring id must point to an existing spring.");

    public int WheelIndexOf(int wheelId) => IndexOf(_wheels, wheelId, nameof(wheelId), "Wheel id must point to an existing wheel.");

    public int TouchSensorIndexOf(int touchSensorId) => IndexOf(_touchSensors, touchSensorId, nameof(touchSensorId), "Touch sensor id must point to an existing touch sensor.");

    public int RubberIndexOf(int rubberId) => IndexOf(_rubbers, rubberId, nameof(rubberId), "Rubber id must point to an existing rubber.");

    // The index of the part with partId in parts, or an ArgumentOutOfRangeException for parameterName.
    private static int IndexOf<T>(List<T> parts, int partId, string parameterName, string message)
        where T : IPartDef
    {
        var index = parts.FindIndex(part => part.Id == partId);
        return index >= 0 ? index : throw new ArgumentOutOfRangeException(parameterName, message);
    }

    private static bool IsSamePair(BeamDef beam, int nodeA, int nodeB) => IsSamePair(beam.NodeA, beam.NodeB, nodeA, nodeB);

    private static bool IsSamePair(int linkA, int linkB, int nodeA, int nodeB) =>
        (linkA == nodeA && linkB == nodeB) || (linkA == nodeB && linkB == nodeA);

    private bool HasNode(int nodeId) => _nodes.Any(node => node.Id == nodeId);

    private bool HasBeam(int beamId) => _beams.Any(beam => beam.Id == beamId);

    private void ValidateNodeId(int nodeId)
    {
        if (!HasNode(nodeId))
        {
            throw new ArgumentOutOfRangeException(nameof(nodeId), "Node id must point to an existing node.");
        }
    }

    private void ValidateBeamId(int beamId)
    {
        if (!HasBeam(beamId))
        {
            throw new ArgumentOutOfRangeException(nameof(beamId), "Beam id must point to an existing beam.");
        }
    }

    private void ValidateServoSide(int sideId, int nodeId)
    {
        if (WheelAt(nodeId)?.Id != sideId && !Link(sideId).Touches(nodeId))
        {
            throw new ArgumentException("A motor can only use links at its joint, or its Wheel.");
        }
    }

    private int AllocatePartId() => _nextPartId++;

    private IEnumerable<LinkRef> Links() => LinkRef.All(_beams, _pistons, _springs);

    /// <summary>The part with <paramref name="partId"/>, whatever its kind, or null.</summary>
    public IPartDef? FindPart(int partId) => Parts.FirstOrDefault(part => part.Id == partId);

    private IPartDef ExistingPart(int partId) =>
        FindPart(partId) ?? throw new ArgumentOutOfRangeException(nameof(partId), "Part id must point to an existing part.");

    // Puts part in place of the one with its id, in its kind's list.
    private void Replace(IPartDef part) => ListOf(part.PartKind).Replace(part);

    // The list a part of this kind is kept in, so the operations every part has need no branch per kind.
    private IPartList ListOf(PartKind kind) => kind switch
    {
        PartKind.Node => new PartList<NodeDef>(_nodes),
        PartKind.Beam => new PartList<BeamDef>(_beams),
        PartKind.Accelerometer or PartKind.Camera => new PartList<SensorDef>(_sensors),
        PartKind.Servo => new PartList<ServoDef>(_servos),
        PartKind.Piston => new PartList<PistonDef>(_pistons),
        PartKind.Spring => new PartList<SpringDef>(_springs),
        PartKind.Wheel => new PartList<WheelDef>(_wheels),
        PartKind.TouchSensor => new PartList<TouchSensorDef>(_touchSensors),
        PartKind.Rubber => new PartList<RubberDef>(_rubbers),
        PartKind.VelocityMotor => new PartList<VelocityMotorDef>(_velocityMotors),
        PartKind.Brake => new PartList<BrakeDef>(_brakes),
        PartKind.AngleSensor => new PartList<AngleSensorDef>(_angleSensors),
        PartKind.Stepper => new PartList<StepperDef>(_steppers),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Every part kind has a list."),
    };

    private interface IPartList
    {
        void Add(IPartDef part);

        void Replace(IPartDef part);

        void Remove(int partId);
    }

    // One kind's typed list, taking parts of that kind.
    private sealed class PartList<T>(List<T> parts) : IPartList
        where T : IPartDef
    {
        public void Add(IPartDef part) => parts.Add((T)part);

        public void Replace(IPartDef part)
        {
            var index = parts.FindIndex(listed => listed.Id == part.Id);
            parts[index >= 0 ? index : throw new InvalidOperationException($"No {part.PartKind} {part.Id} to replace.")] = (T)part;
        }

        public void Remove(int partId) => parts.RemoveAll(listed => listed.Id == partId);
    }
}
