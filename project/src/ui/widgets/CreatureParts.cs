using Godot;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Mechanics;
using NodeRunner.Theme;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// A creature shown still, made of the same part visuals Training draws (#769, #770): one
/// <see cref="JointPart"/>, <see cref="ServoPart"/>, <see cref="VelocityMotorPart"/>, <see cref="BrakePart"/>, <see cref="WheelPart"/>, <see cref="BeamPart"/>, <see cref="PistonPart"/>,
/// <see cref="SpringPart"/> and <see cref="SensorPart"/> per part, and one <see cref="HatchPart"/> for the rigid hatch, each on
/// its <see cref="CreatureLayers"/> layer. Its points are creature units; its owner places it and
/// hands it what to show after every change. Build's <see cref="BuildCanvas"/> shows its edits
/// with <see cref="CreatureMarks"/>; a <see cref="CreatureThumbnail"/> shows the creature plain.
/// </summary>
public partial class CreatureParts : Node2D
{
    private readonly Dictionary<int, JointPart> _joints = [];
    private readonly Dictionary<int, ServoPart> _servos = [];
    private readonly Dictionary<int, VelocityMotorPart> _velocityMotors = [];
    private readonly Dictionary<int, BrakePart> _brakes = [];
    private readonly Dictionary<int, WheelPart> _wheels = [];
    private readonly Dictionary<int, BeamPart> _beams = [];
    private readonly Dictionary<int, PistonPart> _pistons = [];
    private readonly Dictionary<int, SpringPart> _springs = [];
    private readonly Dictionary<int, SensorPart> _sensors = [];
    private readonly HatchPart _hatch = new();
    private readonly SensorPart _previewSensor = new() { Visible = false };
    private float _pixelScale;

    public CreatureParts()
    {
        AddChild(_hatch);
        AddChild(_previewSensor);
    }

    public VisualTheme Theme { get; set; } = VisualTheme.Neon;

    /// <summary>A saved creature, plain: nothing selected and no edit marks.</summary>
    public void Show(CreatureDef creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        Show(new CreatureShape(creature.Nodes, creature.Beams, creature.Servos, creature.Pistons, creature.Springs, creature.Sensors, creature.Wheels, creature.TouchSensors, creature.Rubbers, creature.VelocityMotors, creature.Brakes, creature.AngleSensors), CreatureMarks.None);
    }

    /// <summary>
    /// Shows <paramref name="shape"/>, which need not be a whole creature yet, with the edit
    /// <paramref name="marks"/> on it.
    /// </summary>
    public void Show(CreatureShape shape, CreatureMarks marks)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(marks);

        var nodes = shape.Nodes.ToDictionary(node => node.Id);
        ShowJoints(shape, marks);
        ShowServos(shape, nodes, marks);
        ShowVelocityMotors(shape, nodes, marks);
        ShowBrakes(shape, nodes, marks);
        ShowWheels(shape, nodes, marks);
        ShowBeams(shape, nodes, marks);
        ShowPistons(shape, nodes, marks);
        ShowSprings(shape, nodes, marks);
        ShowSensors(shape, nodes, marks);
        ShowHatch(shape, nodes);
        ShowDrawGroups(shape, marks);
        PartVisual.RedrawOnNewPixelScale(this, ref _pixelScale);
    }

    private void ShowJoints(CreatureShape shape, CreatureMarks marks)
    {
        Prune(_joints, shape.Nodes.Select(node => node.Id));
        foreach (var node in shape.Nodes)
        {
            var part = PartFor(_joints, node.Id);
            part.Position = ToGodot(node.Position);
            part.Radius = (float)ShownRadius(shape, marks, node.Id);
            part.Loose = marks.ShowsAsLoose(node.Id);
            part.Selected = (marks.Selected.Nodes.Contains(node.Id) && !CreatureDef.HasJointPart(node.Id, shape.MotorsAndBrakes, shape.Wheels, shape.TouchSensors, shape.Rubbers))
                || AngleSensorSelectedAt(shape, marks, node.Id);
            part.ShowsRing = !HasWheel(shape, marks, node.Id) && !HasMotor(shape, marks, node.Id);
            part.ShowsHub = HasWheelAndNoServo(shape, marks, node.Id);
            var touch = shape.TouchSensors.FirstOrDefault(touch => touch.NodeId == node.Id);
            part.Cushion = touch is not null || marks.PreviewNodeOf(BuildPart.TouchSensor) == node.Id;
            part.TouchSelected = touch is not null && marks.Selected.TouchSensors.Contains(touch.Id);
            var rubber = shape.Rubbers.FirstOrDefault(rubber => rubber.NodeId == node.Id);
            part.Skin = rubber is not null || marks.PreviewNodeOf(BuildPart.Rubber) == node.Id;
            part.SkinSelected = rubber is not null && marks.Selected.Rubbers.Contains(rubber.Id);
            part.WheelSelected = shape.Wheels.Any(wheel => wheel.NodeId == node.Id && marks.Selected.Wheels.Contains(wheel.Id))
                || marks.PreviewNodeOf(BuildPart.Wheel) == node.Id;
            var takes = marks.Placing.Joints.Contains(node.Id);
            part.Placing = takes ? PlacingMark.Takes : marks.Placing.RefusedJoints.Contains(node.Id) ? PlacingMark.Refuses : PlacingMark.None;
            part.PlacingRadius = (float)(takes ? marks.Placing.JointRingOf(node.Id) : NodeRadius(shape, node.Id));
        }
    }

    // An Angle sensor is drawn as its plain joint until its picture (#1205), so the joint shows its mark.
    private static bool AngleSensorSelectedAt(CreatureShape shape, CreatureMarks marks, int nodeId) =>
        shape.AngleSensors.Any(angleSensor => angleSensor.NodeId == nodeId && marks.Selected.AngleSensors.Contains(angleSensor.Id));

    // A joint's radius as drawn, with a previewed motor or Wheel, so a cushion sits round it.
    private static double ShownRadius(CreatureShape shape, CreatureMarks marks, int nodeId) =>
        Math.Max(
            NodeRadius(shape, nodeId),
            marks.PreviewNodeOf(BuildPart.Wheel) == nodeId ? WheelDef.DefaultRadius
            : PreviewsMotor(marks, nodeId) ? JointMotor.Radius
            : 0);

    private static bool HasWheel(CreatureShape shape, CreatureMarks marks, int nodeId) =>
        shape.Wheels.Any(wheel => wheel.NodeId == nodeId) || marks.PreviewNodeOf(BuildPart.Wheel) == nodeId;

    // A Wheel's hub is drawn by its joint, over the links (#1086); a motor, placed or previewed, covers it.
    private static bool HasWheelAndNoServo(CreatureShape shape, CreatureMarks marks, int nodeId) =>
        HasWheel(shape, marks, nodeId) && !HasMotor(shape, marks, nodeId);

    // A Servo, Velocity motor (#454) or Brake (#1041), placed or previewed, stands in for its joint's ring.
    private static bool HasMotor(CreatureShape shape, CreatureMarks marks, int nodeId) =>
        shape.MotorsAndBrakes.Any(motor => motor.NodeId == nodeId) || PreviewsMotor(marks, nodeId) || marks.PreviewNodeOf(BuildPart.Brake) == nodeId;

    private static bool PreviewsMotor(CreatureMarks marks, int nodeId) =>
        marks.PreviewNodeOf(BuildPart.Servo) == nodeId || marks.PreviewNodeOf(BuildPart.VelocityMotor) == nodeId;

    // A Wheel stands in for its joint (#129), so it shows the joint's loose mark too. Build shows it
    // unturned; a tray drag previews the smallest Wheel on the joint it would take.
    private void ShowWheels(CreatureShape shape, Dictionary<int, NodeDef> nodes, CreatureMarks marks)
    {
        var ids = shape.Wheels.Select(wheel => wheel.Id).Concat(marks.PreviewNodeOf(BuildPart.Wheel) is { } nodeId ? [-nodeId] : []);
        Prune(_wheels, ids);
        foreach (var wheel in shape.Wheels)
        {
            ShowWheelPart(PartFor(_wheels, wheel.Id), nodes[wheel.NodeId], wheel.Radius, marks.ShowsAsLoose(wheel.NodeId));
        }

        if (marks.PreviewNodeOf(BuildPart.Wheel) is { } previewNode && nodes.TryGetValue(previewNode, out var node))
        {
            ShowWheelPart(PartFor(_wheels, -previewNode), node, WheelDef.DefaultRadius, loose: false);
        }
    }

    // Its joint draws its selection mark (JointPart.WheelSelected), so the face is only raised, never selected.
    private static void ShowWheelPart(WheelPart part, NodeDef joint, double radius, bool loose)
    {
        part.Position = ToGodot(joint.Position);
        part.Radius = (float)radius;
        part.Loose = loose;
        part.Simplified = false;
    }

    private void ShowServos(CreatureShape shape, Dictionary<int, NodeDef> nodes, CreatureMarks marks)
    {
        var ids = shape.Servos.Select(servo => servo.Id).Concat(marks.PreviewNodeOf(BuildPart.Servo) is { } nodeId ? [-nodeId] : []);
        Prune(_servos, ids);
        foreach (var servo in shape.Servos)
        {
            var part = PartFor(_servos, servo.Id);
            ShowServoPart(part, shape, nodes, servo.NodeId, servo.FixedLinkId, servo.TargetLinkId, servo.Range, servo.Start, marks.Selected.Servos.Contains(servo.Id));
        }

        if (marks.PreviewNodeOf(BuildPart.Servo) is { } previewNode && nodes.TryGetValue(previewNode, out var node))
        {
            var part = PartFor(_servos, -previewNode);
            var (fixedId, targetId) = JointMotor.DefaultSides(LinksAtNode(shape, previewNode), shape.Wheels.FirstOrDefault(wheel => wheel.NodeId == previewNode));
            ShowServoPart(
                part,
                shape,
                nodes,
                node.Id,
                fixedId,
                targetId,
                ServoDef.DefaultRange,
                ServoDef.DefaultStart,
                selected: true);
        }
    }

    private void ShowVelocityMotors(CreatureShape shape, Dictionary<int, NodeDef> nodes, CreatureMarks marks)
    {
        var ids = shape.VelocityMotors.Select(motor => motor.Id).Concat(marks.PreviewNodeOf(BuildPart.VelocityMotor) is { } nodeId ? [-nodeId] : []);
        Prune(_velocityMotors, ids);
        foreach (var motor in shape.VelocityMotors)
        {
            var part = PartFor(_velocityMotors, motor.Id);
            ShowVelocityMotorPart(part, shape, nodes, motor.NodeId, motor.FixedLinkId, motor.TargetLinkId, marks.Selected.VelocityMotors.Contains(motor.Id));
        }

        if (marks.PreviewNodeOf(BuildPart.VelocityMotor) is { } previewNode && nodes.TryGetValue(previewNode, out var node))
        {
            var (fixedId, targetId) = JointMotor.DefaultSides(LinksAtNode(shape, previewNode), shape.Wheels.FirstOrDefault(wheel => wheel.NodeId == previewNode));
            ShowVelocityMotorPart(PartFor(_velocityMotors, -previewNode), shape, nodes, node.Id, fixedId, targetId, selected: true);
        }
    }

    // At rest each layer takes its side's built direction; a Wheel's is 0, so the solid magnets
    // sit on its unturned cue tubes.
    private static void ShowVelocityMotorPart(
        VelocityMotorPart part,
        CreatureShape shape,
        Dictionary<int, NodeDef> nodes,
        int nodeId,
        int? fixedLinkId,
        int? targetLinkId,
        bool selected)
    {
        part.Position = ToGodot(nodes[nodeId].Position);
        part.Selected = selected;
        part.Visible = true;
        part.Simplified = false;
        part.HasFixed = TrySide(shape, nodes, nodeId, fixedLinkId, out _, out var fixedAngle);
        part.HasTarget = TrySide(shape, nodes, nodeId, targetLinkId, out _, out var targetAngle);
        part.FixedAngle = fixedAngle;
        part.TargetAngle = targetAngle;
    }

    private void ShowBrakes(CreatureShape shape, Dictionary<int, NodeDef> nodes, CreatureMarks marks)
    {
        var ids = shape.Brakes.Select(brake => brake.Id).Concat(marks.PreviewNodeOf(BuildPart.Brake) is { } nodeId ? [-nodeId] : []);
        Prune(_brakes, ids);
        foreach (var brake in shape.Brakes)
        {
            var part = PartFor(_brakes, brake.Id);
            ShowBrakePart(part, shape, nodes, brake.NodeId, brake.FixedLinkId, brake.TargetLinkId, brake.Locked, marks.Selected.Brakes.Contains(brake.Id));
        }

        if (marks.PreviewNodeOf(BuildPart.Brake) is { } previewNode && nodes.TryGetValue(previewNode, out var node))
        {
            var (fixedId, targetId) = JointMotor.DefaultSides(LinksAtNode(shape, previewNode), shape.Wheels.FirstOrDefault(wheel => wheel.NodeId == previewNode));
            ShowBrakePart(PartFor(_brakes, -previewNode), shape, nodes, node.Id, fixedId, targetId, locked: false, selected: true);
        }
    }

    // At rest the caliper and the disc each take their side's built direction, as a Velocity motor's layers do.
    private static void ShowBrakePart(
        BrakePart part,
        CreatureShape shape,
        Dictionary<int, NodeDef> nodes,
        int nodeId,
        int? fixedLinkId,
        int? targetLinkId,
        bool locked,
        bool selected)
    {
        part.Position = ToGodot(nodes[nodeId].Position);
        part.Selected = selected;
        part.Visible = true;
        part.Simplified = false;
        part.Locked = locked;
        part.HasFixed = TrySide(shape, nodes, nodeId, fixedLinkId, out _, out var fixedAngle);
        part.HasTarget = TrySide(shape, nodes, nodeId, targetLinkId, out _, out var targetAngle);
        part.FixedAngle = fixedAngle;
        part.TargetAngle = targetAngle;
    }

    private static void ShowServoPart(
        ServoPart part,
        CreatureShape shape,
        Dictionary<int, NodeDef> nodes,
        int nodeId,
        int? fixedLinkId,
        int? targetLinkId,
        double range,
        double start,
        bool selected)
    {
        var joint = nodes[nodeId];
        var hasFixed = TrySide(shape, nodes, nodeId, fixedLinkId, out var fixedLink, out var fixedAngle);
        var hasTarget = TrySide(shape, nodes, nodeId, targetLinkId, out _, out var targetAngle);
        var localTarget = hasTarget ? RelativeAngle(fixedAngle, targetAngle) : 0;

        part.Position = ToGodot(joint.Position);
        part.Rotation = fixedAngle;
        part.Radius = (float)JointMotor.Radius;
        part.Selected = selected;
        part.Visible = true;
        part.Simplified = false;
        part.HasFixed = hasFixed;
        part.HasTarget = hasTarget;
        part.TargetAngle = localTarget;
        part.BuiltAngle = localTarget;
        part.Range = (float)range;
        part.Start = (float)start;
        part.HousingReach = fixedLink is { } link ? HousingReach(shape, nodes, link) : 0;
    }

    // A side is a link at the joint, pointing to its far joint, or the joint's Wheel, a one-sided
    // link pointing along its turn (#1118), which is 0 in Build.
    private static bool TrySide(CreatureShape shape, Dictionary<int, NodeDef> nodes, int nodeId, int? sideId, out LinkRef? link, out float angle)
    {
        link = null;
        angle = 0;
        if (sideId is { } id && shape.Wheels.Any(wheel => wheel.Id == id && wheel.NodeId == nodeId))
        {
            return true;
        }

        if (!TryLink(shape, nodeId, sideId, out var found))
        {
            return false;
        }

        link = found;
        angle = LinkAngle(nodes, nodeId, found);
        return true;
    }

    private static bool TryLink(CreatureShape shape, int nodeId, int? linkId, out LinkRef link)
    {
        if (linkId is { } id)
        {
            foreach (var entry in Links(shape))
            {
                if (entry.Id == id && entry.Touches(nodeId))
                {
                    link = entry;
                    return true;
                }
            }
        }

        link = default;
        return false;
    }

    private static IEnumerable<LinkRef> LinksAtNode(CreatureShape shape, int nodeId) =>
        Links(shape).Where(link => link.Touches(nodeId));

    private static IEnumerable<LinkRef> Links(CreatureShape shape) => LinkRef.All(shape.Beams, shape.Pistons, shape.Springs);

    private static float LinkAngle(Dictionary<int, NodeDef> nodes, int jointNodeId, LinkRef link)
    {
        var joint = nodes[jointNodeId].Position;
        var far = nodes[link.FarNodeFrom(jointNodeId)].Position;
        return (float)Math.Atan2(far.Y - joint.Y, far.X - joint.X);
    }

    private static float HousingReach(CreatureShape shape, Dictionary<int, NodeDef> nodes, LinkRef link) =>
        ServoGeometry.HousingReach(nodes.Values.ToArray(), nodeId => CreatureDef.CoveredRadius(nodeId, shape.JointMotors), link, SensorLength(shape, link));

    private static float SensorLength(CreatureShape shape, LinkRef link) =>
        link.Kind == CreatureElementKind.Beam && shape.Sensors.FirstOrDefault(sensor => sensor.BeamId == link.Id) is { } sensor ? (float)SensorPicture.SizeOf(sensor.Kind) : 0;

    private static float RelativeAngle(float fixedAngle, float targetAngle) =>
        Mathf.Atan2(Mathf.Sin(targetAngle - fixedAngle), Mathf.Cos(targetAngle - fixedAngle));

    private void ShowBeams(CreatureShape shape, Dictionary<int, NodeDef> nodes, CreatureMarks marks)
    {
        var selected = marks.Selected;
        Prune(_beams, shape.Beams.Select(beam => beam.Id));
        foreach (var beam in shape.Beams)
        {
            var (nodeA, nodeB) = (nodes[beam.NodeA], nodes[beam.NodeB]);
            var part = PartFor(_beams, beam.Id);
            part.A = ToGodot(nodeA.Position);
            part.B = ToGodot(nodeB.Position);
            part.RadiusA = (float)NodeRadius(shape, nodeA.Id);
            part.RadiusB = (float)NodeRadius(shape, nodeB.Id);
            // A beam too short for training (#593) is drawn in danger until its joints move apart.
            part.Danger = marks.ShowsTooShort && CreatureReadiness.IsTooShort(nodeA, nodeB);
            part.HaloA = selected.Nodes.Contains(beam.NodeA);
            part.HaloB = selected.Nodes.Contains(beam.NodeB);
            part.Selected = selected.Beams.Contains(beam.Id);
            part.Placing = marks.Placing.Beams.Contains(beam.Id) ? PlacingMark.Takes
                : marks.Placing.RefusedBeams.Contains(beam.Id) ? PlacingMark.Refuses
                : PlacingMark.None;
        }
    }

    private void ShowPistons(CreatureShape shape, Dictionary<int, NodeDef> nodes, CreatureMarks marks)
    {
        var selected = marks.Selected;
        Prune(_pistons, shape.Pistons.Select(piston => piston.Id));
        foreach (var piston in shape.Pistons)
        {
            var (nodeA, nodeB) = (nodes[piston.NodeA], nodes[piston.NodeB]);
            var built = Math.Sqrt(Math.Pow(nodeB.Position.X - nodeA.Position.X, 2) + Math.Pow(nodeB.Position.Y - nodeA.Position.Y, 2));
            var part = PartFor(_pistons, piston.Id);
            part.A = ToGodot(nodeA.Position);
            part.B = ToGodot(nodeB.Position);
            part.RadiusA = (float)NodeRadius(shape, nodeA.Id);
            part.RadiusB = (float)NodeRadius(shape, nodeB.Id);
            part.CoveredA = (float)CreatureDef.CoveredRadius(nodeA.Id, shape.JointMotors);
            part.CoveredB = (float)CreatureDef.CoveredRadius(nodeB.Id, shape.JointMotors);
            part.Travel = (float)(Piston.LongestLength(piston, built, Travel.HubRadii) - Piston.ShortestLength(piston, built, Travel.HubRadii));
            part.Danger = marks.ShowsTooShort && CreatureReadiness.IsTooShort(nodeA, nodeB);
            part.HaloA = selected.Nodes.Contains(piston.NodeA);
            part.HaloB = selected.Nodes.Contains(piston.NodeB);
            part.Selected = selected.Pistons.Contains(piston.Id);
        }
    }

    private void ShowSprings(CreatureShape shape, Dictionary<int, NodeDef> nodes, CreatureMarks marks)
    {
        var selected = marks.Selected;
        Prune(_springs, shape.Springs.Select(spring => spring.Id));
        foreach (var spring in shape.Springs)
        {
            var (nodeA, nodeB) = (nodes[spring.NodeA], nodes[spring.NodeB]);
            var built = Math.Sqrt(Math.Pow(nodeB.Position.X - nodeA.Position.X, 2) + Math.Pow(nodeB.Position.Y - nodeA.Position.Y, 2));
            var part = PartFor(_springs, spring.Id);
            part.A = ToGodot(nodeA.Position);
            part.B = ToGodot(nodeB.Position);
            part.Travel = (float)(Spring.LongestLength(spring, built, Travel.HubRadii) - Spring.ShortestLength(spring, built, Travel.HubRadii));
            part.Rest = (float)Spring.RestLength(spring, built, Travel.HubRadii);
            part.Stiffness = spring.Stiffness;
            part.RadiusA = (float)NodeRadius(shape, nodeA.Id);
            part.RadiusB = (float)NodeRadius(shape, nodeB.Id);
            part.CoveredA = (float)CreatureDef.CoveredRadius(nodeA.Id, shape.JointMotors);
            part.CoveredB = (float)CreatureDef.CoveredRadius(nodeB.Id, shape.JointMotors);
            part.Danger = marks.ShowsTooShort && CreatureReadiness.IsTooShort(nodeA, nodeB);
            part.HaloA = selected.Nodes.Contains(spring.NodeA);
            part.HaloB = selected.Nodes.Contains(spring.NodeB);
            part.Selected = selected.Springs.Contains(spring.Id);
        }
    }

    /// <summary>
    /// Each sensor as a picture on its beam (#576, <see cref="SensorPicture.Centre"/>), upright on the beam's built up
    /// side: the Accelerometer with its weight where <see cref="CreatureMarks.WeightOffset"/> has
    /// it (at rest by default), and the camera looking along its rays.
    /// </summary>
    private void ShowSensors(CreatureShape shape, Dictionary<int, NodeDef> nodes, CreatureMarks marks)
    {
        Prune(_sensors, shape.Sensors.Select(sensor => sensor.Id));
        var beams = shape.Beams.ToDictionary(beam => beam.Id);
        foreach (var sensor in shape.Sensors)
        {
            var part = PartFor(_sensors, sensor.Id);
            var beam = beams[sensor.BeamId];
            PlaceSensor(part, shape, nodes[beam.NodeA], nodes[beam.NodeB], sensor.Kind, sensor.Aim, marks.WeightOffset(sensor.Id));
            part.Selected = marks.Selected.Sensors.Contains(sensor.Id);
        }

        // The sensor a tray drag would place, or a sensor drag would move, on the beam under the finger (#376, #806).
        _previewSensor.Visible = marks.PreviewSensor is not null;
        _previewSensor.Theme = Theme;
        if (marks.PreviewSensor is ({ } previewBeam, var kind, var aim))
        {
            PlaceSensor(_previewSensor, shape, nodes[previewBeam.NodeA], nodes[previewBeam.NodeB], kind, aim, null);
        }
    }

    private static void PlaceSensor(SensorPart part, CreatureShape shape, NodeDef jointA, NodeDef jointB, SensorKind kind, double? aim, Vector2D? weightOffset)
    {
        var (nodeA, nodeB) = (jointA.Position, jointB.Position);
        var beamRotation = (float)CameraRays.BeamAngle(nodeA, nodeB);
        var upSign = Accelerometer.UpSign(nodeA, nodeB);
        var pictureRotation = beamRotation + (upSign == 1 ? Mathf.Pi : 0);
        part.Position = ToGodot(SensorPicture.Centre(nodeA, nodeB, CreatureDef.CoveredRadius(jointA.Id, shape.JointMotors), CreatureDef.CoveredRadius(jointB.Id, shape.JointMotors)));
        part.Rotation = pictureRotation;
        part.Kind = kind;
        part.WeightOffset = weightOffset ?? Accelerometer.RestWeightOffset(beamRotation, upSign);
        part.CameraAim = Vector2.FromAngle((float)(aim ?? SensorDef.DefaultAim(nodeA, nodeB)) + beamRotation - pictureRotation);
    }

    /// <summary>
    /// Puts each part in its draw group (#1107) and on the surface <see cref="CreatureMarks.Raised"/>
    /// gives it, so it is drawn where Build's touches hit it.
    /// </summary>
    private void ShowDrawGroups(CreatureShape shape, CreatureMarks marks)
    {
        var groups = DrawGroups.Of(shape.Nodes, Links(shape));
        var raised = marks.Raised;
        foreach (var (id, part) in _joints)
        {
            part.Place(groups, id, raised.Joints.Contains(id));
        }

        var servoNodes = shape.Servos.ToDictionary(servo => servo.Id, servo => servo.NodeId);
        foreach (var (id, part) in _servos)
        {
            var nodeId = id < 0 ? -id : servoNodes[id];
            part.Place(groups, nodeId, raised.Joints.Contains(nodeId));
        }

        var velocityMotorNodes = shape.VelocityMotors.ToDictionary(motor => motor.Id, motor => motor.NodeId);
        foreach (var (id, part) in _velocityMotors)
        {
            var nodeId = id < 0 ? -id : velocityMotorNodes[id];
            part.Place(groups, nodeId, raised.Joints.Contains(nodeId));
        }

        var brakeNodes = shape.Brakes.ToDictionary(brake => brake.Id, brake => brake.NodeId);
        foreach (var (id, part) in _brakes)
        {
            var nodeId = id < 0 ? -id : brakeNodes[id];
            part.Place(groups, nodeId, raised.Joints.Contains(nodeId));
        }

        var wheelNodes = shape.Wheels.ToDictionary(wheel => wheel.Id, wheel => wheel.NodeId);
        foreach (var (id, part) in _wheels)
        {
            var nodeId = id < 0 ? -id : wheelNodes[id];
            part.Place(groups, nodeId, raised.Wheels.Contains(nodeId));
        }

        foreach (var (id, part) in _beams.Select(entry => (entry.Key, (PartVisual)entry.Value))
            .Concat(_pistons.Select(entry => (entry.Key, (PartVisual)entry.Value)))
            .Concat(_springs.Select(entry => (entry.Key, (PartVisual)entry.Value))))
        {
            part.Place(groups, id, raised.Links.Contains(id));
        }

        var sensors = shape.Sensors.ToDictionary(sensor => sensor.Id);
        foreach (var (id, part) in _sensors)
        {
            part.Place(groups, sensors[id].BeamId, raised.Sensor(sensors[id]));
        }

        if (marks.PreviewSensor is ({ } previewBeam, _, _))
        {
            _previewSensor.Place(groups, previewBeam.Id, raised: true);
        }
    }

    /// <summary>The rigid hatch of the closed triangles among the beams that have length.</summary>
    private void ShowHatch(CreatureShape shape, Dictionary<int, NodeDef> nodes)
    {
        _hatch.Theme = Theme;
        if (DrawableTopology(shape, nodes) is not { } creature)
        {
            _hatch.Lines = [];
            _hatch.Triangles = [];
            return;
        }

        var lines = new List<Vector2>();
        var corners = new List<Vector2>();
        foreach (var triangle in RigidTriangles.Of(creature))
        {
            var a = ToGodot(creature.Nodes[triangle.NodeA].Position);
            var b = ToGodot(creature.Nodes[triangle.NodeB].Position);
            var c = ToGodot(creature.Nodes[triangle.NodeC].Position);
            corners.AddRange([a, b, c]);
            foreach (var (start, end) in TriangleHatch.Lines(a, b, c, Theme.RigidHatchSpacing))
            {
                lines.Add(start);
                lines.Add(end);
            }
        }

        if (!lines.SequenceEqual(_hatch.Lines))
        {
            _hatch.Lines = [.. lines];
        }

        if (!corners.SequenceEqual(_hatch.Triangles))
        {
            _hatch.Triangles = [.. corners];
        }
    }

    /// <summary>The creature made of the beams that have length, or null when it has none or is not yet whole.</summary>
    private static CreatureDef? DrawableTopology(CreatureShape shape, Dictionary<int, NodeDef> nodes)
    {
        var beams = shape.Beams
            .Where(beam => nodes[beam.NodeA].Position != nodes[beam.NodeB].Position)
            .ToArray();
        if (beams.Length == 0)
        {
            return null;
        }

        var nodeIds = beams.SelectMany(beam => new[] { beam.NodeA, beam.NodeB }).ToHashSet();
        var beamIds = beams.Select(beam => beam.Id).ToHashSet();
        try
        {
            return new CreatureDef(
                shape.Nodes.Where(node => nodeIds.Contains(node.Id)).ToArray(),
                beams,
                []);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private TPart PartFor<TPart>(Dictionary<int, TPart> parts, int id)
        where TPart : PartVisual, new()
    {
        if (!parts.TryGetValue(id, out var part))
        {
            part = new TPart();
            parts[id] = part;
            AddChild(part);
        }

        part.Theme = Theme;
        return part;
    }

    private void Prune<TPart>(Dictionary<int, TPart> parts, IEnumerable<int> ids)
        where TPart : PartVisual
    {
        var live = ids.ToHashSet();
        foreach (var id in parts.Keys.Where(id => !live.Contains(id)).ToList())
        {
            RemoveChild(parts[id]);
            parts[id].QueueFree();
            parts.Remove(id);
        }
    }

    private static Vector2 ToGodot(Vector2D position) => new((float)position.X, (float)position.Y);

    private static double NodeRadius(CreatureShape shape, int nodeId) =>
        CreatureDef.JointRadius(nodeId, shape.JointMotors, shape.Wheels);

}
