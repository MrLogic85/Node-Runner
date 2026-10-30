using Godot;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Mechanics;
using NodeRunner.Theme;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// A creature shown still, made of the same part visuals Training draws: one
/// <see cref="JointPart"/>, <see cref="ServoPart"/>, <see cref="VelocityMotorPart"/>, <see cref="BrakePart"/>, <see cref="StepperPart"/>, <see cref="AngleSensorPart"/>, <see cref="WheelPart"/>, <see cref="BeamPart"/>, <see cref="PistonPart"/>,
/// <see cref="SpringPart"/>, <see cref="SensorPart"/> and <see cref="MemoryCellPart"/> per part, and one <see cref="HatchPart"/> for the rigid hatch, each on
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
    private readonly Dictionary<int, StepperPart> _steppers = [];
    private readonly Dictionary<int, AngleSensorPart> _angleSensors = [];
    private readonly Dictionary<int, WheelPart> _wheels = [];
    private readonly Dictionary<int, BeamPart> _beams = [];
    private readonly Dictionary<int, PistonPart> _pistons = [];
    private readonly Dictionary<int, SpringPart> _springs = [];
    private readonly Dictionary<int, SensorPart> _sensors = [];
    private readonly Dictionary<int, MemoryCellPart> _memoryCells = [];

    // What each visual shows: a part, or a dragged joint part's preview on a joint's slot.
    private readonly Dictionary<PartVisual, Shown> _shown = [];
    private readonly HatchPart _hatch = new();
    private readonly SensorPart _previewSensor = new() { Visible = false };
    private readonly MemoryCellPart _previewMemoryCell = new() { Visible = false };
    private float _pixelScale;

    public CreatureParts()
    {
        AddChild(_hatch);
        AddChild(_previewSensor);
        AddChild(_previewMemoryCell);
    }

    /// <summary>The theme of the world it is in (<see cref="VisualTheme.For"/>).</summary>
    public VisualTheme Theme { get; private set; } = VisualTheme.Project;

    public override void _EnterTree() => Theme = VisualTheme.For(this);

    /// <summary>A saved creature, plain: nothing selected and no edit marks.</summary>
    public void Show(CreatureDef creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        Show(new CreatureShape(creature.Parts), CreatureMarks.None);
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
        ShowSteppers(shape, nodes, marks);
        ShowAngleSensors(shape, nodes, marks);
        ShowWheels(shape, nodes, marks);
        ShowBeams(shape, nodes, marks);
        ShowPistons(shape, nodes, marks);
        ShowSprings(shape, nodes, marks);
        ShowSensors(shape, nodes, marks);
        ShowMemoryCells(shape, nodes, marks);
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
            part.Selected = marks.Selected.Contains(CreatureElementKind.Node, node.Id) && !CreatureDef.HasJointPart(node.Id, shape.JointParts);
            part.ShowsRing = !HasWheel(shape, marks, node.Id) && !HasHingePart(shape, marks, node.Id);
            part.ShowsHub = HasWheelAndNoHingePart(shape, marks, node.Id);
            var touch = shape.TouchSensors.FirstOrDefault(touch => touch.NodeId == node.Id);
            part.Cushion = touch is not null || marks.PreviewNodeOf(BuildPart.TouchSensor) == node.Id;
            part.TouchSelected = touch is not null && marks.Selected.Contains(CreatureElementKind.TouchSensor, touch.Id);
            var rubber = shape.Rubbers.FirstOrDefault(rubber => rubber.NodeId == node.Id);
            part.Skin = rubber is not null || marks.PreviewNodeOf(BuildPart.Rubber) == node.Id;
            part.SkinSelected = rubber is not null && marks.Selected.Contains(CreatureElementKind.Rubber, rubber.Id);
            part.WheelSelected = shape.Wheels.Any(wheel => wheel.NodeId == node.Id && marks.Selected.Contains(CreatureElementKind.Wheel, wheel.Id))
                || marks.PreviewNodeOf(BuildPart.Wheel) == node.Id;
            part.Placing = marks.Placing.Joints.Contains(node.Id) ? PlacingMark.Takes : marks.Placing.RefusedJoints.Contains(node.Id) ? PlacingMark.Refuses : PlacingMark.None;
            part.PlacingRadius = (float)(part.Placing switch
            {
                PlacingMark.Takes => marks.Placing.JointRingOf(node.Id),
                PlacingMark.Refuses => marks.Placing.RefusedRingOf(node.Id),
                _ => NodeRadius(shape, node.Id),
            });
        }
    }

    // A joint's radius as drawn, with a previewed motor or Wheel, so a cushion sits round it.
    private static double ShownRadius(CreatureShape shape, CreatureMarks marks, int nodeId) =>
        Math.Max(
            NodeRadius(shape, nodeId),
            marks.PreviewNodeOf(BuildPart.Wheel) == nodeId ? WheelDef.DefaultRadius
            : PreviewsMotor(marks, nodeId) ? JointMotor.Radius
            : 0);

    private static bool HasWheel(CreatureShape shape, CreatureMarks marks, int nodeId) =>
        shape.Wheels.Any(wheel => wheel.NodeId == nodeId) || marks.PreviewNodeOf(BuildPart.Wheel) == nodeId;

    // A Wheel's hub is drawn by its joint, over the links; a hinge part, placed or previewed, covers it.
    private static bool HasWheelAndNoHingePart(CreatureShape shape, CreatureMarks marks, int nodeId) =>
        HasWheel(shape, marks, nodeId) && !HasHingePart(shape, marks, nodeId);

    // A Servo, Velocity motor, Stepper, Brake or Angle sensor, placed or previewed, stands in for its joint's ring.
    private static bool HasHingePart(CreatureShape shape, CreatureMarks marks, int nodeId) =>
        shape.HingeParts.Any(part => part.NodeId == nodeId) || PreviewsMotor(marks, nodeId) || marks.PreviewNodeOf(BuildPart.Brake) == nodeId || marks.PreviewNodeOf(BuildPart.AngleSensor) == nodeId;

    private static bool PreviewsMotor(CreatureMarks marks, int nodeId) =>
        marks.PreviewNodeOf(BuildPart.Servo) == nodeId || marks.PreviewNodeOf(BuildPart.VelocityMotor) == nodeId || marks.PreviewNodeOf(BuildPart.Stepper) == nodeId;

    // A Wheel stands in for its joint, so it shows the joint's loose mark too. Build shows it
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
            ShowWheelPart(PreviewFor(_wheels, previewNode, JointSlot.Wheel), node, WheelDef.DefaultRadius, loose: false);
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
            ShowServoPart(part, shape, nodes, servo.NodeId, servo.FixedLinkId, servo.TargetLinkId, servo.Range, servo.Start, marks.Selected.Contains(CreatureElementKind.Servo, servo.Id));
        }

        if (marks.PreviewNodeOf(BuildPart.Servo) is { } previewNode && nodes.TryGetValue(previewNode, out var node))
        {
            var part = PreviewFor(_servos, previewNode, JointSlot.HingePart);
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
            ShowVelocityMotorPart(part, shape, nodes, motor.NodeId, motor.FixedLinkId, motor.TargetLinkId, marks.Selected.Contains(CreatureElementKind.VelocityMotor, motor.Id));
        }

        if (marks.PreviewNodeOf(BuildPart.VelocityMotor) is { } previewNode && nodes.TryGetValue(previewNode, out var node))
        {
            var (fixedId, targetId) = JointMotor.DefaultSides(LinksAtNode(shape, previewNode), shape.Wheels.FirstOrDefault(wheel => wheel.NodeId == previewNode));
            ShowVelocityMotorPart(PreviewFor(_velocityMotors, previewNode, JointSlot.HingePart), shape, nodes, node.Id, fixedId, targetId, selected: true);
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
            ShowBrakePart(part, shape, nodes, brake.NodeId, brake.FixedLinkId, brake.TargetLinkId, brake.Locked, marks.Selected.Contains(CreatureElementKind.Brake, brake.Id));
        }

        if (marks.PreviewNodeOf(BuildPart.Brake) is { } previewNode && nodes.TryGetValue(previewNode, out var node))
        {
            var (fixedId, targetId) = JointMotor.DefaultSides(LinksAtNode(shape, previewNode), shape.Wheels.FirstOrDefault(wheel => wheel.NodeId == previewNode));
            ShowBrakePart(PreviewFor(_brakes, previewNode, JointSlot.HingePart), shape, nodes, node.Id, fixedId, targetId, locked: false, selected: true);
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

    private void ShowSteppers(CreatureShape shape, Dictionary<int, NodeDef> nodes, CreatureMarks marks)
    {
        var ids = shape.Steppers.Select(stepper => stepper.Id).Concat(marks.PreviewNodeOf(BuildPart.Stepper) is { } nodeId ? [-nodeId] : []);
        Prune(_steppers, ids);
        foreach (var stepper in shape.Steppers)
        {
            var part = PartFor(_steppers, stepper.Id);
            ShowStepperPart(part, shape, nodes, stepper.NodeId, stepper.FixedLinkId, stepper.TargetLinkId, stepper.StepsPerTurn, marks.Selected.Contains(CreatureElementKind.Stepper, stepper.Id), Cushioned(shape, marks, stepper.NodeId));
        }

        if (marks.PreviewNodeOf(BuildPart.Stepper) is { } previewNode && nodes.TryGetValue(previewNode, out var node))
        {
            var (fixedId, targetId) = JointMotor.DefaultSides(LinksAtNode(shape, previewNode), shape.Wheels.FirstOrDefault(wheel => wheel.NodeId == previewNode));
            ShowStepperPart(PreviewFor(_steppers, previewNode, JointSlot.HingePart), shape, nodes, node.Id, fixedId, targetId, StepperDef.DefaultStepsPerTurn, selected: true, Cushioned(shape, marks, node.Id));
        }
    }

    private static bool Cushioned(CreatureShape shape, CreatureMarks marks, int nodeId) =>
        shape.TouchSensors.Any(touch => touch.NodeId == nodeId) || marks.PreviewNodeOf(BuildPart.TouchSensor) == nodeId;

    // The rim takes the Fixed side's built direction and the pawl the Target's, so notch 0 sits on the
    // Target; with a side missing, both take the one there is.
    private static void ShowStepperPart(
        StepperPart part,
        CreatureShape shape,
        Dictionary<int, NodeDef> nodes,
        int nodeId,
        int? fixedLinkId,
        int? targetLinkId,
        int stepsPerTurn,
        bool selected,
        bool cushioned)
    {
        part.Position = ToGodot(nodes[nodeId].Position);
        part.Selected = selected;
        part.Visible = true;
        part.Simplified = false;
        part.StepsPerTurn = stepsPerTurn;
        part.Cushioned = cushioned;
        part.HasFixed = TrySide(shape, nodes, nodeId, fixedLinkId, out _, out var fixedAngle);
        part.HasTarget = TrySide(shape, nodes, nodeId, targetLinkId, out _, out var targetAngle);
        var (rim, pawl, built) = StepperGeometry.BuildAngles(part.HasFixed ? fixedAngle : null, part.HasTarget ? targetAngle : null);
        part.Turn(rim, pawl);
        part.BuiltAngle = built;
    }

    private void ShowAngleSensors(CreatureShape shape, Dictionary<int, NodeDef> nodes, CreatureMarks marks)
    {
        var ids = shape.AngleSensors.Select(angleSensor => angleSensor.Id).Concat(marks.PreviewNodeOf(BuildPart.AngleSensor) is { } nodeId ? [-nodeId] : []);
        Prune(_angleSensors, ids);
        foreach (var angleSensor in shape.AngleSensors)
        {
            var part = PartFor(_angleSensors, angleSensor.Id);
            ShowAngleSensorPart(part, shape, nodes, marks, angleSensor.NodeId, angleSensor.FixedLinkId, angleSensor.TargetLinkId, marks.Selected.Contains(CreatureElementKind.AngleSensor, angleSensor.Id));
        }

        if (marks.PreviewNodeOf(BuildPart.AngleSensor) is { } previewNode && nodes.TryGetValue(previewNode, out var node))
        {
            var (fixedId, targetId) = JointMotor.DefaultSides(LinksAtNode(shape, previewNode), shape.Wheels.FirstOrDefault(wheel => wheel.NodeId == previewNode));
            ShowAngleSensorPart(PreviewFor(_angleSensors, previewNode, JointSlot.HingePart), shape, nodes, marks, node.Id, fixedId, targetId, selected: true);
        }
    }

    private static void ShowAngleSensorPart(
        AngleSensorPart part,
        CreatureShape shape,
        Dictionary<int, NodeDef> nodes,
        CreatureMarks marks,
        int nodeId,
        int? fixedLinkId,
        int? targetLinkId,
        bool selected)
    {
        part.Position = ToGodot(nodes[nodeId].Position);
        part.Selected = selected;
        part.Visible = true;
        part.Simplified = false;
        part.ArcRadius = (float)AngleSensorDef.ArcRadiusAt(
            nodeId,
            shape.Rubbers.Select(rubber => rubber.NodeId).Concat(marks.PreviewNodeOf(BuildPart.Rubber) is { } rubberNode ? [rubberNode] : []),
            shape.TouchSensors.Select(touch => touch.NodeId).Concat(marks.PreviewNodeOf(BuildPart.TouchSensor) is { } touchNode ? [touchNode] : []));
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
    // link pointing along its turn, which is 0 in Build.
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
        ServoGeometry.HousingReach(nodes.Values.ToArray(), nodeId => CreatureDef.CoveredRadius(nodeId, shape.JointMotors), link, BeamPartLength(shape, link));

    // A Servo's housing is drawn over its Fixed link, so a Piston's or Spring's telescope starts past it.
    private static float LinkCover(CreatureShape shape, Dictionary<int, NodeDef> nodes, LinkRef link, int nodeId) =>
        ServoGeometry.LinkCover(nodes.Values.ToArray(), id => CreatureDef.CoveredRadius(id, shape.JointMotors), shape.Servos, link, nodeId);

    private static float BeamPartLength(CreatureShape shape, LinkRef link) =>
        link.Kind == CreatureElementKind.Beam && CreatureDef.BeamPartOn(link.Id, shape.BeamParts) is { } part ? (float)SensorPicture.SizeOf(part) : 0;

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
            // A beam too short for training is drawn in danger until its joints move apart.
            part.Danger = marks.ShowsTooShort && CreatureReadiness.IsTooShort(CreatureElementKind.Beam, nodeA, nodeB, shape.JointMotors);
            part.HaloA = selected.Contains(CreatureElementKind.Node, beam.NodeA);
            part.HaloB = selected.Contains(CreatureElementKind.Node, beam.NodeB);
            part.Selected = selected.Contains(CreatureElementKind.Beam, beam.Id);
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
            var link = new LinkRef(piston.Id, piston.NodeA, piston.NodeB, CreatureElementKind.Piston);
            part.CoveredA = LinkCover(shape, nodes, link, nodeA.Id);
            part.CoveredB = LinkCover(shape, nodes, link, nodeB.Id);
            part.Shortest = (float)Piston.ShortestLength(piston, built, Travel.HubRadii);
            part.Longest = (float)Piston.LongestLength(piston, built, Travel.HubRadii);
            part.Danger = marks.ShowsTooShort && CreatureReadiness.IsTooShort(CreatureElementKind.Piston, nodeA, nodeB, shape.JointMotors);
            part.HaloA = selected.Contains(CreatureElementKind.Node, piston.NodeA);
            part.HaloB = selected.Contains(CreatureElementKind.Node, piston.NodeB);
            part.Selected = selected.Contains(CreatureElementKind.Piston, piston.Id);
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
            part.Shortest = (float)Spring.ShortestLength(spring, built, Travel.HubRadii);
            part.Longest = (float)Spring.LongestLength(spring, built, Travel.HubRadii);
            part.Rest = (float)Spring.RestLength(spring, built, Travel.HubRadii);
            part.Stiffness = spring.Stiffness;
            part.RadiusA = (float)NodeRadius(shape, nodeA.Id);
            part.RadiusB = (float)NodeRadius(shape, nodeB.Id);
            var link = new LinkRef(spring.Id, spring.NodeA, spring.NodeB, CreatureElementKind.Spring);
            part.CoveredA = LinkCover(shape, nodes, link, nodeA.Id);
            part.CoveredB = LinkCover(shape, nodes, link, nodeB.Id);
            part.Danger = marks.ShowsTooShort && CreatureReadiness.IsTooShort(CreatureElementKind.Spring, nodeA, nodeB, shape.JointMotors);
            part.HaloA = selected.Contains(CreatureElementKind.Node, spring.NodeA);
            part.HaloB = selected.Contains(CreatureElementKind.Node, spring.NodeB);
            part.Selected = selected.Contains(CreatureElementKind.Spring, spring.Id);
        }
    }

    // Each sensor as a picture on its beam (SensorPicture.Centre), upright on the beam's built up
    // side: the Accelerometer with its weight where CreatureMarks.WeightOffset has
    // it (at rest by default), and the camera looking along its rays.
    private void ShowSensors(CreatureShape shape, Dictionary<int, NodeDef> nodes, CreatureMarks marks)
    {
        Prune(_sensors, shape.Sensors.Select(sensor => sensor.Id));
        var beams = shape.Beams.ToDictionary(beam => beam.Id);
        foreach (var sensor in shape.Sensors)
        {
            var part = PartFor(_sensors, sensor.Id);
            var beam = beams[sensor.BeamId];
            PlaceSensor(part, shape, nodes[beam.NodeA], nodes[beam.NodeB], sensor.Kind, sensor.Aim, marks.WeightOffset(sensor.Id));
            part.Selected = marks.Selected.Contains(CreatureElementKind.Sensor, sensor.Id);
        }

        // The sensor a tray drag would place, or a sensor drag would move, on the beam under the finger.
        _previewSensor.Visible = marks.PreviewSensor is not null;
        _previewSensor.Theme = Theme;
        if (marks.PreviewSensor is ({ } previewBeam, var kind, var aim))
        {
            PlaceSensor(_previewSensor, shape, nodes[previewBeam.NodeA], nodes[previewBeam.NodeB], kind, aim, null);
        }
    }

    // Each Memory cell as its glyph on its beam, upright on the beam's built up side as a sensor is,
    // and the one a tray drag would place on the beam under the finger.
    private void ShowMemoryCells(CreatureShape shape, Dictionary<int, NodeDef> nodes, CreatureMarks marks)
    {
        Prune(_memoryCells, shape.MemoryCells.Select(memoryCell => memoryCell.Id));
        var beams = shape.Beams.ToDictionary(beam => beam.Id);
        foreach (var memoryCell in shape.MemoryCells)
        {
            var part = PartFor(_memoryCells, memoryCell.Id);
            var beam = beams[memoryCell.BeamId];
            PlaceOnBeam(part, shape, nodes[beam.NodeA], nodes[beam.NodeB]);
            part.Selected = marks.Selected.Contains(CreatureElementKind.MemoryCell, memoryCell.Id);
        }

        _previewMemoryCell.Visible = marks.PreviewMemoryCell is not null;
        _previewMemoryCell.Theme = Theme;
        if (marks.PreviewMemoryCell is { } previewBeam)
        {
            PlaceOnBeam(_previewMemoryCell, shape, nodes[previewBeam.NodeA], nodes[previewBeam.NodeB]);
        }
    }

    private static void PlaceSensor(SensorPart part, CreatureShape shape, NodeDef jointA, NodeDef jointB, SensorKind kind, double? aim, Vector2D? weightOffset)
    {
        var (nodeA, nodeB) = (jointA.Position, jointB.Position);
        var beamRotation = (float)CameraRays.BeamAngle(nodeA, nodeB);
        var upSign = Accelerometer.UpSign(nodeA, nodeB);
        var pictureRotation = PlaceOnBeam(part, shape, jointA, jointB);
        part.Kind = kind;
        part.WeightOffset = weightOffset ?? Accelerometer.RestWeightOffset(beamRotation, upSign);
        part.CameraAim = Vector2.FromAngle((float)(aim ?? SensorDef.DefaultAim(nodeA, nodeB)) + beamRotation - pictureRotation);
    }

    // Puts a beam part's picture on its beam (SensorPicture.Centre), turned so its top faces the beam's
    // built up side, and returns that turn.
    private static float PlaceOnBeam(PartVisual part, CreatureShape shape, NodeDef jointA, NodeDef jointB)
    {
        var (nodeA, nodeB) = (jointA.Position, jointB.Position);
        var pictureRotation = (float)CameraRays.BeamAngle(nodeA, nodeB) + (Accelerometer.UpSign(nodeA, nodeB) == 1 ? Mathf.Pi : 0);
        part.Position = ToGodot(SensorPicture.Centre(nodeA, nodeB, CreatureDef.CoveredRadius(jointA.Id, shape.JointMotors), CreatureDef.CoveredRadius(jointB.Id, shape.JointMotors)));
        part.Rotation = pictureRotation;
        return pictureRotation;
    }

    // Puts each part in its draw group and on the surface CreatureMarks.Raised
    // gives it, so it is drawn where Build's touches hit it.
    private void ShowDrawGroups(CreatureShape shape, CreatureMarks marks)
    {
        var groups = DrawGroups.Of(shape.Nodes, Links(shape));
        var raised = marks.Raised;
        foreach (var (visual, shown) in _shown)
        {
            if (shown.PreviewSlot is { } slot)
            {
                visual.Place(groups, shown.Id, raised.JointPart(shown.Id, slot));
            }
            else
            {
                visual.Place(groups, shape.PartById[shown.Id], raised);
            }
        }

        if (marks.PreviewSensor is ({ } previewBeam, _, _))
        {
            _previewSensor.Place(groups, previewBeam.Id, raised: true);
        }

        if (marks.PreviewMemoryCell is { } cellBeam)
        {
            _previewMemoryCell.Place(groups, cellBeam.Id, raised: true);
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
        try
        {
            return new CreatureDef([.. shape.Nodes.Where(node => nodeIds.Contains(node.Id)), .. beams]);
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
            _shown[part] = new Shown(id, PreviewSlot: null);
            AddChild(part);
        }

        part.Theme = Theme;
        return part;
    }

    // A dragged joint part's preview, kept by its joint's id negated, as no part has that id.
    private TPart PreviewFor<TPart>(Dictionary<int, TPart> parts, int nodeId, JointSlot slot)
        where TPart : PartVisual, new()
    {
        var part = PartFor(parts, -nodeId);
        _shown[part] = new Shown(nodeId, slot);
        return part;
    }

    // The part a visual shows by its id, or with a PreviewSlot the joint a preview stands on.
    private readonly record struct Shown(int Id, JointSlot? PreviewSlot);

    private void Prune<TPart>(Dictionary<int, TPart> parts, IEnumerable<int> ids)
        where TPart : PartVisual
    {
        var live = ids.ToHashSet();
        foreach (var id in parts.Keys.Where(id => !live.Contains(id)).ToList())
        {
            _shown.Remove(parts[id]);
            RemoveChild(parts[id]);
            parts[id].QueueFree();
            parts.Remove(id);
        }
    }

    private static Vector2 ToGodot(Vector2D position) => new((float)position.X, (float)position.Y);

    private static double NodeRadius(CreatureShape shape, int nodeId) =>
        CreatureDef.JointRadius(nodeId, shape.JointMotors, shape.Wheels);
}
