using Godot;
using NodeRunner.Domain;
using NodeRunner.Mechanics;
using NodeRunner.ML;
using NodeRunner.ML.Networks;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

public partial class Creature : Node2D
{
    /// <summary>The most shadows a generation runs at once.</summary>
    public const int MaximumShadows = TrainSettingsDef.MaxShadows;

    // Ground is layer 1. Every creature body, in every shadow, shares layer 2 and masks only the
    // ground, so shadows never touch each other or their own parts.
    private const uint _groundLayer = 1u;
    private const uint _creatureLayer = 1u << 1;

    // A Piston's or Spring's weight is simulated as half on each of its end nodes. A beam's is split
    // half on its own body and a quarter on each end node, so a creature weighs the same.
    private const float _beamWeight = 1.2f;

    // A Piston's or Spring's end-stop cylinder weighs half a beam on top, so Godot's
    // joints can hold it on its node A.
    private const float _cylinderMass = _beamWeight / 2;

    // The beam body carries half its beam's weight: Godot's solver cannot hold a nearly
    // massless body pinned between heavy nodes, so under Piston load its pins give way and rigid
    // triangles fold inside out.
    private const float _beamBodyMass = _beamWeight / 2;

    // Godot needs some mass on every body; a node joined to nothing still weighs this.
    private const float _minNodeMass = 0.1f;

    // An intact beam has no collider, so Godot cannot derive its turning inertia; it is
    // set as a solid bar this thick. Mass, centre of mass and inertia are all set by hand,
    // so the shape a torn beam gets (Creature.Tears.cs) changes none of them.
    private const float _beamInertiaThickness = 12f;

    // How near a tap must land to a part, on screen, at least: a part drawn smaller when the
    // Training camera zooms out still takes a finger.
    private const float _hitTolerancePixels = 16;

    private RigidBody2D[] _beamBodies = [];
    private float[] _beamHalfLengths = [];
    private RigidBody2D[] _nodeBodies = [];

    private float[] _nodeColliderRadii = [];
    private NodeVisual[] _nodeVisuals = [];
    private BeamVisual[] _beamVisuals = [];
    private KnockoutVisual[] _beamKnockouts = [];
    private RigidHatchVisual[] _hatchVisuals = [];
    private SensorVisual[] _sensorVisuals = [];
    private ServoJoint[] _servos = [];
    private ServoVisual[] _servoVisuals = [];
    private VelocityMotorJoint[] _velocityMotors = [];
    private VelocityMotorVisual[] _velocityMotorVisuals = [];
    private BrakeJoint[] _brakes = [];
    private BrakeVisual[] _brakeVisuals = [];
    private StepperVisual[] _stepperVisuals = [];
    private AngleSensorJoint[] _angleSensors = [];
    private AngleSensorVisual[] _angleSensorVisuals = [];
    private StepperJoint[] _steppers = [];
    private WheelVisual[] _wheelVisuals = [];
    private PistonLink[] _pistons = [];
    private PistonVisual[] _pistonVisuals = [];
    private SpringVisual[] _springVisuals = [];
    private DrawGroups? _drawGroups;
    private RaisedParts _raised = RaisedParts.None;
    private EndStops[] _pistonEndStops = [];
    private EndStops[] _springEndStops = [];
    private SpringLink[] _springs = [];
    private CameraRaysVisual? _cameraRaysVisual;
    private readonly List<KnockoutVisual> _knockoutVisuals = [];
    private bool _isShadow;
    private IReadOnlyList<CreatureElementSelection> _selection = [];
    private IBeamSensor[] _sensors = [];
    private TouchSensor[] _touchSensors = [];
    private NodeVisual[] _touchVisuals = [];
    private double[] _rawInputs = [];
    private int[] _inputPortOf = [];
    private int[] _outputPortOf = [];
    private double[] _sensorValues = [];
    private double[] _outputValues = [];
    private double[] _scratchA = [];
    private double[] _scratchB = [];
    private bool _isBuilt;

    public CreatureDef? Definition { get; set; }

    public VisualTheme Theme { get; set; } = VisualTheme.Neon;

    /// <summary>The network driving the creature; <c>null</c> until training or Simulate sets one.</summary>
    public NeuralNetwork? Network { get; private set; }

    /// <summary>The network's ports in runtime order; empty when the creature has no network.</summary>
    public NetworkPortLayout Ports { get; private set; } = NetworkPortLayout.Empty;

    /// <summary>
    /// True for every shadow except the followed one: each visual draws as it declares in
    /// <see cref="IShadowVisual.AsShadow"/>, and the creature is drawn with the other shadows behind
    /// the followed creature, all faded together to the theme's shadow alpha. The followed
    /// creature has a knock-out outline in the arena's background colour instead.
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
        CheckTears();

        foreach (var spring in _springs)
        {
            spring.Step(delta);
        }

        SenseTouches();

        foreach (var brake in _brakes)
        {
            brake.Hold(delta);
        }

        // Its arc counts the turns too (AngleSensorVisual), network or not.
        foreach (var angleSensor in _angleSensors)
        {
            angleSensor.Sides.UpdatePhysicsState();
        }

        foreach (var stepper in _steppers)
        {
            stepper.Sides.UpdatePhysicsState();
        }

        if (Network is null)
        {
            // A Stepper holds its notch with or without a network, as a Brake holds.
            foreach (var stepper in _steppers)
            {
                stepper.Hold(delta);
            }

            return;
        }

        foreach (var servo in _servos)
        {
            servo.Sides.UpdatePhysicsState();
        }

        foreach (var motor in _velocityMotors)
        {
            motor.Sides.UpdatePhysicsState();
        }

        ReadSensors(_sensorValues, delta);
        Network.Forward(_sensorValues, _outputValues, _scratchA, _scratchB);

        // Each Servo, Velocity motor and Piston has two outputs in sim order: target or speed, then strength;
        // each Stepper, last, its frequency.
        var output = 0;
        foreach (var servo in _servos)
        {
            servo.Drive(_outputValues[_outputPortOf[output]], _outputValues[_outputPortOf[output + 1]], delta);
            output += 2;
        }

        foreach (var motor in _velocityMotors)
        {
            motor.Drive(_outputValues[_outputPortOf[output]], _outputValues[_outputPortOf[output + 1]], delta);
            output += 2;
        }

        foreach (var piston in _pistons)
        {
            piston.Drive(_outputValues[_outputPortOf[output]], _outputValues[_outputPortOf[output + 1]], delta);
            output += 2;
        }

        foreach (var stepper in _steppers)
        {
            stepper.Drive(_outputValues[_outputPortOf[output]], delta);
            output++;
        }
    }

    public void BuildFrom(CreatureDef definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.HingeParts.FirstOrDefault(part => part.FixedLinkId is null || part.TargetLinkId is null) is { } incomplete)
        {
            throw new InvalidOperationException($"{incomplete.Kind} {incomplete.Id} must have two sides before the creature is built for simulation.");
        }

        Definition = definition;
        _selection = [];
        Build(definition);
        ResetSensors();
        Network = null;
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
        _bodiesReadTick = ulong.MaxValue;
        _knockoutVisuals.Clear();

        CreateBeams(definition);
        CreateRigidHatches(definition);
        CreateNodes(definition);
        PinBeamsToNodes(definition);
        ResetTears(definition);
        CreateSensors(definition);
        CreateServos(definition);
        CreateVelocityMotors(definition);
        CreatePistons(definition);
        CreateSprings(definition);
        CreateBrakes(definition);
        CreateAngleSensors(definition);
        CreateSteppers(definition);
        CapEndStops(definition);
        _drawGroups = DrawGroups.Of(definition.Nodes, LinkRef.All(definition.Beams, definition.Pistons, definition.Springs));
        ConfigureNetworkBuffers(definition);
        ShowInEitherArenaView(this);
        ApplyShadow();
    }

    // Whether the arena's viewport or the shadows' draws this creature is up to its root
    // (ApplyShadow), so every item under it is on both, as is the world above it.
    private static void ShowInEitherArenaView(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is CanvasItem item)
            {
                item.VisibilityLayer = ArenaVisibility.Both;
            }

            ShowInEitherArenaView(child);
        }
    }

    private KnockoutVisual CreateKnockout(Vector2 a, Vector2 b, float radius)
    {
        var visual = new KnockoutVisual { Theme = Theme, A = a, B = b, Radius = radius };
        _knockoutVisuals.Add(visual);
        return visual;
    }

    /// <summary>
    /// Assigns the network to drive the creature. The network's layer sizes must match this
    /// creature's network ports.
    /// </summary>
    public void SetNetwork(NeuralNetwork network)
    {
        ArgumentNullException.ThrowIfNull(network);

        var expectedInputs = _sensorValues.Length;
        var expectedOutputs = _outputValues.Length;
        if (network.LayerSizes[0] != expectedInputs || network.LayerSizes[^1] != expectedOutputs)
        {
            throw new ArgumentException(
                $"Network shape [{string.Join(',', network.LayerSizes)}] does not match this creature's " +
                $"input/output counts [{expectedInputs}, ..., {expectedOutputs}].",
                nameof(network));
        }

        Network = network;
    }

    /// <summary>
    /// Starts the creature over in its built shape at rest, lowered or raised so its lowest point
    /// is at <paramref name="lowestPointY"/>. Every body and joint is built afresh: Godot's
    /// solver remembers the last pushes of joints and contacts and applies them again on the next
    /// step, so bodies merely moved back would start each run a little differently, and a
    /// creature's motion is chaotic enough that the same network then ends somewhere else. The
    /// network, the selection and the shadow look are kept.
    /// </summary>
    public void ResetPose(float lowestPointY)
    {
        if (Definition is null || _nodeBodies.Length == 0)
        {
            return;
        }

        Build(Definition);

        var offset = GlobalTransform.BasisXformInv(new Vector2(0, lowestPointY - LowestPointY));
        foreach (var body in _beamBodies.Concat(_nodeBodies).Concat(_pistonEndStops.Concat(_springEndStops).Select(stops => stops.Cylinder)))
        {
            body.Position += offset;
        }

        _bodiesReadTick = ulong.MaxValue;
        ResetSensors();
    }

    private void ApplyShadow()
    {
        VisibilityLayer = _isShadow ? ArenaVisibility.Shadows : ArenaVisibility.Arena;
        ZIndex = _isShadow ? ArenaLayers.Shadows : ArenaLayers.Followed;
        foreach (var pulse in _tearPulses.Where(IsInstanceValid))
        {
            pulse.IsShadow = _isShadow;
        }

        foreach (var visual in _knockoutVisuals)
        {
            visual.IsShadow = _isShadow;
        }

        foreach (var visual in _nodeVisuals)
        {
            visual.IsShadow = _isShadow;
        }

        foreach (var visual in _beamVisuals)
        {
            visual.IsShadow = _isShadow;
            visual.FlashA &= !_isShadow;
            visual.FlashB &= !_isShadow;
        }

        foreach (var visual in _hatchVisuals)
        {
            visual.IsShadow = _isShadow;
        }

        foreach (var visual in _sensorVisuals)
        {
            visual.IsShadow = _isShadow;
        }

        foreach (var visual in _servoVisuals)
        {
            visual.IsShadow = _isShadow;
        }

        foreach (var visual in _velocityMotorVisuals)
        {
            visual.IsShadow = _isShadow;
        }

        foreach (var visual in _brakeVisuals)
        {
            visual.IsShadow = _isShadow;
        }

        foreach (var visual in _stepperVisuals)
        {
            visual.IsShadow = _isShadow;
        }

        foreach (var visual in _angleSensorVisuals)
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

        foreach (var visual in _wheelVisuals)
        {
            visual.IsShadow = _isShadow;
        }

        if (_cameraRaysVisual is not null)
        {
            _cameraRaysVisual.IsShadow = _isShadow;
        }

        ApplySelection();
    }

    // Every body senses the ground; the followed creature's cushions show its readings, a shadow draws none.
    private void SenseTouches()
    {
        for (var i = 0; i < _touchSensors.Length; i++)
        {
            _touchSensors[i].Sense();
            if (!_isShadow)
            {
                _touchVisuals[i].Touch = (float)_touchSensors[i].Reading;
            }
        }
    }

    private void ResetSensors()
    {
        foreach (var sensor in _sensors)
        {
            sensor.Reset();
        }

        foreach (var touch in _touchSensors)
        {
            touch.Reset();
        }
    }

    // An intact beam is a body without a collider: it carries mass, turning inertia and sensors,
    // and is pinned at both ends to its nodes, which are what touch the world. A torn beam gets
    // a shape to land (.claude/rules/creature.md rule 17).
    private void CreateBeams(CreatureDef definition)
    {
        var beamDefs = definition.Beams;
        _beamBodies = new RigidBody2D[beamDefs.Count];
        _beamHalfLengths = new float[beamDefs.Count];
        _beamVisuals = new BeamVisual[beamDefs.Count];
        _beamKnockouts = new KnockoutVisual[beamDefs.Count];

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
                CanSleep = false,
            };

            var visual = new BeamVisual
            {
                Name = $"Beam{i}Visual",
                Theme = Theme,
                A = new Vector2(-halfLength, 0),
                B = new Vector2(halfLength, 0),
                RadiusA = ToGodotFloat(definition.NodeRadius(beamDef.NodeA), nameof(JointMotor.Radius)),
                RadiusB = ToGodotFloat(definition.NodeRadius(beamDef.NodeB), nameof(JointMotor.Radius)),
            };
            var knockout = CreateKnockout(visual.A, visual.B, Theme.BeamWidth / 2);
            body.AddChild(knockout);
            body.AddChild(visual);
            _beamVisuals[i] = visual;
            _beamKnockouts[i] = knockout;

            AddChild(body);
            _beamBodies[i] = body;
            _beamHalfLengths[i] = halfLength;
        }
    }

    // A node is its own body with a circle collider, weighing a quarter of each beam and half of each
    // link it joins (see _beamWeight). Its rotation is locked:
    // a free-spinning circle pinned at its centre would roll like a wheel and give
    // the creature no grip on the ground. A joint with a Wheel is that wheel on purpose (MakeWheel).
    private void CreateNodes(CreatureDef definition)
    {
        var count = definition.Nodes.Count;
        _nodeBodies = new RigidBody2D[count];
        _nodeColliderRadii = new float[count];
        _nodeVisuals = new NodeVisual[count];
        _wheelVisuals = new WheelVisual[definition.Wheels.Count];

        var masses = new float[count];
        foreach (var beam in definition.Beams)
        {
            masses[definition.NodeIndexOf(beam.NodeA)] += _beamWeight / 4;
            masses[definition.NodeIndexOf(beam.NodeB)] += _beamWeight / 4;
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

        foreach (var wheel in definition.Wheels)
        {
            masses[definition.NodeIndexOf(wheel.NodeId)] += ToGodotFloat(Wheel.Mass(wheel), nameof(Wheel.Mass));
        }

        for (var i = 0; i < count; i++)
        {
            var position = ToGodot(definition.Nodes[i].Position);
            var radius = ToGodotFloat(definition.NodeRadius(definition.Nodes[i].Id), nameof(JointMotor.Radius));

            // No CCD (.claude/rules/creature.md rule 3).
            var body = new RigidBody2D
            {
                Name = $"Node{i}",
                CollisionLayer = _creatureLayer,
                CollisionMask = _groundLayer,
                Position = position,
                Mass = Math.Max(masses[i], _minNodeMass),
                LockRotation = true,
                CanSleep = false,
            };
            body.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = radius } });

            var hasHingePart = definition.HingeParts.Any(part => part.NodeId == definition.Nodes[i].Id);
            var hasWheel = definition.Wheels.Any(wheel => wheel.NodeId == definition.Nodes[i].Id);
            var hasTouch = definition.TouchSensors.Any(touch => touch.NodeId == definition.Nodes[i].Id);
            var hasRubber = definition.Rubbers.Any(rubber => rubber.NodeId == definition.Nodes[i].Id);

            var visual = new NodeVisual
            {
                Name = $"Node{i}Visual",
                Theme = Theme,
                Radius = radius,
                ShowsRing = !hasWheel && !hasHingePart,
                ShowsHub = hasWheel && !hasHingePart,
                Cushion = hasTouch,
                Skin = hasRubber,
            };

            // The knock-out runs past a cushion or skin, so it breaks the ground edge round a touching foot.
            var knockout = hasTouch ? radius + (float)TouchSensorDef.Cushion : hasRubber ? radius + (float)RubberDef.Skin : radius;
            body.AddChild(CreateKnockout(Vector2.Zero, Vector2.Zero, knockout));
            body.AddChild(visual);
            if (definition.Wheels.FirstOrDefault(wheel => wheel.NodeId == definition.Nodes[i].Id) is { } wheel)
            {
                _wheelVisuals[definition.WheelIndexOf(wheel.Id)] = MakeWheel(body, wheel, radius);
            }

            if (hasRubber)
            {
                MakeRubber(body);
            }

            AddChild(body);
            _nodeBodies[i] = body;
            _nodeColliderRadii[i] = radius;
            _nodeVisuals[i] = visual;
        }
    }

    private void PinBeamsToNodes(CreatureDef definition)
    {
        _beamPins = new PinJoint2D[2 * definition.Beams.Count];
        for (var i = 0; i < definition.Beams.Count; i++)
        {
            var beamDef = definition.Beams[i];
            _beamPins[2 * i] = PinBeamToNode(i, definition.NodeIndexOf(beamDef.NodeA));
            _beamPins[(2 * i) + 1] = PinBeamToNode(i, definition.NodeIndexOf(beamDef.NodeB));
        }
    }

    private PinJoint2D PinBeamToNode(int beamIndex, int nodeIndex)
    {
        var pin = new PinJoint2D
        {
            Name = $"Beam{beamIndex}Node{nodeIndex}Pin",
            Position = _nodeBodies[nodeIndex].Position,
        };
        AddChild(pin);
        pin.NodeA = pin.GetPathTo(_nodeBodies[nodeIndex]);
        pin.NodeB = pin.GetPathTo(_beamBodies[beamIndex]);
        return pin;
    }

    // Each rigid triangle's hatch rides on one of its beams. The lines are laid out in the
    // creature's built space, as in Build, so the hatches of triangles that share a beam line up.
    // Each keeps its three beams, as a tear of any hides it (Creature.Tears).
    private void CreateRigidHatches(CreatureDef definition)
    {
        var hatches = new List<RigidHatchVisual>();
        var hatchBeams = new List<int>();
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
            var visual = new RigidHatchVisual
            {
                Name = $"Hatch{hatches.Count}",
                Theme = Theme,
                Lines = TriangleHatch.Lines(a, b, c, Theme.RigidHatchSpacing)
                    .SelectMany(line => new[] { toBeam * line.Start, toBeam * line.End })
                    .ToArray(),
                Triangles = [toBeam * a, toBeam * b, toBeam * c],
            };
            _beamBodies[beamIndex].AddChild(visual);
            hatches.Add(visual);
            hatchBeams.AddRange([beamIndex, BeamIndexBetween(definition, triangle.NodeB, triangle.NodeC), BeamIndexBetween(definition, triangle.NodeA, triangle.NodeC)]);
        }

        _hatchVisuals = [.. hatches];
        _hatchBeams = [.. hatchBeams];
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
        _sensorBeams = new int[definition.Sensors.Count];

        for (var i = 0; i < _sensors.Length; i++)
        {
            var sensor = definition.Sensors[i];
            var beamIndex = definition.BeamIndexOf(sensor.BeamId);
            _sensorBeams[i] = beamIndex;
            _sensors[i] = sensor.Kind switch
            {
                SensorKind.Accelerometer => CreateAccelerometer(definition, beamIndex, gravity),
                SensorKind.Camera => new CameraSensor(_beamBodies[beamIndex], sensor, SensorOnBeam(definition, beamIndex)),
                _ => throw new InvalidOperationException($"Unknown sensor kind {sensor.Kind}."),
            };
        }

        CreateSensorVisuals(definition);
        _touchSensors = definition.TouchSensors
            .Select(touch => new TouchSensor(_nodeBodies[definition.NodeIndexOf(touch.NodeId)], _nodeColliderRadii[definition.NodeIndexOf(touch.NodeId)]))
            .ToArray();
        _touchVisuals = definition.TouchSensors
            .Select(touch => _nodeVisuals[definition.NodeIndexOf(touch.NodeId)])
            .ToArray();
    }

    // Each sensor's picture rides its beam body.
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
                Position = SensorOnBeam(definition, beamIndex),
                Rotation = glyphRotation,
                Kind = sensor.Kind,
                Accelerometer = _sensors[i] as AccelerometerSensor,
                CameraAim = Vector2.FromAngle((float)(sensor.Aim ?? 0)).Rotated(-glyphRotation),
            };
            _beamBodies[beamIndex].AddChild(visual);
            _sensorVisuals[i] = visual;
        }

        // On the overlay layer, over the whole creature.
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
        return new AccelerometerSensor(_beamBodies[beamIndex], SensorOnBeam(definition, beamIndex), Accelerometer.UpSign(nodeA, nodeB), gravity);
    }

    // Where a sensor sits on its beam's body, whose +x runs from node A to node B: see SensorPicture.Shift.
    private static Vector2 SensorOnBeam(CreatureDef definition, int beamIndex)
    {
        var beam = definition.Beams[beamIndex];
        var shift = SensorPicture.Shift(definition.CoveredRadius(beam.NodeA), definition.CoveredRadius(beam.NodeB));
        return new Vector2(ToGodotFloat(shift, nameof(SensorPicture.Shift)), 0);
    }

    private void ConfigureNetworkBuffers(CreatureDef definition)
    {
        // A creature with nothing to drive still has its senses: its network runs with no outputs.
        Ports = NetworkPorts.Of(definition);
        _inputPortOf = PortPositions(
            [
                .. definition.Sensors.SelectMany(NetworkPorts.SensorPorts),
                .. definition.TouchSensors.Select(touch => NetworkPorts.TouchInput(touch.Id)),
                .. definition.AngleSensors.SelectMany(angleSensor => NetworkPorts.AngleSensorInputs(angleSensor.Id)),
                .. definition.Servos.SelectMany(servo => NetworkPorts.ServoInputs(servo.Id)),
                .. definition.VelocityMotors.SelectMany(motor => NetworkPorts.VelocityMotorInputs(motor.Id)),
                .. definition.Pistons.SelectMany(piston => NetworkPorts.PistonInputs(piston.Id)),
                .. definition.Steppers.SelectMany(stepper => NetworkPorts.StepperInputs(stepper.Id)),
            ],
            Ports.Inputs);
        _outputPortOf = PortPositions(
            [
                .. definition.Servos.SelectMany(servo => NetworkPorts.ServoOutputs(servo.Id)),
                .. definition.VelocityMotors.SelectMany(motor => NetworkPorts.VelocityMotorOutputs(motor.Id)),
                .. definition.Pistons.SelectMany(piston => NetworkPorts.PistonOutputs(piston.Id)),
                .. definition.Steppers.SelectMany(stepper => NetworkPorts.StepperOutputs(stepper.Id)),
            ],
            Ports.Outputs);
        _rawInputs = new double[_inputPortOf.Length];
        _sensorValues = new double[_inputPortOf.Length];
        _outputValues = new double[_outputPortOf.Length];

        var scratchSize = DirectNetwork.LayerSizes(Ports).Max();
        _scratchA = new double[scratchSize];
        _scratchB = new double[scratchSize];
    }

    // Where each sim-order port sits in the network's port order. The sim and NetworkPorts must
    // declare exactly the same ports; a mismatch is a bug, not bad data.
    private static int[] PortPositions(IReadOnlyList<NetworkPort> simOrder, IReadOnlyList<NetworkPort> networkOrder)
    {
        var positionOf = new Dictionary<NetworkPort, int>(networkOrder.Count);
        for (var i = 0; i < networkOrder.Count; i++)
        {
            positionOf.Add(networkOrder[i], i);
        }

        if (simOrder.Count != networkOrder.Count)
        {
            throw new InvalidOperationException($"The sim has {simOrder.Count} ports but NetworkPorts declares {networkOrder.Count}.");
        }

        return simOrder
            .Select(port => positionOf.TryGetValue(port, out var position)
                ? position
                : throw new InvalidOperationException($"NetworkPorts does not declare {port}."))
            .ToArray();
    }

    // Each sensor part writes its values (Accelerometer: along, across; Camera: one per ray from
    // left to right, then hit), then each Touch sensor its touch, each Angle sensor its angle's sine and
    // cosine and its speed, each Servo its angle and speed,
    // each Velocity motor its angle's sine and cosine and its speed, each Piston its length and speed, and each Stepper its angle's
    // sine and cosine and its tick, into the raw buffer in sim order; they are then copied into the network's port order (NetworkPorts).
    // After a tear a sensor on a beam gone from the world and a passive part write nothing, so
    // their inputs keep the values they last had.
    private void ReadSensors(double[] values, double delta)
    {
        var index = 0;
        for (var i = 0; i < _sensors.Length; i++)
        {
            var sensor = _sensors[i];
            if (!_beamGone[_sensorBeams[i]])
            {
                sensor.Read(_rawInputs, index, delta);
            }

            index += sensor.ValueCount;
        }

        foreach (var touch in _touchSensors)
        {
            _rawInputs[index++] = touch.Reading;
        }

        foreach (var angleSensor in _angleSensors)
        {
            if (angleSensor.IsPassive)
            {
                index += 3;
                continue;
            }

            var (sin, cos) = angleSensor.AngleInputs;
            _rawInputs[index++] = sin;
            _rawInputs[index++] = cos;
            _rawInputs[index++] = angleSensor.SpeedInput;
        }

        foreach (var servo in _servos)
        {
            if (servo.IsPassive)
            {
                index += 2;
                continue;
            }

            _rawInputs[index++] = servo.AngleInput;
            _rawInputs[index++] = servo.SpeedInput;
        }

        foreach (var motor in _velocityMotors)
        {
            if (motor.IsPassive)
            {
                index += 3;
                continue;
            }

            var (sin, cos) = motor.AngleInputs;
            _rawInputs[index++] = sin;
            _rawInputs[index++] = cos;
            _rawInputs[index++] = motor.SpeedInput;
        }

        foreach (var piston in _pistons)
        {
            _rawInputs[index++] = piston.LengthInput;
            _rawInputs[index++] = piston.SpeedInput;
        }

        foreach (var stepper in _steppers)
        {
            if (stepper.IsPassive)
            {
                index += 3;
                continue;
            }

            var (sin, cos) = stepper.AngleInputs;
            _rawInputs[index++] = sin;
            _rawInputs[index++] = cos;
            _rawInputs[index++] = stepper.TickInput;
        }

        for (var i = 0; i < _rawInputs.Length; i++)
        {
            values[_inputPortOf[i]] = _rawInputs[i];
        }
    }

    /// <summary>
    /// The network's inputs as of the last physics step, in its port order (<see cref="NetworkPorts"/>);
    /// empty while no network drives a part. Read-only: never changes the simulation.
    /// </summary>
    public void ReadInputs(List<double> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        inputs.Clear();
        if (Network is null)
        {
            return;
        }

        inputs.AddRange(_sensorValues);
    }

    /// <summary>How many powered outputs the creature has.</summary>
    public int MotorCount => _pistons.Length + _servos.Length + _velocityMotors.Length + _steppers.Length;

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
