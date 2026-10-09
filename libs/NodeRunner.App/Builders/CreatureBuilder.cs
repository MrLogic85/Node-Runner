using System.Diagnostics.CodeAnalysis;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Mechanics;

namespace NodeRunner.App.Builders;

/// <summary>
/// Mutable, in-progress creature anatomy driven by Build-mode UI
/// (0.3.0). Add/move/remove nodes, beams, sensors, joint parts, Pistons and Springs here; <see cref="Build"/>
/// returns the drawing as an immutable <see cref="CreatureDef"/> for saving, and
/// <see cref="TryBuild"/> returns it only once it can be simulated. See
/// docs/CREATURE_MODEL.md for the vocabulary and docs/BUILD_MODE.md for
/// the feature this supports.
///
/// Lives in `NodeRunner.App`, not `NodeRunner.Domain`: this is mutable
/// business logic (add/remove/cascade), and Domain keeps only records and
/// a short list of stateless helpers (`libs/NodeRunner.Domain/AGENTS.md`).
/// </summary>
public sealed class CreatureBuilder
{
    /// <summary>Why a sensor cannot go on a beam that already has one.</summary>
    public static UiText OneSensorPerBeamReason { get; } = UiText.Plain("One sensor per beam");

    /// <summary>Why a joint part cannot be added to a node that already has one.</summary>
    public static UiText OnePartPerJointReason { get; } = UiText.Plain("One part per joint");

    /// <summary>Why a Servo dropped on anything but a joint is refused.</summary>
    public static UiText ServosGoOnAJointReason { get; } = UiText.Plain("Servos go on a joint");

    /// <summary>Why a Wheel cannot be added to a node that already has one (#129).</summary>
    public static UiText OneWheelPerJointReason { get; } = UiText.Plain("One wheel per joint");

    /// <summary>Why a Wheel dropped on anything but a joint is refused (#129).</summary>
    public static UiText WheelsGoOnAJointReason { get; } = UiText.Plain("Wheels go on a joint");

    public static UiText ServoNeedsTwoLinksReason { get; } = UiText.Plain("A Servo needs two links at its joint");

    /// <summary>Why another beam cannot join two nodes a beam already holds rigid (#877); a Piston or Spring replaces it instead (#849).</summary>
    public static UiText BeamJoinsTheseNodesReason { get; } = UiText.Plain("A beam already joins these joints");

    /// <summary>Why a beam or another link cannot join two nodes a Piston already links.</summary>
    public static UiText PistonJoinsTheseNodesReason { get; } = UiText.Plain("These joints already have a piston");

    /// <summary>Why a beam or another link cannot join two nodes a Spring already links (#453).</summary>
    public static UiText SpringJoinsTheseNodesReason { get; } = UiText.Plain("These joints already have a spring");

    /// <summary>Why a Piston or Spring cannot replace the beam between two nodes (#849): the sensor on it is never deleted silently.</summary>
    public static UiText SensorSitsOnThisBeamReason { get; } = UiText.Plain("A sensor sits on this beam");

    private readonly List<NodeDef> _nodes = [];
    private readonly List<BeamDef> _beams = [];
    private readonly List<SensorDef> _sensors = [];
    private readonly List<ServoDef> _servos = [];
    private readonly List<PistonDef> _pistons = [];
    private readonly List<SpringDef> _springs = [];
    private readonly List<WheelDef> _wheels = [];
    private int _nextPartId = 1;

    public CreatureBuilder()
    {
    }

    public CreatureBuilder(CreatureDef creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        _nodes.AddRange(creature.Nodes);
        _beams.AddRange(creature.Beams);
        _sensors.AddRange(creature.Sensors);
        _servos.AddRange(creature.Servos);
        _pistons.AddRange(creature.Pistons);
        _springs.AddRange(creature.Springs);
        _wheels.AddRange(creature.Wheels);
        _nextPartId = creature.NextPartId;
    }

    public IReadOnlyList<NodeDef> Nodes => _nodes;

    public IReadOnlyList<BeamDef> Beams => _beams;

    public IReadOnlyList<SensorDef> Sensors => _sensors;

    public IReadOnlyList<ServoDef> Servos => _servos;

    public IReadOnlyList<PistonDef> Pistons => _pistons;

    public IReadOnlyList<SpringDef> Springs => _springs;

    public IReadOnlyList<WheelDef> Wheels => _wheels;

    public int NextPartId => _nextPartId;

    /// <summary>Adds a node and returns its id.</summary>
    public int AddNode(Vector2D position)
    {
        var id = AllocatePartId();
        _nodes.Add(new NodeDef(id, position));
        return id;
    }

    /// <summary>Moves an existing node to a new position, keeping its name.</summary>
    public void MoveNode(int nodeId, Vector2D position)
    {
        var nodeIndex = NodeIndexOf(nodeId);
        var node = _nodes[nodeIndex];
        _nodes[nodeIndex] = new NodeDef(node.Id, position, node.Name);
    }

    /// <summary>
    /// Removes a node, cascading to every beam, sensor, joint part, Piston and Spring that referenced it.
    /// </summary>
    public void RemoveNode(int nodeId)
    {
        var nodeIndex = NodeIndexOf(nodeId);
        var removedLinks = LinksAt(nodeId);
        var removedBeamIds = removedLinks.Where(link => link.Kind == CreatureElementKind.Beam).Select(link => link.Id).ToHashSet();
        var removedLinkIds = removedLinks.Select(link => link.Id).ToArray();

        _beams.RemoveAll(beam => removedBeamIds.Contains(beam.Id));
        _sensors.RemoveAll(sensor => removedBeamIds.Contains(sensor.BeamId));
        _servos.RemoveAll(servo => servo.NodeId == nodeId);
        _wheels.RemoveAll(wheel => wheel.NodeId == nodeId);
        _pistons.RemoveAll(piston => piston.NodeA == nodeId || piston.NodeB == nodeId);
        _springs.RemoveAll(spring => spring.NodeA == nodeId || spring.NodeB == nodeId);
        _nodes.RemoveAt(nodeIndex);
        foreach (var linkId in removedLinkIds)
        {
            ClearServoLink(linkId);
        }
    }

    /// <summary>
    /// Adds a beam between two distinct, existing nodes and returns its
    /// id. Throws if either node id is invalid, the nodes are the
    /// same, or a beam or link between them already exists.
    /// </summary>
    public int AddBeam(int nodeIdA, int nodeIdB)
    {
        ValidateNodeId(nodeIdA);
        ValidateNodeId(nodeIdB);
        if (!CanAddBeam(nodeIdA, nodeIdB, out var reason))
        {
            throw new ArgumentException(reason.Message);
        }

        var id = AllocatePartId();
        _beams.Add(new BeamDef(id, nodeIdA, nodeIdB));
        return id;
    }

    /// <summary>
    /// Whether <see cref="AddBeam"/> would accept this pair (#877): two distinct, existing nodes
    /// with no beam and no link yet; if not, <paramref name="reason"/> says why.
    /// </summary>
    public bool CanAddBeam(int nodeIdA, int nodeIdB, [NotNullWhen(false)] out UiText? reason)
    {
        if (!HasNode(nodeIdA) || !HasNode(nodeIdB) || nodeIdA == nodeIdB)
        {
            reason = UiText.Plain("A beam must connect two different joints.");
            return false;
        }

        reason = BeamBlockedReason(nodeIdA, nodeIdB);
        return reason is null;
    }

    /// <summary>
    /// Whether <see cref="AddPiston"/> would accept this pair (#451): two distinct, existing nodes
    /// with no link yet. A beam between them is replaced (#849), unless a sensor sits on it; if
    /// not, <paramref name="reason"/> says why.
    /// </summary>
    public bool CanAddPiston(int nodeIdA, int nodeIdB, [NotNullWhen(false)] out UiText? reason)
    {
        if (!HasNode(nodeIdA) || !HasNode(nodeIdB) || nodeIdA == nodeIdB)
        {
            reason = UiText.Plain("A piston must connect two different joints.");
            return false;
        }

        reason = MovingLinkBlockedReason(nodeIdA, nodeIdB);
        return reason is null;
    }

    /// <summary>
    /// Whether <see cref="AddSpring"/> would accept this pair (#453): the same rules as a Piston's,
    /// two distinct, existing nodes with no link between them, replacing a beam without a sensor; if not,
    /// <paramref name="reason"/> says why.
    /// </summary>
    public bool CanAddSpring(int nodeIdA, int nodeIdB, [NotNullWhen(false)] out UiText? reason)
    {
        if (!HasNode(nodeIdA) || !HasNode(nodeIdB) || nodeIdA == nodeIdB)
        {
            reason = UiText.Plain("A spring must connect two different joints.");
            return false;
        }

        reason = MovingLinkBlockedReason(nodeIdA, nodeIdB);
        return reason is null;
    }

    // A beam refuses a pair that already has a beam or a link; a Piston or Spring goes through MovingLinkBlockedReason.
    private UiText? BeamBlockedReason(int nodeIdA, int nodeIdB) =>
        BeamBetween(nodeIdA, nodeIdB) is not null ? BeamJoinsTheseNodesReason : LinkReason(nodeIdA, nodeIdB);

    // A Piston or Spring takes a beam's place instead (#849), but never a sensor's beam.
    private UiText? MovingLinkBlockedReason(int nodeIdA, int nodeIdB) =>
        BeamBetween(nodeIdA, nodeIdB) is { } beamId
            ? _sensors.Any(sensor => sensor.BeamId == beamId) ? SensorSitsOnThisBeamReason : null
            : LinkReason(nodeIdA, nodeIdB);

    /// <summary>The beam joining two nodes, or null; a Piston or Spring placed there replaces it (#849).</summary>
    public int? BeamBetween(int nodeIdA, int nodeIdB) =>
        _beams.FirstOrDefault(beam => IsSamePair(beam, nodeIdA, nodeIdB))?.Id;

    // Why the link already between these nodes stops another part; null when there is none.
    private UiText? LinkReason(int nodeIdA, int nodeIdB) =>
        _pistons.Any(piston => IsSamePair(piston.NodeA, piston.NodeB, nodeIdA, nodeIdB)) ? PistonJoinsTheseNodesReason
        : _springs.Any(spring => IsSamePair(spring.NodeA, spring.NodeB, nodeIdA, nodeIdB)) ? SpringJoinsTheseNodesReason
        : null;

    /// <summary>Adds a Piston between two nodes with the default settings, in place of a beam there (#849), and returns its id; see <see cref="CanAddPiston"/>.</summary>
    public int AddPiston(int nodeIdA, int nodeIdB)
    {
        ValidateNodeId(nodeIdA);
        ValidateNodeId(nodeIdB);
        if (!CanAddPiston(nodeIdA, nodeIdB, out var reason))
        {
            throw new ArgumentException(reason.Message);
        }

        var id = AllocatePartId();
        _pistons.Add(new PistonDef(id, nodeIdA, nodeIdB));
        ReplaceBeamWith(nodeIdA, nodeIdB, id);
        return id;
    }

    /// <summary>Adds a Spring between two nodes with the default settings, in place of a beam there (#849), and returns its id; see <see cref="CanAddSpring"/>.</summary>
    public int AddSpring(int nodeIdA, int nodeIdB)
    {
        ValidateNodeId(nodeIdA);
        ValidateNodeId(nodeIdB);
        if (!CanAddSpring(nodeIdA, nodeIdB, out var reason))
        {
            throw new ArgumentException(reason.Message);
        }

        var id = AllocatePartId();
        _springs.Add(new SpringDef(id, nodeIdA, nodeIdB));
        ReplaceBeamWith(nodeIdA, nodeIdB, id);
        return id;
    }

    // The new link takes the beam's place (#849) and its Servo roles: a link change, so such a Servo gets a new id.
    private void ReplaceBeamWith(int nodeIdA, int nodeIdB, int linkId)
    {
        if (BeamBetween(nodeIdA, nodeIdB) is not { } beamId)
        {
            return;
        }

        _beams.RemoveAt(BeamIndexOf(beamId));
        for (var i = 0; i < _servos.Count; i++)
        {
            var servo = _servos[i];
            if (servo.FixedLinkId == beamId || servo.TargetLinkId == beamId)
            {
                _servos[i] = servo.WithLinks(
                    servo.FixedLinkId == beamId ? linkId : servo.FixedLinkId,
                    servo.TargetLinkId == beamId ? linkId : servo.TargetLinkId,
                    AllocatePartId());
            }
        }
    }

    /// <summary>The settings part <paramref name="partId"/> has (#704), in panel order; a joint and a beam have none.</summary>
    public IReadOnlyList<PartParameterId> ParametersOf(int partId) =>
        _pistons.Any(piston => piston.Id == partId) ? _pistonParameters
        : _servos.Any(servo => servo.Id == partId) ? _servoParameters
        : _springs.Any(spring => spring.Id == partId) ? _springParameters
        : _wheels.Any(wheel => wheel.Id == partId) ? _wheelParameters
        : _sensors.Any(sensor => sensor.Id == partId && sensor.Kind == SensorKind.Camera) ? _cameraParameters
        : [];

    /// <summary>
    /// Whether the drawing is one Build lets a player make (#899): every joint inside
    /// <see cref="BuildViewModel.BuildArea"/> and every panel setting inside its slider's range. A
    /// creature from outside the app, like a share code, is checked with this.
    /// </summary>
    public bool IsWithinBuildLimits() =>
        _nodes.All(node => BuildViewModel.BuildArea.Contains(node.Position))
        && _pistons.Select(piston => piston.Id).Concat(_servos.Select(servo => servo.Id)).Concat(_springs.Select(spring => spring.Id)).Concat(_wheels.Select(wheel => wheel.Id))
            .All(partId => ParametersOf(partId).All(parameter =>
                PartParameters.Of(parameter).Slider is not { } slider || slider.Range.Allows(slider.Shown(ParameterValue(partId, parameter)))));

    /// <summary>Part <paramref name="partId"/>'s <paramref name="parameter"/>, in world units.</summary>
    public double ParameterValue(int partId, PartParameterId parameter)
    {
        if (_springs.Any(spring => spring.Id == partId))
        {
            var spring = _springs[SpringIndexOf(partId)];
            return parameter switch
            {
                PartParameterId.Stiffness => spring.Stiffness,
                PartParameterId.Damping => spring.Damping,
                PartParameterId.Stroke => spring.Stroke,
                PartParameterId.CoilLength => spring.CoilLength,
                _ => throw new ArgumentOutOfRangeException(nameof(parameter)),
            };
        }

        if (_wheels.Any(wheel => wheel.Id == partId))
        {
            var wheel = _wheels[WheelIndexOf(partId)];
            return parameter switch
            {
                PartParameterId.WheelRadius => wheel.Radius,
                PartParameterId.Grip => wheel.Grip,
                _ => throw new ArgumentOutOfRangeException(nameof(parameter)),
            };
        }

        if (_servos.Any(servo => servo.Id == partId))
        {
            var servo = _servos[ServoIndexOf(partId)];
            return parameter switch
            {
                PartParameterId.ServoStrength => servo.Strength,
                PartParameterId.Range => servo.Range,
                PartParameterId.ServoStartPosition => servo.Start,
                PartParameterId.AngularMaxSpeed => servo.MaxSpeed,
                PartParameterId.RiseTime => servo.RiseTime,
                _ => throw new ArgumentOutOfRangeException(nameof(parameter)),
            };
        }

        return parameter switch
        {
            PartParameterId.Strength => _pistons[PistonIndexOf(partId)].Strength,
            PartParameterId.Stroke => _pistons[PistonIndexOf(partId)].Stroke,
            PartParameterId.StartPosition => _pistons[PistonIndexOf(partId)].Start,
            PartParameterId.MaxSpeed => _pistons[PistonIndexOf(partId)].MaxSpeed,
            PartParameterId.RiseTime => _pistons[PistonIndexOf(partId)].RiseTime,
            PartParameterId.Aim => Camera(partId).Aim ?? DefaultAim(Camera(partId).BeamId),
            _ => throw new ArgumentOutOfRangeException(nameof(parameter)),
        };
    }

    /// <summary>Sets part <paramref name="partId"/>'s <paramref name="parameter"/>, in world units, keeping its other settings.</summary>
    public void SetParameter(int partId, PartParameterId parameter, double value)
    {
        if (parameter == PartParameterId.Aim)
        {
            var camera = Camera(partId);
            _sensors[SensorIndexOf(partId)] = camera.WithAim(value);
            return;
        }

        if (_springs.Any(spring => spring.Id == partId))
        {
            var springIndex = SpringIndexOf(partId);
            var spring = _springs[springIndex];
            _springs[springIndex] = parameter switch
            {
                PartParameterId.Stiffness => spring.WithSettings(value, spring.Damping, spring.Stroke, spring.CoilLength),
                PartParameterId.Damping => spring.WithSettings(spring.Stiffness, value, spring.Stroke, spring.CoilLength),
                PartParameterId.Stroke => spring.WithSettings(spring.Stiffness, spring.Damping, value, spring.CoilLength),
                PartParameterId.CoilLength => spring.WithSettings(spring.Stiffness, spring.Damping, spring.Stroke, value),
                _ => throw new ArgumentOutOfRangeException(nameof(parameter)),
            };
            return;
        }

        if (_wheels.Any(wheel => wheel.Id == partId))
        {
            var wheelIndex = WheelIndexOf(partId);
            var wheel = _wheels[wheelIndex];
            _wheels[wheelIndex] = parameter switch
            {
                PartParameterId.WheelRadius => wheel.WithSettings(value, wheel.Grip),
                PartParameterId.Grip => wheel.WithSettings(wheel.Radius, value),
                _ => throw new ArgumentOutOfRangeException(nameof(parameter)),
            };
            return;
        }

        if (_servos.Any(servo => servo.Id == partId))
        {
            var servoIndex = ServoIndexOf(partId);
            var servo = _servos[servoIndex];
            _servos[servoIndex] = parameter switch
            {
                PartParameterId.ServoStrength => servo.WithSettings(value, servo.Range, servo.Start, servo.MaxSpeed, servo.RiseTime),
                PartParameterId.Range => servo.WithSettings(servo.Strength, value, servo.Start, servo.MaxSpeed, servo.RiseTime),
                PartParameterId.ServoStartPosition => servo.WithSettings(servo.Strength, servo.Range, value, servo.MaxSpeed, servo.RiseTime),
                PartParameterId.AngularMaxSpeed => servo.WithSettings(servo.Strength, servo.Range, servo.Start, value, servo.RiseTime),
                PartParameterId.RiseTime => servo.WithSettings(servo.Strength, servo.Range, servo.Start, servo.MaxSpeed, value),
                _ => throw new ArgumentOutOfRangeException(nameof(parameter)),
            };
            return;
        }

        var index = PistonIndexOf(partId);
        var piston = _pistons[index];
        _pistons[index] = parameter switch
        {
            PartParameterId.Strength => piston.WithSettings(value, piston.Stroke, piston.Start, piston.MaxSpeed, piston.RiseTime),
            PartParameterId.Stroke => piston.WithSettings(piston.Strength, value, piston.Start, piston.MaxSpeed, piston.RiseTime),
            PartParameterId.StartPosition => piston.WithSettings(piston.Strength, piston.Stroke, value, piston.MaxSpeed, piston.RiseTime),
            PartParameterId.MaxSpeed => piston.WithSettings(piston.Strength, piston.Stroke, piston.Start, value, piston.RiseTime),
            PartParameterId.RiseTime => piston.WithSettings(piston.Strength, piston.Stroke, piston.Start, piston.MaxSpeed, value),
            _ => throw new ArgumentOutOfRangeException(nameof(parameter)),
        };
    }

    private static readonly PartParameterId[] _pistonParameters = [PartParameterId.Strength, PartParameterId.Stroke, PartParameterId.StartPosition, PartParameterId.MaxSpeed, PartParameterId.RiseTime];

    private static readonly PartParameterId[] _servoParameters = [PartParameterId.ServoStrength, PartParameterId.Range, PartParameterId.ServoStartPosition, PartParameterId.AngularMaxSpeed, PartParameterId.RiseTime];

    private static readonly PartParameterId[] _springParameters = [PartParameterId.Stiffness, PartParameterId.Damping, PartParameterId.Stroke, PartParameterId.CoilLength];

    private static readonly PartParameterId[] _wheelParameters = [PartParameterId.WheelRadius, PartParameterId.Grip];

    private static readonly PartParameterId[] _cameraParameters = [PartParameterId.Aim];

    private SensorDef Camera(int sensorId) => _sensors[SensorIndexOf(sensorId)] is { Kind: SensorKind.Camera } camera
        ? camera
        : throw new ArgumentOutOfRangeException(nameof(sensorId), "Only a Camera has an aim.");

    private double DefaultAim(int beamId)
    {
        var beam = _beams[BeamIndexOf(beamId)];
        return SensorDef.DefaultAim(_nodes[NodeIndexOf(beam.NodeA)].Position, _nodes[NodeIndexOf(beam.NodeB)].Position);
    }

    /// <summary>Removes a Piston by id, leaving any Servo that used it incomplete until the player picks another link.</summary>
    public void RemovePiston(int pistonId)
    {
        _pistons.RemoveAt(PistonIndexOf(pistonId));
        ClearServoLink(pistonId);
    }

    /// <summary>Removes a Servo by id.</summary>
    public void RemoveServo(int servoId) => _servos.RemoveAt(ServoIndexOf(servoId));

    /// <summary>Removes a Wheel by id.</summary>
    public void RemoveWheel(int wheelId) => _wheels.RemoveAt(WheelIndexOf(wheelId));

    /// <summary>Removes a Spring by id, leaving any Servo that used it incomplete until the player picks another link.</summary>
    public void RemoveSpring(int springId)
    {
        _springs.RemoveAt(SpringIndexOf(springId));
        ClearServoLink(springId);
    }

    /// <summary>Removes a beam by id, cascading to sensors on it.</summary>
    public void RemoveBeam(int beamId)
    {
        var beamIndex = BeamIndexOf(beamId);
        _beams.RemoveAt(beamIndex);
        _sensors.RemoveAll(sensor => sensor.BeamId == beamId);
        ClearServoLink(beamId);
    }

    private void ClearServoLink(int linkId)
    {
        for (var i = 0; i < _servos.Count; i++)
        {
            var servo = _servos[i];
            if (servo.FixedLinkId == linkId || servo.TargetLinkId == linkId)
            {
                _servos[i] = new ServoDef(
                    servo.Id,
                    servo.NodeId,
                    servo.FixedLinkId == linkId ? null : servo.FixedLinkId,
                    servo.TargetLinkId == linkId ? null : servo.TargetLinkId,
                    servo.Name,
                    servo.Strength,
                    servo.Range,
                    servo.Start,
                    servo.MaxSpeed,
                    servo.RiseTime);
            }
        }
    }

    /// <summary>Whether a Servo can sit on this node; if not, <paramref name="reason"/> says why.</summary>
    public bool CanAddServo(int nodeId, [NotNullWhen(false)] out UiText? reason)
    {
        if (!HasNode(nodeId))
        {
            reason = ServosGoOnAJointReason;
            return false;
        }

        if (CreatureDef.HasJointPart(nodeId, _servos, _wheels))
        {
            reason = OnePartPerJointReason;
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>
    /// Whether a Wheel can sit on this node (#129); if not, <paramref name="reason"/> says why. Until
    /// a joint holds several parts (#1044), a joint with a Wheel or a Servo takes no other.
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

        if (CreatureDef.HasJointPart(nodeId, _servos, _wheels))
        {
            reason = OnePartPerJointReason;
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

    public bool ServoNeedsTwoLinks(int nodeId) => HasNode(nodeId) && !ServoDef.HasTwoLinks(LinksAt(nodeId));

    /// <summary>
    /// Adds a Servo with the first two links at the joint as Fixed and Target. A joint with fewer
    /// links leaves the missing roles empty, so the Servo waits in its error state for a link.
    /// </summary>
    public int AddServo(int nodeId)
    {
        var links = HasNode(nodeId) ? LinksAt(nodeId).OrderBy(link => link.Id).ToArray() : [];
        return AddServo(nodeId, links.Length > 0 ? links[0].Id : null, links.Length > 1 ? links[1].Id : null);
    }

    /// <summary>Adds a Servo with the given Fixed and Target links, each at the joint or empty, as a copied Servo has (#990).</summary>
    public int AddServo(int nodeId, int? fixedLinkId, int? targetLinkId)
    {
        if (!CanAddServo(nodeId, out var reason))
        {
            throw new ArgumentException(reason.Message);
        }

        foreach (var linkId in new[] { fixedLinkId, targetLinkId }.OfType<int>())
        {
            ValidateLinkTouchesNode(linkId, nodeId);
        }

        var id = AllocatePartId();
        _servos.Add(new ServoDef(id, nodeId, fixedLinkId, targetLinkId));
        return id;
    }

    /// <summary>The beams, Pistons and Springs touching <paramref name="nodeId"/>, in their current list order.</summary>
    public IReadOnlyList<LinkRef> LinksAt(int nodeId) => [.. Links().Where(link => link.Touches(nodeId))];

    /// <summary>The beam, Piston or Spring with <paramref name="linkId"/>.</summary>
    public LinkRef Link(int linkId) => LinkRef.Find(Links(), linkId);

    /// <summary>A node's current drawn/collision radius without building a CreatureDef.</summary>
    public double NodeRadius(int nodeId)
    {
        ValidateNodeId(nodeId);
        return CreatureDef.JointRadius(nodeId, _servos, _wheels);
    }

    /// <summary>
    /// Changes the Servo's fixed or target link. Picking the other role swaps them; any real role
    /// change gives the Servo a fresh part id so old sign-dependent weights are not reused.
    /// </summary>
    public int SetServoLink(int servoId, bool fixedRole, int linkId)
    {
        var index = ServoIndexOf(servoId);
        var servo = _servos[index];
        ValidateLinkTouchesNode(linkId, servo.NodeId);
        int? fixedLink = servo.FixedLinkId;
        int? targetLink = servo.TargetLinkId;
        if (fixedRole)
        {
            fixedLink = linkId;
            if (targetLink == linkId)
            {
                targetLink = servo.FixedLinkId;
            }
        }
        else
        {
            targetLink = linkId;
            if (fixedLink == linkId)
            {
                fixedLink = servo.TargetLinkId;
            }
        }

        if (fixedLink == servo.FixedLinkId && targetLink == servo.TargetLinkId)
        {
            return servo.Id;
        }

        var newId = AllocatePartId();
        _servos[index] = servo.WithLinks(fixedLink, targetLink, newId);
        return newId;
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
    /// Moves a sensor to another beam unless that beam already has a sensor (#806), as
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
    /// <paramref name="movingSensorId"/>, the one being moved (#806).
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
    /// The sensor as it would be on <paramref name="beamId"/> (#806): the same id, so the same brain
    /// ports, kind and name. A Camera keeps the world direction it looks in as built, so its aim
    /// along the new beam changes.
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

        return new SensorDef(sensor.Id, beamId, sensor.Kind, sensor.Name, aim);
    }

    private (Vector2D NodeA, Vector2D NodeB) BeamEnds(int beamId)
    {
        var beam = _beams[BeamIndexOf(beamId)];
        return (_nodes[NodeIndexOf(beam.NodeA)].Position, _nodes[NodeIndexOf(beam.NodeB)].Position);
    }

    /// <summary>Removes a sensor by id.</summary>
    public void RemoveSensor(int sensorId)
    {
        var sensorIndex = SensorIndexOf(sensorId);
        _sensors.RemoveAt(sensorIndex);
    }

    public void Rename(int partId, string? name)
    {
        if (partId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(partId), "Part id must be positive.");
        }

        var nodeIndex = _nodes.FindIndex(node => node.Id == partId);
        if (nodeIndex >= 0)
        {
            var node = _nodes[nodeIndex];
            _nodes[nodeIndex] = new NodeDef(node.Id, node.Position, name);
            return;
        }

        var beamIndex = _beams.FindIndex(beam => beam.Id == partId);
        if (beamIndex >= 0)
        {
            var beam = _beams[beamIndex];
            _beams[beamIndex] = new BeamDef(beam.Id, beam.NodeA, beam.NodeB, name);
            return;
        }

        var sensorIndex = _sensors.FindIndex(sensor => sensor.Id == partId);
        if (sensorIndex >= 0)
        {
            var sensor = _sensors[sensorIndex];
            _sensors[sensorIndex] = sensor.WithName(name);
            return;
        }

        var pistonIndex = _pistons.FindIndex(piston => piston.Id == partId);
        if (pistonIndex >= 0)
        {
            _pistons[pistonIndex] = _pistons[pistonIndex].WithName(name);
            return;
        }

        var servoIndex = _servos.FindIndex(servo => servo.Id == partId);
        if (servoIndex >= 0)
        {
            _servos[servoIndex] = _servos[servoIndex].WithName(name);
            return;
        }

        var springIndex = _springs.FindIndex(spring => spring.Id == partId);
        if (springIndex >= 0)
        {
            _springs[springIndex] = _springs[springIndex].WithName(name);
            return;
        }

        var wheelIndex = _wheels.FindIndex(wheel => wheel.Id == partId);
        if (wheelIndex >= 0)
        {
            _wheels[wheelIndex] = _wheels[wheelIndex].WithName(name);
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(partId), "Part id must point to an existing part.");
    }

    /// <summary>The current drawing, finished or not: what a saved Creation stores.</summary>
    public CreatureDef Build() => new(_nodes, _beams, _sensors, _servos, _pistons, _springs, _wheels, _nextPartId);

    /// <summary>The current drawing if it can be simulated, else the player-facing problems that stop it.</summary>
    public bool TryBuild(out CreatureDef? creature, out IReadOnlyList<UiText> errors)
    {
        var built = Build();
        errors = CreatureReadiness.Problems(built);
        creature = errors.Count == 0 ? built : null;
        return creature is not null;
    }

    public int NodeIndexOf(int nodeId)
    {
        var index = _nodes.FindIndex(node => node.Id == nodeId);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nodeId), "Node id must point to an existing node.");
        }

        return index;
    }

    public int BeamIndexOf(int beamId)
    {
        var index = _beams.FindIndex(beam => beam.Id == beamId);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(beamId), "Beam id must point to an existing beam.");
        }

        return index;
    }

    public int SensorIndexOf(int sensorId)
    {
        var index = _sensors.FindIndex(sensor => sensor.Id == sensorId);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sensorId), "Sensor id must point to an existing sensor.");
        }

        return index;
    }

    public int PistonIndexOf(int pistonId)
    {
        var index = _pistons.FindIndex(piston => piston.Id == pistonId);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pistonId), "Piston id must point to an existing piston.");
        }

        return index;
    }

    public int ServoIndexOf(int servoId)
    {
        var index = _servos.FindIndex(servo => servo.Id == servoId);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(servoId), "Servo id must point to an existing servo.");
        }

        return index;
    }

    public int SpringIndexOf(int springId)
    {
        var index = _springs.FindIndex(spring => spring.Id == springId);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(springId), "Spring id must point to an existing spring.");
        }

        return index;
    }

    public int WheelIndexOf(int wheelId)
    {
        var index = _wheels.FindIndex(wheel => wheel.Id == wheelId);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(wheelId), "Wheel id must point to an existing wheel.");
        }

        return index;
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

    private void ValidateLinkTouchesNode(int linkId, int nodeId)
    {
        if (!Link(linkId).Touches(nodeId))
        {
            throw new ArgumentException("A servo can only use links at its joint.");
        }
    }

    private int AllocatePartId() => _nextPartId++;

    private IEnumerable<LinkRef> Links() => LinkRef.All(_beams, _pistons, _springs);
}
