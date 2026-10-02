using Godot;
using NodeRunner.Domain;
using NodeRunner.ML;
using NodeRunner.ML.Brains;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

public partial class Creature : Node2D
{
    public const int MaximumCollisionSlots = 16;

    // A beam's weight is simulated as half on each of its end nodes, so a node's
    // mass is the sum of half of every beam it joins.
    private const float _beamWeight = 1.2f;

    // The beam body itself is nearly massless (Godot needs some mass) and only
    // carries motor torque and sensors between its two nodes.
    private const float _beamBodyMass = 0.1f;

    // Beams have no collider, so Godot cannot derive their turning inertia; it is
    // set as a solid bar this thick.
    private const float _beamInertiaThickness = 12f;

    // A node's collider is a hair smaller than its drawn circle, so the drawing sinks a
    // little into the ground and reads as resting on it.
    private const float _nodeColliderInset = 2f;

    // Tuned empirically: a beam resting on the ground must be tipped up against
    // gravity and friction at its nodes. 4000 (the original guess) could never
    // lift a resting beam; 60000 reliably did — see MotorRelation for the gain.
    private const float _maxMotorTorque = 60000f;
    private const float _maxAngularVelocityRadPerSec = 6f;
    private const float _lineHitTolerancePixels = 16;

    private RigidBody2D[] _beamBodies = [];
    private float[] _beamHalfLengths = [];
    private Vector2[] _beamInitialPositions = [];
    private float[] _beamInitialRotations = [];
    private RigidBody2D[] _nodeBodies = [];
    private Vector2[] _nodeInitialPositions = [];
    private float[] _nodeColliderRadii = [];
    private NodeVisual[] _nodeVisuals = [];
    private BeamVisual[] _beamVisuals = [];
    private SensorVisual[] _sensorVisuals = [];
    private IBeamSensor[] _sensors = [];
    private AccelerometerSensor[] _accelerometers = [];
    private MotorRelation[] _motorRelations = [];
    private (int NodeId, int BeamId)[] _motorJoints = [];
    private double[] _rawInputs = [];
    private int[] _inputPortOf = [];
    private int[] _outputPortOf = [];
    private double[] _sensorValues = [];
    private double[] _motorTargets = [];
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

    public override void _Ready()
    {
        if (!_isBuilt)
        {
            BuildFrom(Definition ?? HardcodedCreatureFactory.Create());
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Brain is null || _motorRelations.Length == 0)
        {
            return;
        }

        ReadSensors(_sensorValues, delta);
        Brain.Forward(_sensorValues, _motorTargets, _scratchA, _scratchB);

        for (var i = 0; i < _motorRelations.Length; i++)
        {
            _motorRelations[i].Drive(_motorTargets[_outputPortOf[i]]);
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
        CreateNodes(definition);
        DisableSelfCollisions();
        PinBeamsToNodes(definition);
        CreateSensors(definition);
        CreateMotorRelations(definition);
        ConfigureBrainBuffers(definition);
        ResetSensors();

        if (_motorRelations.Length > 0)
        {
            RandomizeBrain(CreateSeed());
        }
    }

    public void RandomizeBrain(int seed)
    {
        if (_motorRelations.Length == 0)
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
    /// must match this creature's sensor/motor counts.
    /// </summary>
    public void SetBrain(NeuralNetwork brain, int seed)
    {
        ArgumentNullException.ThrowIfNull(brain);

        var expectedInputs = _sensorValues.Length;
        var expectedOutputs = _motorRelations.Length;
        if (brain.LayerSizes[0] != expectedInputs || brain.LayerSizes[^1] != expectedOutputs)
        {
            throw new ArgumentException(
                $"Brain shape [{string.Join(',', brain.LayerSizes)}] does not match this creature's " +
                $"sensor/motor counts [{expectedInputs}, ..., {expectedOutputs}].",
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

        if (_nodeBodies.Length == 0)
        {
            return;
        }

        var offset = GlobalTransform.BasisXformInv(new Vector2(0, lowestPointY - LowestPointY));
        foreach (var body in _beamBodies.Concat(_nodeBodies))
        {
            body.Position += offset;
        }

        ResetSensors();
    }

    public void ConfigureCollisionSlot(int slot)
    {
        if (slot is < 1 or > MaximumCollisionSlots)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }

        var slotLayer = 1u << slot;
        foreach (var body in _beamBodies.Concat(_nodeBodies))
        {
            body.CollisionLayer = slotLayer;
            body.CollisionMask = 1u | slotLayer;
        }
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
        for (var nodeIndex = 0; nodeIndex < _nodeVisuals.Length; nodeIndex++)
        {
            var radius = ToGodotFloat(Definition!.Nodes[nodeIndex].Radius, nameof(NodeDef.Radius));
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
            if (SensorPicture.Contains(Definition.Sensors[sensorIndex].Kind, new Vector2D(local.X, local.Y), new Vector2D(-halfLength, 0), new Vector2D(halfLength, 0)))
            {
                selection = new CreatureElementSelection(CreatureElementKind.Sensor, Definition.Sensors[sensorIndex].Id);
                return true;
            }
        }

        var tolerance = GetLineHitTolerance();
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
                Width = Theme.BeamWidth,
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
            var colliderRadius = Math.Max(radius - _nodeColliderInset, 1f);

            var body = new RigidBody2D
            {
                Name = $"Node{i}",
                Position = position,
                Mass = Math.Max(masses[i], _beamBodyMass),
                LockRotation = true,
                LinearDamp = 0.55f,
                CanSleep = false,
                ContinuousCd = RigidBody2D.CcdMode.CastRay,
            };
            body.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = colliderRadius } });

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
            _nodeColliderRadii[i] = colliderRadius;
            _nodeVisuals[i] = visual;
        }
    }

    private void DisableSelfCollisions()
    {
        var bodies = _beamBodies.Concat(_nodeBodies).ToArray();
        for (var i = 0; i < bodies.Length; i++)
        {
            for (var j = i + 1; j < bodies.Length; j++)
            {
                bodies[i].AddCollisionExceptionWith(bodies[j]);
            }
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
        var cameras = _sensors.OfType<CameraSensor>().ToArray();
        if (cameras.Length > 0)
        {
            AddChild(new CameraRaysVisual { Name = "CameraRays", Theme = Theme, Cameras = cameras });
        }
    }

    private AccelerometerSensor CreateAccelerometer(CreatureDef definition, int beamIndex, double gravity)
    {
        var beam = definition.Beams[beamIndex];
        var nodeA = definition.Nodes[definition.NodeIndexOf(beam.NodeA)].Position;
        var nodeB = definition.Nodes[definition.NodeIndexOf(beam.NodeB)].Position;
        return new AccelerometerSensor(_beamBodies[beamIndex], Accelerometer.UpSign(nodeA, nodeB), gravity);
    }

    private void CreateMotorRelations(CreatureDef definition)
    {
        var connections = MotorTopology.BuildNodeConnections(definition);
        var motorRelations = new List<MotorRelation>();
        var motorJoints = new List<(int NodeId, int BeamId)>();

        foreach (var connection in connections.Where(connection => connection.IsMotorized))
        {
            var referenceBody = _beamBodies[connection.ReferenceBeamIndex];
            var otherBody = _beamBodies[connection.OtherBeamIndex];
            motorRelations.Add(new MotorRelation(referenceBody, otherBody, _maxMotorTorque, _maxAngularVelocityRadPerSec));
            motorJoints.Add((definition.Nodes[connection.NodeIndex].Id, definition.Beams[connection.OtherBeamIndex].Id));
        }

        _motorRelations = motorRelations.ToArray();
        _motorJoints = motorJoints.ToArray();
    }

    private void ConfigureBrainBuffers(CreatureDef definition)
    {
        if (_motorRelations.Length == 0)
        {
            Brain = null;
            Ports = BrainPortLayout.Empty;
            _rawInputs = [];
            _inputPortOf = [];
            _outputPortOf = [];
            _sensorValues = [];
            _motorTargets = [];
            _scratchA = [];
            _scratchB = [];
            return;
        }

        Ports = BrainPorts.Of(definition);
        _inputPortOf = PortPositions(
            [
                .. definition.Sensors.SelectMany(BrainPorts.SensorPorts),
                .. _motorJoints.SelectMany(joint => BrainPorts.JointMotorInputs(joint.NodeId, joint.BeamId)),
            ],
            Ports.Inputs);
        _outputPortOf = PortPositions(
            _motorJoints.Select(joint => BrainPorts.JointMotorOutput(joint.NodeId, joint.BeamId)).ToArray(),
            Ports.Outputs);
        _rawInputs = new double[_inputPortOf.Length];
        _sensorValues = new double[_inputPortOf.Length];
        _motorTargets = new double[_outputPortOf.Length];

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
    // right 1), then each motor relation its angle and speed, into the raw buffer in sim order;
    // they are then copied into the brain's port order (BrainPorts).
    private void ReadSensors(double[] values, double delta)
    {
        var index = 0;
        foreach (var sensor in _sensors)
        {
            sensor.Read(_rawInputs, index, delta);
            index += sensor.ValueNames.Count;
        }

        foreach (var relation in _motorRelations)
        {
            _rawInputs[index++] = relation.AngleInput;
            _rawInputs[index++] = relation.SpeedInput;
        }

        for (var i = 0; i < _rawInputs.Length; i++)
        {
            values[_inputPortOf[i]] = _rawInputs[i];
        }
    }

    // Populates the sensor-to-brain-to-motor mapping display (issue #42)
    // from the same buffers ReadSensors/_PhysicsProcess already computed
    // this tick, in the brain's port order: reading i is brain input i and
    // motor j brain output j. Read-only telemetry: never mutates simulation state.
    public void ReadMapping(List<SensorReading> sensors, List<MotorReading> motors)
    {
        ArgumentNullException.ThrowIfNull(sensors);
        ArgumentNullException.ThrowIfNull(motors);

        sensors.Clear();
        motors.Clear();
        if (Brain is null || _motorRelations.Length == 0)
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

        for (var m = 0; m < _motorRelations.Length; m++)
        {
            inputs[_inputPortOf[index]] = new SensorReading("Motor relation", m + 1, "angle", _rawInputs[index]);
            index++;
            inputs[_inputPortOf[index]] = new SensorReading("Motor relation", m + 1, "angular velocity", _rawInputs[index]);
            index++;
        }

        var outputs = new MotorReading[_motorRelations.Length];
        for (var m = 0; m < _motorRelations.Length; m++)
        {
            outputs[_outputPortOf[m]] = new MotorReading(m + 1, _motorTargets[_outputPortOf[m]], _motorRelations[m].LastAppliedTorque);
        }

        sensors.AddRange(inputs);
        motors.AddRange(outputs);
    }

    private float GetLineHitTolerance()
    {
        var canvasTransform = GetViewport().GetCanvasTransform();
        var pixelsPerWorldUnit = Math.Max(canvasTransform.X.Length(), canvasTransform.Y.Length());
        return pixelsPerWorldUnit > 0 ? _lineHitTolerancePixels / pixelsPerWorldUnit : _lineHitTolerancePixels;
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
