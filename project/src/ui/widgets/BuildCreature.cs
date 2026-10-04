using Godot;
using NodeRunner.App.Builders;
using NodeRunner.App.Lifecycle;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Theme;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The creature Build is drawing, made of the same part visuals Training draws (#769): one
/// <see cref="JointPart"/>, <see cref="BeamPart"/>, <see cref="PistonPart"/>, <see cref="SpringPart"/>
/// and <see cref="SensorPart"/> per part, and one <see cref="HatchPart"/> for the rigid hatch, each on
/// its <see cref="CreatureLayers"/> layer. Its points are creature units; <see cref="BuildCanvas"/>
/// places it through the view's zoom and pan and hands it what to show after every change.
/// </summary>
public partial class BuildCreature : Node2D
{
    private readonly Dictionary<int, JointPart> _joints = [];
    private readonly Dictionary<int, BeamPart> _beams = [];
    private readonly Dictionary<int, PistonPart> _pistons = [];
    private readonly Dictionary<int, SpringPart> _springs = [];
    private readonly Dictionary<int, SensorPart> _sensors = [];
    private readonly HatchPart _hatch = new();
    private readonly SensorPart _previewSensor = new() { Visible = false };
    private float _pixelScale;

    public BuildCreature()
    {
        AddChild(_hatch);
        AddChild(_previewSensor);
    }

    public VisualTheme Theme { get; set; } = VisualTheme.Neon;

    /// <summary>
    /// Shows <paramref name="viewModel"/>'s creature: <paramref name="selected"/> in <c>halo</c>,
    /// the joints <paramref name="showsAsLoose"/> picks in <c>danger</c>, each Accelerometer's
    /// weight where <paramref name="sensorMotion"/> swings it, and the sensor a tray drag would
    /// place in <paramref name="previewSensor"/>, if any.
    /// </summary>
    public void Show(
        BuildViewModel viewModel,
        PartSet selected,
        Func<int, bool> showsAsLoose,
        BuildSensorMotion sensorMotion,
        (BeamDef Beam, SensorKind Kind)? previewSensor)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(showsAsLoose);
        ArgumentNullException.ThrowIfNull(sensorMotion);

        var nodes = viewModel.Nodes.ToDictionary(node => node.Id);
        ShowJoints(viewModel, selected, showsAsLoose);
        ShowBeams(viewModel, nodes, selected);
        ShowPistons(viewModel, nodes, selected);
        ShowSprings(viewModel, nodes, selected);
        ShowSensors(viewModel, nodes, selected, sensorMotion, previewSensor);
        ShowHatch(viewModel, nodes);
        PartVisual.RedrawOnNewPixelScale(this, ref _pixelScale);
    }

    private void ShowJoints(BuildViewModel viewModel, PartSet selected, Func<int, bool> showsAsLoose)
    {
        Prune(_joints, viewModel.Nodes.Select(node => node.Id));
        foreach (var node in viewModel.Nodes)
        {
            var part = PartFor(_joints, node.Id);
            part.Position = ToGodot(node.Position);
            part.Radius = (float)node.Radius;
            part.Loose = showsAsLoose(node.Id);
            part.Selected = selected.Nodes.Contains(node.Id);
        }
    }

    private void ShowBeams(BuildViewModel viewModel, Dictionary<int, NodeDef> nodes, PartSet selected)
    {
        Prune(_beams, viewModel.Beams.Select(beam => beam.Id));
        foreach (var beam in viewModel.Beams)
        {
            var (nodeA, nodeB) = (nodes[beam.NodeA], nodes[beam.NodeB]);
            var part = PartFor(_beams, beam.Id);
            part.A = ToGodot(nodeA.Position);
            part.B = ToGodot(nodeB.Position);
            part.RadiusA = (float)nodeA.Radius;
            part.RadiusB = (float)nodeB.Radius;
            // A beam too short for training (#593) is drawn in danger until its joints move apart.
            part.Danger = CreatureReadiness.IsTooShort(nodeA, nodeB);
            part.HaloA = selected.Nodes.Contains(beam.NodeA);
            part.HaloB = selected.Nodes.Contains(beam.NodeB);
            part.Selected = selected.Beams.Contains(beam.Id);
        }
    }

    private void ShowPistons(BuildViewModel viewModel, Dictionary<int, NodeDef> nodes, PartSet selected)
    {
        Prune(_pistons, viewModel.Pistons.Select(piston => piston.Id));
        var showStroke = viewModel.CanEdit(PartParameterId.Stroke);
        foreach (var piston in viewModel.Pistons)
        {
            var (nodeA, nodeB) = (nodes[piston.NodeA], nodes[piston.NodeB]);
            var built = Math.Sqrt(Math.Pow(nodeB.Position.X - nodeA.Position.X, 2) + Math.Pow(nodeB.Position.Y - nodeA.Position.Y, 2));
            var part = PartFor(_pistons, piston.Id);
            part.A = ToGodot(nodeA.Position);
            part.B = ToGodot(nodeB.Position);
            part.RadiusA = (float)nodeA.Radius;
            part.RadiusB = (float)nodeB.Radius;
            part.Shortest = (float)Piston.ShortestLength(built, piston.Stroke);
            part.Longest = (float)Piston.LongestLength(built, piston.Stroke);
            part.Danger = CreatureReadiness.IsTooShort(nodeA, nodeB);
            part.ShowStroke = showStroke;
            part.HaloA = selected.Nodes.Contains(piston.NodeA);
            part.HaloB = selected.Nodes.Contains(piston.NodeB);
            part.Selected = selected.Pistons.Contains(piston.Id);
        }
    }

    private void ShowSprings(BuildViewModel viewModel, Dictionary<int, NodeDef> nodes, PartSet selected)
    {
        Prune(_springs, viewModel.Springs.Select(spring => spring.Id));
        foreach (var spring in viewModel.Springs)
        {
            var (nodeA, nodeB) = (nodes[spring.NodeA], nodes[spring.NodeB]);
            var part = PartFor(_springs, spring.Id);
            part.A = ToGodot(nodeA.Position);
            part.B = ToGodot(nodeB.Position);
            part.RadiusA = (float)nodeA.Radius;
            part.RadiusB = (float)nodeB.Radius;
            part.Danger = CreatureReadiness.IsTooShort(nodeA, nodeB);
            part.HaloA = selected.Nodes.Contains(spring.NodeA);
            part.HaloB = selected.Nodes.Contains(spring.NodeB);
            part.Selected = selected.Springs.Contains(spring.Id);
        }
    }

    /// <summary>
    /// Each sensor as a picture at the middle of its beam (#576), upright on the beam's built up
    /// side: the Accelerometer with its weight where <see cref="BuildSensorMotion"/> has it, and
    /// the camera looking along its rays.
    /// </summary>
    private void ShowSensors(
        BuildViewModel viewModel,
        Dictionary<int, NodeDef> nodes,
        PartSet selected,
        BuildSensorMotion sensorMotion,
        (BeamDef Beam, SensorKind Kind)? previewSensor)
    {
        Prune(_sensors, viewModel.Sensors.Select(sensor => sensor.Id));
        foreach (var sensor in viewModel.Sensors)
        {
            var part = PartFor(_sensors, sensor.Id);
            var beam = viewModel.Beams[viewModel.BeamIndexOf(sensor.BeamId)];
            PlaceSensor(part, nodes[beam.NodeA].Position, nodes[beam.NodeB].Position, sensor.Kind, sensor.Aim, sensorMotion.WeightOffset(sensor.Id));
            part.Selected = selected.Sensors.Contains(sensor.Id);
        }

        // The sensor a tray drag would place on the free beam under the finger (#376).
        _previewSensor.Visible = previewSensor is not null;
        _previewSensor.Theme = Theme;
        if (previewSensor is ({ } previewBeam, var kind))
        {
            PlaceSensor(_previewSensor, nodes[previewBeam.NodeA].Position, nodes[previewBeam.NodeB].Position, kind, null, null);
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
        part.CameraAim = Vector2.FromAngle((float)(aim ?? CameraRays.DefaultAim(nodeA, nodeB)) + beamRotation - pictureRotation);
    }

    /// <summary>The rigid hatch of the closed triangles among the beams that have length.</summary>
    private void ShowHatch(BuildViewModel viewModel, Dictionary<int, NodeDef> nodes)
    {
        _hatch.Theme = Theme;
        if (DrawableTopology(viewModel, nodes) is not { } creature)
        {
            _hatch.Lines = [];
            return;
        }

        var lines = new List<Vector2>();
        foreach (var triangle in RigidTriangles.Of(creature))
        {
            var a = ToGodot(creature.Nodes[triangle.NodeA].Position);
            var b = ToGodot(creature.Nodes[triangle.NodeB].Position);
            var c = ToGodot(creature.Nodes[triangle.NodeC].Position);
            // Every joint is plain today, so all three share one radius.
            foreach (var (start, end) in TriangleHatch.Lines(a, b, c, Theme.RigidHatchSpacing, (float)creature.Nodes[triangle.NodeA].Radius))
            {
                lines.Add(start);
                lines.Add(end);
            }
        }

        if (!lines.SequenceEqual(_hatch.Lines))
        {
            _hatch.Lines = [.. lines];
        }
    }

    /// <summary>The creature made of the beams that have length, or null when it has none or is not yet whole.</summary>
    private static CreatureDef? DrawableTopology(BuildViewModel viewModel, Dictionary<int, NodeDef> nodes)
    {
        var beams = viewModel.Beams
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
                viewModel.Nodes.Where(node => nodeIds.Contains(node.Id)).ToArray(),
                beams,
                viewModel.Sensors.Where(sensor => beamIds.Contains(sensor.BeamId)).ToArray());
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
}
