using Godot;
using NodeRunner.Domain;
using NodeRunner.Mechanics;
using NodeRunner.ML;
using NodeRunner.ML.Brains;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

public partial class Creature : Node2D
{
    /// <summary>The most shadows a generation runs at once (#384).</summary>
    public const int MaximumShadows = TrainSettingsDef.MaxShadows;

    // Ground is layer 1. Every creature body, in every shadow, shares layer 2 and masks only the
    // ground, so shadows never touch each other or their own parts (#384).
    private const uint _groundLayer = 1u;
    private const uint _creatureLayer = 1u << 1;

    // A Piston's or Spring's weight is simulated as half on each of its end nodes. A beam's is split
    // half on its own body and a quarter on each end node (#794), so a creature weighs the same.
    private const float _beamWeight = 1.2f;

    // A Piston's end-stop cylinder weighs half a beam on top (#731), so Godot's joints can hold it
    // on its node A.
    private const float _cylinderMass = _beamWeight / 2;

    // The beam body carries half its beam's weight (#794): Godot's solver cannot hold a nearly
    // massless body pinned between heavy nodes, so under Piston load its pins gave way and rigid
    // triangles folded inside out.
    private const float _beamBodyMass = _beamWeight / 2;

    // Godot needs some mass on every body; a node joined to nothing still weighs this.
    private const float _minNodeMass = 0.1f;

    // Beams have no collider, so Godot cannot derive their turning inertia; it is
    // set as a solid bar this thick.
    private const float _beamInertiaThickness = 12f;

    // How near a tap must land to a part, on screen, at least: a part drawn smaller when the
    // Training camera zooms out (#675) still takes a finger.
    private const float _hitTolerancePixels = 16;

    private RigidBody2D[] _beamBodies = [];
    private float[] _beamHalfLengths = [];
    private RigidBody2D[] _nodeBodies = [];

    // The mass each node moves: its own body plus half of each beam body pinned to it. Piston control
    // and Spring damping are tuned to this, not to the node body alone (#794).
    private float[] _nodeLoads = [];

    private float[] _nodeColliderRadii = [];
    private NodeVisual[] _nodeVisuals = [];
    private BeamVisual[] _beamVisuals = [];
    private RigidHatchVisual[] _hatchVisuals = [];
    private SensorVisual[] _sensorVisuals = [];
    private PistonLink[] _pistons = [];
    private PistonVisual[] _pistonVisuals = [];
    private SpringVisual[] _springVisuals = [];
    private RigidBody2D[] _cylinderBodies = [];
    private CameraRaysVisual? _cameraRaysVisual;
    private bool _isShadow;
    private CreatureElementSelection? _selection;
    private IBeamSensor[] _sensors = [];
    private AccelerometerSensor[] _accelerometers = [];
    private double[] _rawInputs = [];
    private int[] _inputPortOf = [];
    private int[] _outputPortOf = [];
    private double[] _sensorValues = [];
    private double[] _outputValues = [];
    private int _outputCount;
    private double[] _scratchA = [];
    private double[] _scratchB = [];
    private bool _isBuilt;

    public CreatureDef? Definition { get; set; }

    public VisualTheme Theme { get; set; } = VisualTheme.Neon;

    public NeuralNetwork? Brain { get; private set; }

    public int BrainSeed { get; private set; }

    public bool IsBuilt => _isBuilt;

    /// <summary>The brain's ports in runtime order (#534); empty when the creature has no brain.</summary>
    public BrainPortLayout Ports { get; private set; } = BrainPortLayout.Empty;

    public IReadOnlyList<AccelerometerSensor> Accelerometers => _accelerometers;

    /// <summary>
    /// True for every shadow except the followed one (#385): each visual draws as it declares in
    /// <see cref="IShadowVisual.AsShadow"/>, the whole creature fades to the theme's shadow alpha
    /// and draws behind the followed creature.
    /// </summary>
    public bool IsShadow
    {
        get => _isShadow;
        set
        {
            _isShadow = value;
            ApplyShadow();
        }
    }

    public override void _Ready()
    {
        if (!_isBuilt && Definition is not null)
        {
            BuildFrom(Definition);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Brain is null || _outputCount == 0)
        {
            return;
        }

        ReadSensors(_sensorValues, delta);
        Brain.Forward(_sensorValues, _outputValues, _scratchA, _scratchB);

        // Each Piston has two outputs in sim order: position, then strength.
        var output = 0;
        foreach (var piston in _pistons)
        {
            piston.Drive(_outputValues[_outputPortOf[output]], _outputValues[_outputPortOf[output + 1]], delta);
            output += 2;
        }
    }

    public void BuildFrom(CreatureDef definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        Definition = definition;
        _selection = null;
        Build(definition);
        ResetSensors();

        if (_outputCount > 0)
        {
            RandomizeBrain(CreateSeed());
        }
    }

    // Builds every body, joint, sensor and picture afresh from the definition, in its built shape
    // at rest. The old parts leave the tree at once, so they never share a physics step or a frame
    // with the new ones.
    private void Build(CreatureDef definition)
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        _isBuilt = true;

        CreateBeams(definition);
        CreateRigidHatches(definition);
        CreateNodes(definition);
        PinBeamsToNodes(definition);
        CreateSensors(definition);
        CreatePistons(definition);
        CreateSprings(definition);
        ConfigureBrainBuffers(definition);
        ApplyShadow();
    }

    public void RandomizeBrain(int seed)
    {
        if (_outputCount == 0)
        {
            Brain = null;
            BrainSeed = seed;
            return;
        }

        BrainSeed = seed;
        var random = new Random(seed);
        Brain = new NeuralNetwork(DirectBrain.LayerSizes(Ports), Activation.Tanh, random, DirectBrain.OutputActivations(Ports));

        GD.Print($"Node Runner brain seed: {seed}");
    }

    /// <summary>
    /// Assigns a specific brain (e.g. a candidate genome from a trial or
    /// generation) instead of randomizing a new one. The brain's layer sizes
    /// must match this creature's brain ports.
    /// </summary>
    public void SetBrain(NeuralNetwork brain, int seed)
    {
        ArgumentNullException.ThrowIfNull(brain);

        var expectedInputs = _sensorValues.Length;
        var expectedOutputs = _outputCount;
        if (brain.LayerSizes[0] != expectedInputs || brain.LayerSizes[^1] != expectedOutputs)
        {
            throw new ArgumentException(
                $"Brain shape [{string.Join(',', brain.LayerSizes)}] does not match this creature's " +
                $"input/output counts [{expectedInputs}, ..., {expectedOutputs}].",
                nameof(brain));
        }

        Brain = brain;
        BrainSeed = seed;
    }

    /// <summary>
    /// Starts the creature over in its built shape at rest, lowered or raised so its lowest point
    /// is at <paramref name="lowestPointY"/>. Every body and joint is built afresh (#798): Godot's
    /// solver remembers the last pushes of joints and contacts and applies them again on the next
    /// step, so bodies merely moved back would start each run a little differently, and a
    /// creature's motion is chaotic enough that the same brain then ends somewhere else. Fresh
    /// bodies make the same brain run the same way every time. The brain, the selection and the
    /// shadow look are kept. This is plain physical reset, not evolution/fitness logic.
    /// </summary>
    public void ResetPose(float lowestPointY)
    {
        if (Definition is null || _nodeBodies.Length == 0)
        {
            return;
        }

        Build(Definition);

        var offset = GlobalTransform.BasisXformInv(new Vector2(0, lowestPointY - LowestPointY));
        foreach (var body in _beamBodies.Concat(_nodeBodies).Concat(_cylinderBodies))
        {
            body.Position += offset;
        }

        ResetSensors();
    }

    /// <summary>
    /// The Y of the creature's lowest physical point: the bottom of the lowest
    /// node collider (beams do not collide). Godot's Y grows downward, so this
    /// is the largest Y. Returns negative infinity for a creature with no nodes.
    /// </summary>
    public float LowestPointY
    {
        get
        {
            var lowest = float.NegativeInfinity;
            for (var i = 0; i < _nodeBodies.Length; i++)
            {
                lowest = Math.Max(lowest, _nodeBodies[i].GlobalPosition.Y + _nodeColliderRadii[i]);
            }

            return lowest;
        }
    }

    /// <summary>
    /// The box around the creature's node colliders, in global coordinates: what the Training
    /// camera fits in view (#675). Beams run between nodes, so the nodes bound them. An empty
    /// rectangle for a creature with no nodes.
    /// </summary>
    public Rect2 Bounds
    {
        get
        {
            if (_nodeBodies.Length == 0)
            {
                return default;
            }

            var bounds = NodeBox(0);
            for (var i = 1; i < _nodeBodies.Length; i++)
            {
                bounds = bounds.Merge(NodeBox(i));
            }

            return bounds;
        }
    }

    /// <summary>
    /// The average position of all beam bodies, used as a simple centroid
    /// for fitness tracking (e.g. forward distance travelled). Returns
    /// Vector2.Zero for a creature with no beams.
    /// </summary>
    public Vector2 CenterOfMass
    {
        get
        {
            if (_beamBodies.Length == 0)
            {
                return Vector2.Zero;
            }

            var sum = Vector2.Zero;
            foreach (var body in _beamBodies)
            {
                sum += body.GlobalPosition;
            }

            return sum / _beamBodies.Length;
        }
    }

    public bool TrySelectPart(Vector2 globalPosition, out CreatureElementSelection? selection)
    {
        var tolerance = GetHitTolerance();
        for (var nodeIndex = 0; nodeIndex < _nodeVisuals.Length; nodeIndex++)
        {
            var radius = Math.Max(tolerance, ToGodotFloat(Definition!.Nodes[nodeIndex].Radius, nameof(NodeDef.Radius)));
            if (_nodeVisuals[nodeIndex].GlobalPosition.DistanceSquaredTo(globalPosition) <= radius * radius)
            {
                selection = new CreatureElementSelection(CreatureElementKind.Node, Definition!.Nodes[nodeIndex].Id);
                return true;
            }
        }

        for (var sensorIndex = 0; sensorIndex < _sensorVisuals.Length; sensorIndex++)
        {
            var beamIndex = Definition!.BeamIndexOf(Definition.Sensors[sensorIndex].BeamId);
            var local = _beamBodies[beamIndex].ToLocal(globalPosition);
            var halfLength = _beamHalfLengths[beamIndex];
            if (SensorPicture.Contains(Definition.Sensors[sensorIndex].Kind, new Vector2D(local.X, local.Y), new Vector2D(-halfLength, 0), new Vector2D(halfLength, 0))
                || local.LengthSquared() <= tolerance * tolerance)
            {
                selection = new CreatureElementSelection(CreatureElementKind.Sensor, Definition.Sensors[sensorIndex].Id);
                return true;
            }
        }

        for (var pistonIndex = 0; pistonIndex < _pistons.Length; pistonIndex++)
        {
            var piston = _pistons[pistonIndex];
            if (DistanceSquaredToSegment(globalPosition, piston.NodeA.GlobalPosition, piston.NodeB.GlobalPosition) <= tolerance * tolerance)
            {
                selection = new CreatureElementSelection(CreatureElementKind.Piston, piston.Definition.Id);
                return true;
            }
        }

        for (var springIndex = 0; springIndex < _springVisuals.Length; springIndex++)
        {
            var spring = _springVisuals[springIndex];
            if (DistanceSquaredToSegment(globalPosition, spring.NodeA.GlobalPosition, spring.NodeB.GlobalPosition) <= tolerance * tolerance)
            {
                selection = new CreatureElementSelection(CreatureElementKind.Spring, Definition!.Springs[springIndex].Id);
                return true;
            }
        }

        for (var beamIndex = 0; beamIndex < _beamBodies.Length; beamIndex++)
        {
            var body = _beamBodies[beamIndex];
            var halfLength = _beamHalfLengths[beamIndex];
            var start = body.ToGlobal(new Vector2(-halfLength, 0));
            var end = body.ToGlobal(new Vector2(halfLength, 0));
            if (DistanceSquaredToSegment(globalPosition, start, end) <= tolerance * tolerance)
            {
                selection = new CreatureElementSelection(CreatureElementKind.Beam, Definition!.Beams[beamIndex].Id);
                return true;
            }
        }

        selection = null;
        return false;
    }

    /// <summary>
    /// Where a part's name points to, in global coordinates (#388): a joint's centre, a beam's or
    /// link's middle, or a sensor's picture.
    /// </summary>
    public Vector2 PartAnchor(CreatureElementSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return selection.Kind switch
        {
            CreatureElementKind.Node => _nodeVisuals[Definition!.NodeIndexOf(selection.Id)].GlobalPosition,
            CreatureElementKind.Beam => _beamBodies[Definition!.BeamIndexOf(selection.Id)].GlobalPosition,
            CreatureElementKind.Sensor => _sensorVisuals[Definition!.Sensors.ToList().FindIndex(sensor => sensor.Id == selection.Id)].GlobalPosition,
            CreatureElementKind.Piston => Middle(_pistons[Definition!.PistonIndexOf(selection.Id)].NodeA, _pistons[Definition.PistonIndexOf(selection.Id)].NodeB),
            CreatureElementKind.Spring => Middle(_springVisuals[Definition!.SpringIndexOf(selection.Id)].NodeA, _springVisuals[Definition.SpringIndexOf(selection.Id)].NodeB),
            _ => throw new ArgumentOutOfRangeException(nameof(selection), selection.Kind, "Not a part of a creature."),
        };

        static Vector2 Middle(Node2D a, Node2D b) => (a.GlobalPosition + b.GlobalPosition) / 2;
    }

    public void SetSelectedElement(CreatureElementSelection? selection)
    {
        _selection = selection;
        ApplySelection();
    }

    // A shadow never shows a selection (#385), so its parts stay on their unselected layers.
    private void ApplySelection()
    {
        foreach (var visual in _nodeVisuals.Concat<PartVisual>(_beamVisuals).Concat(_sensorVisuals).Concat(_pistonVisuals).Concat(_springVisuals))
        {
            visual.Selected = false;
        }

        if (_selection is null || _isShadow)
        {
            return;
        }

        PartVisual selected = _selection.Kind switch
        {
            CreatureElementKind.Node => _nodeVisuals[Definition!.NodeIndexOf(_selection.Id)],
            CreatureElementKind.Beam => _beamVisuals[Definition!.BeamIndexOf(_selection.Id)],
            CreatureElementKind.Sensor => _sensorVisuals[Definition!.Sensors.ToList().FindIndex(sensor => sensor.Id == _selection.Id)],
            CreatureElementKind.Piston => _pistonVisuals[Definition!.PistonIndexOf(_selection.Id)],
            CreatureElementKind.Spring => _springVisuals[Definition!.SpringIndexOf(_selection.Id)],
            _ => throw new ArgumentOutOfRangeException(nameof(_selection), _selection.Kind, "Not a part of a creature."),
        };
        selected.Selected = true;
    }

    private Rect2 NodeBox(int index)
    {
        var radius = _nodeColliderRadii[index];
        return new Rect2(_nodeBodies[index].GlobalPosition - new Vector2(radius, radius), new Vector2(radius, radius) * 2);
    }

    private void ApplyShadow()
    {
        Modulate = Colors.White with { A = _isShadow ? Theme.ShadowAlpha : 1f };
        ZIndex = _isShadow ? ArenaLayers.Shadows : ArenaLayers.Followed;
        foreach (var visual in _nodeVisuals)
        {
            visual.IsShadow = _isShadow;
        }

        foreach (var visual in _beamVisuals)
        {
            visual.IsShadow = _isShadow;
        }

        foreach (var visual in _hatchVisuals)
        {
            visual.IsShadow = _isShadow;
        }

        foreach (var visual in _sensorVisuals)
        {
            visual.IsShadow = _isShadow;
        }

        foreach (var visual in _pistonVisuals)
        {
            visual.IsShadow = _isShadow;
        }

        foreach (var visual in _springVisuals)
        {
            visual.IsShadow = _isShadow;
        }

        if (_cameraRaysVisual is not null)
        {
            _cameraRaysVisual.IsShadow = _isShadow;
        }

        ApplySelection();
    }

    private void ResetSensors()
    {
        foreach (var sensor in _sensors)
        {
            sensor.Reset();
        }
    }

    // A beam is a body without a collider: it carries mass, turning inertia and sensors,
    // and is pinned at both ends to its nodes, which are what touch the world.
    private void CreateBeams(CreatureDef definition)
    {
        var beamDefs = definition.Beams;
        _beamBodies = new RigidBody2D[beamDefs.Count];
        _beamHalfLengths = new float[beamDefs.Count];
        _beamVisuals = new BeamVisual[beamDefs.Count];

        for (var i = 0; i < beamDefs.Count; i++)
        {
            var beamDef = beamDefs[i];
            var nodeAPos = ToGodot(definition.Nodes[definition.NodeIndexOf(beamDef.NodeA)].Position);
            var nodeBPos = ToGodot(definition.Nodes[definition.NodeIndexOf(beamDef.NodeB)].Position);
            var midpoint = (nodeAPos + nodeBPos) / 2;
            var direction = nodeBPos - nodeAPos;
            var halfLength = direction.Length() / 2;
            var rotation = direction.Angle();

            var body = new RigidBody2D
            {
                Name = $"Beam{i}",
                CollisionLayer = _creatureLayer,
                CollisionMask = _groundLayer,
                Position = midpoint,
                Rotation = rotation,
                Mass = _beamBodyMass,
                CenterOfMassMode = RigidBody2D.CenterOfMassModeEnum.Custom,
                CenterOfMass = Vector2.Zero,
                Inertia = _beamBodyMass * ((4 * halfLength * halfLength) + (_beamInertiaThickness * _beamInertiaThickness)) / 12,
                LinearDamp = 0.55f,
                AngularDamp = 0.55f,
                CanSleep = false,
                ContinuousCd = RigidBody2D.CcdMode.CastRay,
            };

            var visual = new BeamVisual
            {
                Name = $"Beam{i}Visual",
                Theme = Theme,
                A = new Vector2(-halfLength, 0),
                B = new Vector2(halfLength, 0),
                RadiusA = ToGodotFloat(definition.Nodes[definition.NodeIndexOf(beamDef.NodeA)].Radius, nameof(NodeDef.Radius)),
                RadiusB = ToGodotFloat(definition.Nodes[definition.NodeIndexOf(beamDef.NodeB)].Radius, nameof(NodeDef.Radius)),
            };
            body.AddChild(visual);
            _beamVisuals[i] = visual;

            AddChild(body);
            _beamBodies[i] = body;
            _beamHalfLengths[i] = halfLength;
        }
    }

    // A node is its own body with a circle collider, weighing a quarter of each beam and half of each
    // link it joins (see _beamWeight). Its rotation is locked:
    // a free-spinning circle pinned at its centre would roll like a wheel and give
    // the creature no grip on the ground.
    private void CreateNodes(CreatureDef definition)
    {
        var count = definition.Nodes.Count;
        _nodeBodies = new RigidBody2D[count];
        _nodeColliderRadii = new float[count];
        _nodeVisuals = new NodeVisual[count];
        _nodeLoads = new float[count];

        var masses = new float[count];
        foreach (var beam in definition.Beams)
        {
            masses[definition.NodeIndexOf(beam.NodeA)] += _beamWeight / 4;
            masses[definition.NodeIndexOf(beam.NodeB)] += _beamWeight / 4;
            _nodeLoads[definition.NodeIndexOf(beam.NodeA)] += _beamBodyMass / 2;
            _nodeLoads[definition.NodeIndexOf(beam.NodeB)] += _beamBodyMass / 2;
        }

        foreach (var piston in definition.Pistons)
        {
            masses[definition.NodeIndexOf(piston.NodeA)] += _beamWeight / 2;
            masses[definition.NodeIndexOf(piston.NodeB)] += _beamWeight / 2;
        }

        foreach (var spring in definition.Springs)
        {
            masses[definition.NodeIndexOf(spring.NodeA)] += _beamWeight / 2;
            masses[definition.NodeIndexOf(spring.NodeB)] += _beamWeight / 2;
        }

        for (var i = 0; i < count; i++)
        {
            var position = ToGodot(definition.Nodes[i].Position);
            var radius = ToGodotFloat(definition.Nodes[i].Radius, nameof(NodeDef.Radius));

            var body = new RigidBody2D
            {
                Name = $"Node{i}",
                CollisionLayer = _creatureLayer,
                CollisionMask = _groundLayer,
                Position = position,
                Mass = Math.Max(masses[i], _minNodeMass),
                LockRotation = true,
                LinearDamp = 0.55f,
                CanSleep = false,
                ContinuousCd = RigidBody2D.CcdMode.CastRay,
            };
            body.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = radius } });
            _nodeLoads[i] += body.Mass;

            var visual = new NodeVisual
            {
                Name = $"Node{i}Visual",
                Theme = Theme,
                Radius = radius,
            };
            body.AddChild(visual);

            AddChild(body);
            _nodeBodies[i] = body;
            _nodeColliderRadii[i] = radius;
            _nodeVisuals[i] = visual;
        }
    }

    private void PinBeamsToNodes(CreatureDef definition)
    {
        for (var i = 0; i < definition.Beams.Count; i++)
        {
            var beamDef = definition.Beams[i];
            PinBeamToNode(i, definition.NodeIndexOf(beamDef.NodeA));
            PinBeamToNode(i, definition.NodeIndexOf(beamDef.NodeB));
        }
    }

    private void PinBeamToNode(int beamIndex, int nodeIndex)
    {
        var pin = new PinJoint2D
        {
            Name = $"Beam{beamIndex}Node{nodeIndex}Pin",
            Position = _nodeBodies[nodeIndex].Position,
        };
        AddChild(pin);
        pin.NodeA = pin.GetPathTo(_nodeBodies[nodeIndex]);
        pin.NodeB = pin.GetPathTo(_beamBodies[beamIndex]);
    }

    // Each rigid triangle's hatch rides on one of its beams. The lines are laid out in the
    // creature's built space, as in Build, so the hatches of triangles that share a beam line up.
    private void CreateRigidHatches(CreatureDef definition)
    {
        var hatches = new List<RigidHatchVisual>();
        foreach (var triangle in RigidTriangles.Of(definition))
        {
            var beamIndex = BeamIndexBetween(definition, triangle.NodeA, triangle.NodeB);
            if (beamIndex < 0)
            {
                continue;
            }

            var a = ToGodot(definition.Nodes[triangle.NodeA].Position);
            var b = ToGodot(definition.Nodes[triangle.NodeB].Position);
            var c = ToGodot(definition.Nodes[triangle.NodeC].Position);
            var toBeam = _beamBodies[beamIndex].Transform.AffineInverse();
            var jointRadius = ToGodotFloat(definition.Nodes[triangle.NodeA].Radius, nameof(NodeDef.Radius));
            var visual = new RigidHatchVisual
            {
                Name = $"Hatch{hatches.Count}",
                Theme = Theme,
                Lines = TriangleHatch.Lines(a, b, c, Theme.RigidHatchSpacing, jointRadius)
                    .SelectMany(line => new[] { toBeam * line.Start, toBeam * line.End })
                    .ToArray(),
                Triangles = [toBeam * a, toBeam * b, toBeam * c],
            };
            _beamBodies[beamIndex].AddChild(visual);
            hatches.Add(visual);
        }

        _hatchVisuals = [.. hatches];
    }

    private static int BeamIndexBetween(CreatureDef definition, int nodeIndexA, int nodeIndexB)
    {
        var (idA, idB) = (definition.Nodes[nodeIndexA].Id, definition.Nodes[nodeIndexB].Id);
        for (var i = 0; i < definition.Beams.Count; i++)
        {
            var beam = definition.Beams[i];
            if ((beam.NodeA == idA && beam.NodeB == idB) || (beam.NodeA == idB && beam.NodeB == idA))
            {
                return i;
            }
        }

        return -1;
    }

    private void CreateSensors(CreatureDef definition)
    {
        var gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsDouble();
        _sensors = new IBeamSensor[definition.Sensors.Count];

        for (var i = 0; i < _sensors.Length; i++)
        {
            var sensor = definition.Sensors[i];
            var beamIndex = definition.BeamIndexOf(sensor.BeamId);
            _sensors[i] = sensor.Kind switch
            {
                SensorKind.Accelerometer => CreateAccelerometer(definition, beamIndex, gravity),
                SensorKind.Camera => new CameraSensor(_beamBodies[beamIndex], sensor.Aim ?? 0),
                _ => throw new InvalidOperationException($"Unknown sensor kind {sensor.Kind}."),
            };
        }

        _accelerometers = _sensors.OfType<AccelerometerSensor>().ToArray();
        CreateSensorVisuals(definition);
    }

    // Each sensor's picture rides its beam body (#576).
    private void CreateSensorVisuals(CreatureDef definition)
    {
        _sensorVisuals = new SensorVisual[_sensors.Length];
        for (var i = 0; i < _sensors.Length; i++)
        {
            var sensor = definition.Sensors[i];
            var beamIndex = definition.BeamIndexOf(sensor.BeamId);
            var beam = definition.Beams[beamIndex];
            var upSign = Accelerometer.UpSign(
                definition.Nodes[definition.NodeIndexOf(beam.NodeA)].Position,
                definition.Nodes[definition.NodeIndexOf(beam.NodeB)].Position);
            var glyphRotation = upSign == 1 ? Mathf.Pi : 0;
            var visual = new SensorVisual
            {
                Name = $"Sensor{i}Picture",
                Theme = Theme,
                Rotation = glyphRotation,
                Kind = sensor.Kind,
                Accelerometer = _sensors[i] as AccelerometerSensor,
                CameraAim = Vector2.FromAngle((float)(sensor.Aim ?? 0)).Rotated(-glyphRotation),
            };
            _beamBodies[beamIndex].AddChild(visual);
            _sensorVisuals[i] = visual;
        }

        // On the overlay layer, over the whole creature (#623).
        _cameraRaysVisual = null;
        var cameras = _sensors.OfType<CameraSensor>().ToArray();
        if (cameras.Length > 0)
        {
            _cameraRaysVisual = new CameraRaysVisual { Name = "CameraRays", Theme = Theme, Cameras = cameras };
            AddChild(_cameraRaysVisual);
        }
    }

    private AccelerometerSensor CreateAccelerometer(CreatureDef definition, int beamIndex, double gravity)
    {
        var beam = definition.Beams[beamIndex];
        var nodeA = definition.Nodes[definition.NodeIndexOf(beam.NodeA)].Position;
        var nodeB = definition.Nodes[definition.NodeIndexOf(beam.NodeB)].Position;
        return new AccelerometerSensor(_beamBodies[beamIndex], Accelerometer.UpSign(nodeA, nodeB), gravity);
    }

    private void ConfigureBrainBuffers(CreatureDef definition)
    {
        _outputCount = 2 * _pistons.Length;
        if (_outputCount == 0)
        {
            Brain = null;
            Ports = BrainPortLayout.Empty;
            _rawInputs = [];
            _inputPortOf = [];
            _outputPortOf = [];
            _sensorValues = [];
            _outputValues = [];
            _scratchA = [];
            _scratchB = [];
            return;
        }

        Ports = BrainPorts.Of(definition);
        _inputPortOf = PortPositions(
            [
                .. definition.Sensors.SelectMany(BrainPorts.SensorPorts),
                .. definition.Pistons.SelectMany(piston => BrainPorts.PistonInputs(piston.Id)),
            ],
            Ports.Inputs);
        _outputPortOf = PortPositions(
            [
                .. definition.Pistons.SelectMany(piston => BrainPorts.PistonOutputs(piston.Id)),
            ],
            Ports.Outputs);
        _rawInputs = new double[_inputPortOf.Length];
        _sensorValues = new double[_inputPortOf.Length];
        _outputValues = new double[_outputPortOf.Length];

        var scratchSize = DirectBrain.LayerSizes(Ports).Max();
        _scratchA = new double[scratchSize];
        _scratchB = new double[scratchSize];
    }

    // Where each sim-order port sits in the brain's port order. The sim and BrainPorts must
    // declare exactly the same ports; a mismatch is a bug, not bad data.
    private static int[] PortPositions(IReadOnlyList<BrainPort> simOrder, IReadOnlyList<BrainPort> brainOrder)
    {
        var positionOf = new Dictionary<BrainPort, int>(brainOrder.Count);
        for (var i = 0; i < brainOrder.Count; i++)
        {
            positionOf.Add(brainOrder[i], i);
        }

        if (simOrder.Count != brainOrder.Count)
        {
            throw new InvalidOperationException($"The sim has {simOrder.Count} ports but BrainPorts declares {brainOrder.Count}.");
        }

        return simOrder
            .Select(port => positionOf.TryGetValue(port, out var position)
                ? position
                : throw new InvalidOperationException($"BrainPorts does not declare {port}."))
            .ToArray();
    }

    // Each sensor part writes its values (Accelerometer: along, across; Camera: left 1, centre,
    // right 1), then each Piston its length and speed, into the raw buffer in sim order;
    // they are then copied into the brain's port order (BrainPorts).
    private void ReadSensors(double[] values, double delta)
    {
        var index = 0;
        foreach (var sensor in _sensors)
        {
            sensor.Read(_rawInputs, index, delta);
            index += sensor.ValueCount;
        }

        foreach (var piston in _pistons)
        {
            _rawInputs[index++] = piston.LengthInput;
            _rawInputs[index++] = piston.SpeedInput;
        }

        for (var i = 0; i < _rawInputs.Length; i++)
        {
            values[_inputPortOf[i]] = _rawInputs[i];
        }
    }

    /// <summary>
    /// The brain's inputs as of the last physics step, in its port order (<see cref="BrainPorts"/>),
    /// for BrainFocus and the stage notes; empty while no brain drives a part. Read-only: never
    /// changes the simulation.
    /// </summary>
    public void ReadInputs(List<double> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        inputs.Clear();
        if (Brain is null || _outputCount == 0)
        {
            return;
        }

        inputs.AddRange(_sensorValues);
    }

    /// <summary>How many Pistons the creature has.</summary>
    public int PistonCount => _pistons.Length;

    private float GetHitTolerance()
    {
        var canvasTransform = GetViewport().GetCanvasTransform();
        var pixelsPerWorldUnit = Math.Max(canvasTransform.X.Length(), canvasTransform.Y.Length());
        return pixelsPerWorldUnit > 0 ? _hitTolerancePixels / pixelsPerWorldUnit : _hitTolerancePixels;
    }

    private static float DistanceSquaredToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var segmentLengthSquared = segment.LengthSquared();
        if (segmentLengthSquared <= float.Epsilon)
        {
            return point.DistanceSquaredTo(start);
        }

        var projection = Mathf.Clamp((point - start).Dot(segment) / segmentLengthSquared, 0, 1);
        return point.DistanceSquaredTo(start + (segment * projection));
    }

    private static int CreateSeed()
    {
        return Random.Shared.Next(int.MinValue, int.MaxValue);
    }

    private static Vector2 ToGodot(Vector2D value)
    {
        return new Vector2(ToGodotFloat(value.X, nameof(value.X)), ToGodotFloat(value.Y, nameof(value.Y)));
    }

    private static float ToGodotFloat(double value, string parameterName)
    {
        var converted = (float)value;
        if (!float.IsFinite(converted))
        {
            throw new ArgumentOutOfRangeException(parameterName, "Value must fit in Godot's 32-bit float physics API.");
        }

        return converted;
    }
}
