using Godot;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Mechanics;
using NodeRunner.Theme;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// A creature shown still, made of the same part visuals Training draws (#769, #770): one
/// <see cref="JointPart"/>, <see cref="ServoPart"/>, <see cref="WheelPart"/>, <see cref="BeamPart"/>, <see cref="PistonPart"/>,
/// <see cref="SpringPart"/> and <see cref="SensorPart"/> per part, and one <see cref="HatchPart"/> for the rigid hatch, each on
/// its <see cref="CreatureLayers"/> layer. Its points are creature units; its owner places it and
/// hands it what to show after every change. Build's <see cref="BuildCanvas"/> shows its edits
/// with <see cref="CreatureMarks"/>; a <see cref="CreatureThumbnail"/> shows the creature plain.
/// </summary>
public partial class CreatureParts : Node2D
{
    private readonly Dictionary<int, JointPart> _joints = [];
    private readonly Dictionary<int, ServoPart> _servos = [];
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
        Show(new CreatureShape(creature.Nodes, creature.Beams, creature.Servos, creature.Pistons, creature.Springs, creature.Sensors, creature.Wheels), CreatureMarks.None);
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
        ShowWheels(shape, nodes, marks);
        ShowBeams(shape, nodes, marks);
        ShowPistons(shape, nodes, marks);
        ShowSprings(shape, nodes, marks);
        ShowSensors(shape, nodes, marks);
        ShowHatch(shape, nodes);
        PartVisual.RedrawOnNewPixelScale(this, ref _pixelScale);
    }

    private void ShowJoints(CreatureShape shape, CreatureMarks marks)
    {
        Prune(_joints, shape.Nodes.Select(node => node.Id));
        foreach (var node in shape.Nodes)
        {
            var part = PartFor(_joints, node.Id);
            part.Position = ToGodot(node.Position);
            part.Radius = (float)NodeRadius(shape, node.Id);
            part.Loose = marks.ShowsAsLoose(node.Id);
            var hasPart = CreatureDef.HasJointPart(node.Id, shape.Servos, shape.Wheels);
            part.Selected = marks.Selected.Nodes.Contains(node.Id) && !hasPart;
            part.Visible = !hasPart && marks.PreviewJointPart?.NodeId != node.Id;
        }
    }

    // A Wheel stands in for its joint (#129), so it shows the joint's loose mark too. Build shows it
    // unturned; a tray drag previews the smallest Wheel on the joint it would take.
    private void ShowWheels(CreatureShape shape, Dictionary<int, NodeDef> nodes, CreatureMarks marks)
    {
        var ids = shape.Wheels.Select(wheel => wheel.Id).Concat(marks.PreviewNodeOf(BuildPart.Wheel) is { } nodeId ? [-nodeId] : []);
        Prune(_wheels, ids);
        foreach (var wheel in shape.Wheels)
        {
            ShowWheelPart(PartFor(_wheels, wheel.Id), nodes[wheel.NodeId], wheel.Radius, marks.ShowsAsLoose(wheel.NodeId), marks.Selected.Wheels.Contains(wheel.Id));
        }

        if (marks.PreviewNodeOf(BuildPart.Wheel) is { } previewNode && nodes.TryGetValue(previewNode, out var node))
        {
            ShowWheelPart(PartFor(_wheels, -previewNode), node, WheelDef.DefaultRadius, loose: false, selected: true);
        }
    }

    private static void ShowWheelPart(WheelPart part, NodeDef joint, double radius, bool loose, bool selected)
    {
        part.Position = ToGodot(joint.Position);
        part.Radius = (float)radius;
        part.Loose = loose;
        part.Selected = selected;
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
            var links = LinksAtNode(shape, previewNode).OrderBy(link => link.Id).Take(2).ToArray();
            ShowServoPart(
                part,
                shape,
                nodes,
                node.Id,
                links.ElementAtOrDefault(0).Id is var fixedId && fixedId > 0 ? fixedId : null,
                links.ElementAtOrDefault(1).Id is var targetId && targetId > 0 ? targetId : null,
                ServoDef.DefaultRange,
                ServoDef.DefaultStart,
                selected: true);
        }
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
        var hasFixed = TryLink(shape, nodeId, fixedLinkId, out var fixedLink);
        var hasTarget = TryLink(shape, nodeId, targetLinkId, out var targetLink);
        var fixedAngle = hasFixed ? LinkAngle(nodes, nodeId, fixedLink) : 0;
        var targetAngle = hasTarget ? LinkAngle(nodes, nodeId, targetLink) : 0;
        var localTarget = hasTarget ? RelativeAngle(fixedAngle, targetAngle) : 0;

        part.Position = ToGodot(joint.Position);
        part.Rotation = fixedAngle;
        part.Radius = (float)ServoDef.JointRadius;
        part.Selected = selected;
        part.Visible = true;
        part.Simplified = false;
        part.HasFixed = hasFixed;
        part.HasTarget = hasTarget;
        part.TargetAngle = localTarget;
        part.BuiltAngle = hasFixed && hasTarget ? localTarget : 0;
        part.Range = (float)range;
        part.Start = (float)start;
        part.HousingReach = hasFixed ? HousingReach(shape, nodes, fixedLink) : 0;
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
        ServoGeometry.HousingReach(nodes.Values.ToArray(), nodeId => NodeRadius(shape, nodeId), link, SensorLength(shape, link));

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
            part.Danger = marks.ShowsTooShort && IsTooShort(shape, nodeA, nodeB);
            part.HaloA = selected.Nodes.Contains(beam.NodeA);
            part.HaloB = selected.Nodes.Contains(beam.NodeB);
            part.Selected = selected.Beams.Contains(beam.Id);
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
            var radii = NodeRadius(shape, nodeA.Id) + NodeRadius(shape, nodeB.Id);
            part.Travel = (float)(Piston.LongestLength(piston, built, radii) - Piston.ShortestLength(piston, built, radii));
            part.Danger = marks.ShowsTooShort && IsTooShort(shape, nodeA, nodeB);
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
            var radii = NodeRadius(shape, nodeA.Id) + NodeRadius(shape, nodeB.Id);
            part.Travel = (float)(Spring.LongestLength(spring, built, radii) - Spring.ShortestLength(spring, built, radii));
            part.Rest = (float)Spring.RestLength(spring, built, radii);
            part.Stiffness = spring.Stiffness;
            part.RadiusA = (float)NodeRadius(shape, nodeA.Id);
            part.RadiusB = (float)NodeRadius(shape, nodeB.Id);
            part.Danger = marks.ShowsTooShort && IsTooShort(shape, nodeA, nodeB);
            part.HaloA = selected.Nodes.Contains(spring.NodeA);
            part.HaloB = selected.Nodes.Contains(spring.NodeB);
            part.Selected = selected.Springs.Contains(spring.Id);
        }
    }

    /// <summary>
    /// Each sensor as a picture at the middle of its beam (#576), upright on the beam's built up
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
            PlaceSensor(part, nodes[beam.NodeA].Position, nodes[beam.NodeB].Position, sensor.Kind, sensor.Aim, marks.WeightOffset(sensor.Id));
            part.Selected = marks.Selected.Sensors.Contains(sensor.Id);
        }

        // The sensor a tray drag would place, or a sensor drag would move, on the beam under the finger (#376, #806).
        _previewSensor.Visible = marks.PreviewSensor is not null;
        _previewSensor.Theme = Theme;
        if (marks.PreviewSensor is ({ } previewBeam, var kind, var aim))
        {
            PlaceSensor(_previewSensor, nodes[previewBeam.NodeA].Position, nodes[previewBeam.NodeB].Position, kind, aim, null);
        }
    }

    private static void PlaceSensor(SensorPart part, Vector2D nodeA, Vector2D nodeB, SensorKind kind, double? aim, Vector2D? weightOffset)
    {
        var beamRotation = (float)CameraRays.BeamAngle(nodeA, nodeB);
        var upSign = Accelerometer.UpSign(nodeA, nodeB);
        var pictureRotation = beamRotation + (upSign == 1 ? Mathf.Pi : 0);
        part.Position = (ToGodot(nodeA) + ToGodot(nodeB)) / 2;
        part.Rotation = pictureRotation;
        part.Kind = kind;
        part.WeightOffset = weightOffset ?? Accelerometer.RestWeightOffset(beamRotation, upSign);
        part.CameraAim = Vector2.FromAngle((float)(aim ?? SensorDef.DefaultAim(nodeA, nodeB)) + beamRotation - pictureRotation);
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
            foreach (var (start, end) in TriangleHatch.Lines(a, b, c, Theme.RigidHatchSpacing, (float)NodeRadius(shape, creature.Nodes[triangle.NodeA].Id)))
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
        CreatureDef.JointRadius(nodeId, shape.Servos, shape.Wheels);

    private static bool IsTooShort(CreatureShape shape, NodeDef a, NodeDef b) =>
        CreatureReadiness.IsTooShort(a, b, NodeRadius(shape, a.Id), NodeRadius(shape, b.Id));
}
