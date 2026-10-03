using Godot;
using NodeRunner.Domain;
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

    // A beam's weight is simulated as half on each of its end nodes, so a node's
    // mass is the sum of half of every beam it joins.
    private const float _beamWeight = 1.2f;

    // The beam body itself is nearly massless (Godot needs some mass) and only
    // carries sensors between its two nodes.
    private const float _beamBodyMass = 0.1f;

    // Beams have no collider, so Godot cannot derive their turning inertia; it is
    // set as a solid bar this thick.
    private const float _beamInertiaThickness = 12f;

    // How near a tap must land to a part, on screen, at least: a part drawn smaller when the
    // Training camera zooms out (#675) still takes a finger.
    private const float _hitTolerancePixels = 16;

    // Beams draw one z step under their creature's joints and the rigid hatch two (#627), so the
    // followed creature sits three steps above the shadows: even its hatch is never drawn under a
    // shadow's joints (#385).
    private const int _followedZIndex = 3;

    private RigidBody2D[] _beamBodies = [];
    private float[] _beamHalfLengths = [];
    private Vector2[] _beamInitialPositions = [];
    private float[] _beamInitialRotations = [];
    private RigidBody2D[] _nodeBodies = [];
    private Vector2[] _nodeInitialPositions = [];
    private float[] _nodeColliderRadii = [];
    private NodeVisual[] _nodeVisuals = [];
    private BeamVisual[] _beamVisuals = [];
    private RigidHatchVisual[] _hatchVisuals = [];
    private SensorVisual[] _sensorVisuals = [];
    private PistonLink[] _pistons = [];
    private PistonVisual[] _pistonVisuals = [];
    private RigidBody2D[] _cylinderBodies = [];
    private float[] _cylinderInitialRotations = [];
    private CameraRaysVisual? _cameraRaysVisual;
    private bool _isShadow;
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
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _isBuilt = true;

        CreateBeams(definition);
        CreateRigidHatches(definition);
        CreateNodes(definition);
        PinBeamsToNodes(definition);
        CreateSensors(definition);
        CreatePistons(definition);
        ConfigureBrainBuffers(definition);
        ResetSensors();
        ApplyShadow();

        if (_outputCount > 0)
        {
            RandomizeBrain(CreateSeed());
        }
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
    /// Returns every body to its original built shape, rotation and
    /// zero velocity, lowered or raised so its lowest point is at
    /// <paramref name="lowestPointY"/>. A new trial therefore starts from the
    /// exact same physical state as the last. This is plain physical reset,
    /// not evolution/fitness logic.
    /// </summary>
    public void ResetPose(float lowestPointY)
    {
        for (var i = 0; i < _beamBodies.Length; i++)
        {
            var body = _beamBodies[i];
            body.Position = _beamInitialPositions[i];
            body.Rotation = _beamInitialRotations[i];
            body.LinearVelocity = Vector2.Zero;
            body.AngularVelocity = 0f;
        }

        for (var i = 0; i < _nodeBodies.Length; i++)
        {
            var body = _nodeBodies[i];
            body.Position = _nodeInitialPositions[i];
            body.LinearVelocity = Vector2.Zero;
        }

        for (var i = 0; i < _pistons.Length; i++)
        {
            _pistons[i].Reset();
            var cylinder = _cylinderBodies[i];
            cylinder.Position = _pistons[i].NodeA.Position;
            cylinder.Rotation = _cylinderInitialRotations[i];
            cylinder.LinearVelocity = Vector2.Zero;
            cylinder.AngularVelocity = 0f;
        }

        if (_nodeBodies.Length == 0)
        {
            return;
        }

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
    /// Piston's middle, or a sensor's picture.
    /// </summary>
    public Vector2 PartAnchor(CreatureElementSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return selection.Kind switch
        {
            CreatureElementKind.Node => _nodeVisuals[Definition!.NodeIndexOf(selection.Id)].GlobalPosition,
            CreatureElementKind.Beam => _beamBodies[Definition!.BeamIndexOf(selection.Id)].GlobalPosition,
            CreatureElementKind.Sensor => _sensorVisuals[Definition!.Sensors.ToList().FindIndex(sensor => sensor.Id == selection.Id)].GlobalPosition,
            CreatureElementKind.Piston => PistonMiddle(_pistons[Definition!.PistonIndexOf(selection.Id)]),
            _ => throw new ArgumentOutOfRangeException(nameof(selection), selection.Kind, "Not a part of a creature."),
        };

        static Vector2 PistonMiddle(PistonLink piston) => (piston.NodeA.GlobalPosition + piston.NodeB.GlobalPosition) / 2;
    }

    public void SetSelectedElement(CreatureElementSelection? selection)
    {
        foreach (var visual in _nodeVisuals)
        {
            visual.IsSelected = false;
        }

        foreach (var visual in _beamVisuals)
        {
            visual.IsSelected = false;
        }

        foreach (var visual in _sensorVisuals)
        {
            visual.IsSelected = false;
        }

        foreach (var visual in _pistonVisuals)
        {
            visual.IsSelected = false;
        }

        if (selection is null)
        {
            return;
        }

        switch (selection.Kind)
        {
            case CreatureElementKind.Node:
                _nodeVisuals[Definition!.NodeIndexOf(selection.Id)].IsSelected = true;
                break;
            case CreatureElementKind.Beam:
                _beamVisuals[Definition!.BeamIndexOf(selection.Id)].IsSelected = true;
                break;
            case CreatureElementKind.Sensor:
                _sensorVisuals[Definition!.Sensors.ToList().FindIndex(sensor => sensor.Id == selection.Id)].IsSelected = true;
                break;
            case CreatureElementKind.Piston:
                _pistonVisuals[Definition!.PistonIndexOf(selection.Id)].IsSelected = true;
                break;
        }
    }

    private Rect2 NodeBox(int index)
    {
        var radius = _nodeColliderRadii[index];
        return new Rect2(_nodeBodies[index].GlobalPosition - new Vector2(radius, radius), new Vector2(radius, radius) * 2);
    }

    private void ApplyShadow()
    {
        Modulate = Colors.White with { A = _isShadow ? Theme.ShadowAlpha : 1f };
        ZIndex = _isShadow ? 0 : _followedZIndex;
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

        if (_cameraRaysVisual is not null)
        {
            _cameraRaysVisual.IsShadow = _isShadow;
        }
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
        _beamInitialPositions = new Vector2[beamDefs.Count];
        _beamInitialRotations = new float[beamDefs.Count];
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
                ZIndex = -1,
                HalfLength = halfLength,
                RadiusA = ToGodotFloat(definition.Nodes[definition.NodeIndexOf(beamDef.NodeA)].Radius, nameof(NodeDef.Radius)),
                RadiusB = ToGodotFloat(definition.Nodes[definition.NodeIndexOf(beamDef.NodeB)].Radius, nameof(NodeDef.Radius)),
                Width = Theme.BeamWidth,
                RingWidth = Theme.JointRingWidth,
                Color = Theme.Beam,
                SelectionColor = Theme.SelectionGlow,
                SelectionOffset = Theme.SelectedBeamOffset,
                SelectionLineWidth = Theme.SelectedBeamLineWidth,
            };
            body.AddChild(visual);
            _beamVisuals[i] = visual;

            AddChild(body);
            _beamBodies[i] = body;
            _beamHalfLengths[i] = halfLength;
            _beamInitialPositions[i] = midpoint;
            _beamInitialRotations[i] = rotation;
        }
    }

    // A node is its own body with a circle collider, weighing half of each beam it joins. Its rotation is locked:
    // a free-spinning circle pinned at its centre would roll like a wheel and give
    // the creature no grip on the ground.
    private void CreateNodes(CreatureDef definition)
    {
        var count = definition.Nodes.Count;
        _nodeBodies = new RigidBody2D[count];
        _nodeInitialPositions = new Vector2[count];
        _nodeColliderRadii = new float[count];
        _nodeVisuals = new NodeVisual[count];

        var masses = new float[count];
        foreach (var beam in definition.Beams)
        {
            masses[definition.NodeIndexOf(beam.NodeA)] += _beamWeight / 2;
            masses[definition.NodeIndexOf(beam.NodeB)] += _beamWeight / 2;
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
                Mass = Math.Max(masses[i], _beamBodyMass),
                LockRotation = true,
                LinearDamp = 0.55f,
                CanSleep = false,
                ContinuousCd = RigidBody2D.CcdMode.CastRay,
            };
            body.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = radius } });

            var visual = new NodeVisual
            {
                Name = $"Node{i}Visual",
                Theme = Theme,
                Radius = radius,
            };
            body.AddChild(visual);

            AddChild(body);
            _nodeBodies[i] = body;
            _nodeInitialPositions[i] = position;
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
            Position = _nodeInitialPositions[nodeIndex],
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
            var toBeam = new Transform2D(_beamInitialRotations[beamIndex], _beamInitialPositions[beamIndex]).AffineInverse();
            var jointRadius = ToGodotFloat(definition.Nodes[triangle.NodeA].Radius, nameof(NodeDef.Radius));
            var visual = new RigidHatchVisual
            {
                Name = $"Hatch{hatches.Count}",
                ZIndex = -2,
                Color = Theme.RigidHatch,
                Lines = TriangleHatch.Lines(a, b, c, Theme.RigidHatchSpacing, jointRadius)
                    .SelectMany(line => new[] { toBeam * line.Start, toBeam * line.End })
                    .ToArray(),
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

    // Each sensor's picture rides its beam body (#576); node bodies are added later, so joints draw over sensors.
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
                Accelerometer = _sensors[i] as AccelerometerSensor,
                Camera = _sensors[i] as CameraSensor,
                CameraAim = Vector2.FromAngle((float)(sensor.Aim ?? 0)).Rotated(-glyphRotation),
            };
            _beamBodies[beamIndex].AddChild(visual);
            _sensorVisuals[i] = visual;
        }

        // Added after the joint bodies, so tree order draws the rays over the whole creature (#623).
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

    // A Piston pushes on its two node bodies; its only body is the hidden end-stop cylinder. Its
    // picture is a child of the creature one z step down, with the beams, so the joints still draw
    // over it.
    private void CreatePistons(CreatureDef definition)
    {
        _pistons = new PistonLink[definition.Pistons.Count];
        _pistonVisuals = new PistonVisual[definition.Pistons.Count];
        _cylinderBodies = new RigidBody2D[definition.Pistons.Count];
        _cylinderInitialRotations = new float[definition.Pistons.Count];
        for (var i = 0; i < _pistons.Length; i++)
        {
            var piston = definition.Pistons[i];
            var indexA = definition.NodeIndexOf(piston.NodeA);
            var indexB = definition.NodeIndexOf(piston.NodeB);
            _pistons[i] = new PistonLink(piston, _nodeBodies[indexA], _nodeBodies[indexB]);
            CreateEndStops(i);
            var visual = new PistonVisual
            {
                Name = $"Piston{i}Visual",
                ZIndex = -1,
                Theme = Theme,
                Link = _pistons[i],
                RadiusA = ToGodotFloat(definition.Nodes[indexA].Radius, nameof(NodeDef.Radius)),
                RadiusB = ToGodotFloat(definition.Nodes[indexB].Radius, nameof(NodeDef.Radius)),
            };
            AddChild(visual);
            _pistonVisuals[i] = visual;
        }
    }

    // A Piston's end stops are a hard limit, as in a real cylinder (#701): a nearly massless,
    // collider-free cylinder body turns freely on node A, and Godot's GrooveJoint2D lets node B
    // slide only along the cylinder between the stroke's two ends. The groove needs that body:
    // nodes have locked rotation, so a groove on node A would keep one world direction instead of
    // turning with the Piston. It weighs as much as the lightest node: a tenth of that let the
    // joint give three times as far past an end (#701 probe). Inside the stroke the groove
    // pushes nothing along the piston, so only the piston's own force moves it; at an end it
    // holds whatever the load.
    private void CreateEndStops(int index)
    {
        var link = _pistons[index];
        var a = link.NodeA.Position;
        var axis = (link.NodeB.Position - a).Normalized();
        var shortest = (float)Domain.Piston.ShortestLength(link.BuiltLength, link.Definition.Stroke);
        var longest = (float)Domain.Piston.LongestLength(link.BuiltLength, link.Definition.Stroke);
        var rotation = axis.Angle();

        var cylinder = new RigidBody2D
        {
            Name = $"Piston{index}Cylinder",
            CollisionLayer = 0,
            CollisionMask = 0,
            Position = a,
            Rotation = rotation,
            Mass = _beamBodyMass,
            CenterOfMassMode = RigidBody2D.CenterOfMassModeEnum.Custom,
            CenterOfMass = Vector2.Zero,
            Inertia = _beamBodyMass * _beamInertiaThickness * _beamInertiaThickness / 12,
            CanSleep = false,
        };
        AddChild(cylinder);
        _cylinderBodies[index] = cylinder;
        _cylinderInitialRotations[index] = rotation;

        var pin = new PinJoint2D { Name = $"Piston{index}CylinderPin", Position = a };
        AddChild(pin);
        pin.NodeA = pin.GetPathTo(link.NodeA);
        pin.NodeB = pin.GetPathTo(cylinder);

        // The groove runs along the joint's own +Y, so it is turned a quarter back from the axis.
        var groove = new GrooveJoint2D
        {
            Name = $"Piston{index}EndStops",
            Position = a + (axis * shortest),
            Rotation = rotation - (Mathf.Pi / 2),
            Length = longest - shortest,
            InitialOffset = (float)link.BuiltLength - shortest,
        };
        AddChild(groove);
        groove.NodeA = groove.GetPathTo(cylinder);
        groove.NodeB = groove.GetPathTo(link.NodeB);
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
            index += sensor.ValueNames.Count;
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

    // Populates the sensor-to-brain-to-motor mapping display (issue #42)
    // from the same buffers ReadSensors/_PhysicsProcess already computed
    // this tick, in the brain's port order: reading i is brain input i and
    // output j brain output j. Read-only telemetry: never mutates simulation state.
    public void ReadMapping(List<SensorReading> sensors, List<MotorReading> motors)
    {
        ArgumentNullException.ThrowIfNull(sensors);
        ArgumentNullException.ThrowIfNull(motors);

        sensors.Clear();
        motors.Clear();
        if (Brain is null || _outputCount == 0)
        {
            return;
        }

        var inputs = new SensorReading[_rawInputs.Length];
        var index = 0;
        for (var s = 0; s < _sensors.Length; s++)
        {
            var sensor = _sensors[s];
            var groupIndex = 1;
            for (var earlier = 0; earlier < s; earlier++)
            {
                if (_sensors[earlier].GroupKind == sensor.GroupKind)
                {
                    groupIndex++;
                }
            }

            for (var n = 0; n < sensor.ValueNames.Count; n++)
            {
                inputs[_inputPortOf[index]] = new SensorReading(sensor.GroupKind, groupIndex, sensor.ValueNames[n], _rawInputs[index]);
                index++;
            }
        }

        for (var p = 0; p < _pistons.Length; p++)
        {
            inputs[_inputPortOf[index]] = new SensorReading(MotorReading.PistonKind, p + 1, "length", _rawInputs[index]);
            index++;
            inputs[_inputPortOf[index]] = new SensorReading(MotorReading.PistonKind, p + 1, "speed", _rawInputs[index]);
            index++;
        }

        var outputs = new MotorReading[_outputCount];
        // A Piston's position and strength outputs both report the force it last pushed with.
        for (var p = 0; p < _pistons.Length; p++)
        {
            for (var channel = 0; channel < 2; channel++)
            {
                var output = _outputPortOf[(2 * p) + channel];
                outputs[output] = new MotorReading(MotorReading.PistonKind, p + 1, _outputValues[output], _pistons[p].LastForce);
            }
        }

        sensors.AddRange(inputs);
        motors.AddRange(outputs);
    }

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
