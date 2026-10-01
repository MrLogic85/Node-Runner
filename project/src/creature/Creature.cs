using Godot;
using NodeRunner.Domain;
using NodeRunner.ML;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

public partial class Creature : Node2D
{
    public const int MaximumCollisionSlots = 16;

    private const float _beamThickness = 12f;

    // Tuned empirically: a beam resting flat on the ground has both ends of
    // its bottom edge in contact, so tipping it up (the only way to rotate
    // while grounded) must overcome gravity + friction across the whole
    // beam, not just spin freely in open air. 4000 (the original guess)
    // could never lift a resting beam; 60000 reliably does across multiple
    // random creatures/seeds — see MotorRelation for the matching gain fix.
    private const float _maxMotorTorque = 60000f;
    private const float _maxAngularVelocityRadPerSec = 6f;
    private const double _relativeAngularVelocityScale = 8.0;
    private const float _lineHitTolerancePixels = 16;

    private RigidBody2D[] _beamBodies = [];
    private float[] _beamHalfLengths = [];
    private Vector2[] _beamInitialPositions = [];
    private float[] _beamInitialRotations = [];
    private NodeVisual[] _nodeVisuals = [];
    private BeamVisual[] _beamVisuals = [];
    private IBeamSensor[] _sensors = [];
    private AccelerometerSensor[] _accelerometers = [];
    private MotorRelation[] _motorRelations = [];
    private double[] _sensorValues = [];
    private double[] _motorTargets = [];
    private double[] _scratchA = [];
    private double[] _scratchB = [];
    private bool _isBuilt;

    public CreatureDef? Definition { get; set; }

    public BrainShapeDef BrainShape { get; set; } = BrainShapeDef.Default;

    public VisualTheme Theme { get; set; } = VisualTheme.Neon;

    public NeuralNetwork? Brain { get; private set; }

    public int BrainSeed { get; private set; }

    public bool IsBuilt => _isBuilt;

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
            _motorRelations[i].Drive(_motorTargets[i]);
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

        var anchorBeamPerNode = new int[definition.Nodes.Count];
        var anchorOffsetPerNode = new Vector2[definition.Nodes.Count];
        CreateBeams(definition, anchorBeamPerNode, anchorOffsetPerNode);
        DisableSelfCollisions();
        CreateNodeVisuals(definition, anchorBeamPerNode, anchorOffsetPerNode);
        CreateSensors(definition);
        CreateNodeConnections(definition);
        ConfigureBrainBuffers();
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
        Brain = new NeuralNetwork(BrainShape.ToLayerSizes(_sensorValues.Length, _motorRelations.Length), Activation.Tanh, random);

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
    /// Returns every beam body to its original built shape, rotation and
    /// zero velocity, lowered or raised so its lowest point just touches
    /// <paramref name="groundTopY"/>. A new trial therefore starts from the
    /// exact same physical state as the last, without a drop from spawn
    /// height. This is plain physical reset, not evolution/fitness logic.
    /// </summary>
    public void ResetPose(float groundTopY)
    {
        for (var i = 0; i < _beamBodies.Length; i++)
        {
            var body = _beamBodies[i];
            body.Position = _beamInitialPositions[i];
            body.Rotation = _beamInitialRotations[i];
            body.LinearVelocity = Vector2.Zero;
            body.AngularVelocity = 0f;
        }

        if (_beamBodies.Length == 0)
        {
            return;
        }

        var offset = GlobalTransform.BasisXformInv(new Vector2(0, groundTopY - LowestPointY));
        foreach (var body in _beamBodies)
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
        foreach (var body in _beamBodies)
        {
            body.CollisionLayer = slotLayer;
            body.CollisionMask = 1u | slotLayer;
        }
    }

    /// <summary>
    /// The Y of the creature's lowest physical point: the lowest corner of any
    /// beam's collision box. Godot's Y grows downward, so this is the largest Y.
    /// Returns negative infinity for a creature with no beams.
    /// </summary>
    public float LowestPointY
    {
        get
        {
            var lowest = float.NegativeInfinity;
            for (var i = 0; i < _beamBodies.Length; i++)
            {
                var transform = _beamBodies[i].GlobalTransform;
                var halfLength = _beamHalfLengths[i];
                const float halfThickness = _beamThickness / 2;
                lowest = Math.Max(lowest, (transform * new Vector2(-halfLength, -halfThickness)).Y);
                lowest = Math.Max(lowest, (transform * new Vector2(-halfLength, halfThickness)).Y);
                lowest = Math.Max(lowest, (transform * new Vector2(halfLength, -halfThickness)).Y);
                lowest = Math.Max(lowest, (transform * new Vector2(halfLength, halfThickness)).Y);
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
        }
    }

    private void ResetSensors()
    {
        foreach (var sensor in _sensors)
        {
            sensor.Reset();
        }
    }

    private void CreateBeams(CreatureDef definition, int[] anchorBeamPerNode, Vector2[] anchorOffsetPerNode)
    {
        var beamDefs = definition.Beams;
        _beamBodies = new RigidBody2D[beamDefs.Count];
        _beamHalfLengths = new float[beamDefs.Count];
        _beamInitialPositions = new Vector2[beamDefs.Count];
        _beamInitialRotations = new float[beamDefs.Count];
        _beamVisuals = new BeamVisual[beamDefs.Count];

        Array.Fill(anchorBeamPerNode, -1);

        for (var i = 0; i < beamDefs.Count; i++)
        {
            var beamDef = beamDefs[i];
            var nodeAIndex = definition.NodeIndexOf(beamDef.NodeA);
            var nodeBIndex = definition.NodeIndexOf(beamDef.NodeB);
            var nodeAPos = ToGodot(definition.Nodes[nodeAIndex].Position);
            var nodeBPos = ToGodot(definition.Nodes[nodeBIndex].Position);
            var midpoint = (nodeAPos + nodeBPos) / 2;
            var direction = nodeBPos - nodeAPos;
            var halfLength = direction.Length() / 2;
            var rotation = direction.Angle();

            var body = new RigidBody2D
            {
                Name = $"Beam{i}",
                Position = midpoint,
                Rotation = rotation,
                Mass = 1.2f,
                LinearDamp = 0.55f,
                AngularDamp = 0.55f,
                CanSleep = false,
                ContinuousCd = RigidBody2D.CcdMode.CastRay,
            };

            body.AddChild(new CollisionShape2D
            {
                Shape = new RectangleShape2D { Size = new Vector2(halfLength * 2, _beamThickness) },
            });

            var visual = new BeamVisual
            {
                Name = $"Beam{i}Visual",
                ZIndex = -1,
                HalfLength = halfLength,
                Width = Theme.BeamWidth,
                Color = Theme.Beam,
                SelectionColor = Theme.SelectionGlow,
                SelectionWidth = Theme.BeamWidth,
            };
            body.AddChild(visual);
            _beamVisuals[i] = visual;

            AddChild(body);
            _beamBodies[i] = body;
            _beamHalfLengths[i] = halfLength;
            _beamInitialPositions[i] = midpoint;
            _beamInitialRotations[i] = rotation;

            RegisterAnchor(nodeAIndex, i, new Vector2(-halfLength, 0), anchorBeamPerNode, anchorOffsetPerNode);
            RegisterAnchor(nodeBIndex, i, new Vector2(halfLength, 0), anchorBeamPerNode, anchorOffsetPerNode);
        }
    }

    private static void RegisterAnchor(int nodeIndex, int beamIndex, Vector2 localOffset, int[] anchorBeamPerNode, Vector2[] anchorOffsetPerNode)
    {
        // The lowest-indexed beam touching a node anchors its visual;
        // any incident beam works equally well since they all meet
        // at the same physical point.
        if (anchorBeamPerNode[nodeIndex] != -1 && anchorBeamPerNode[nodeIndex] < beamIndex)
        {
            return;
        }

        anchorBeamPerNode[nodeIndex] = beamIndex;
        anchorOffsetPerNode[nodeIndex] = localOffset;
    }

    private void DisableSelfCollisions()
    {
        for (var i = 0; i < _beamBodies.Length; i++)
        {
            for (var j = i + 1; j < _beamBodies.Length; j++)
            {
                _beamBodies[i].AddCollisionExceptionWith(_beamBodies[j]);
            }
        }
    }

    private void CreateNodeVisuals(CreatureDef definition, int[] anchorBeamPerNode, Vector2[] anchorOffsetPerNode)
    {
        _nodeVisuals = new NodeVisual[definition.Nodes.Count];

        for (var i = 0; i < definition.Nodes.Count; i++)
        {
            var visual = new NodeVisual
            {
                Name = $"Node{i}Visual",
                Theme = Theme,
                Position = anchorOffsetPerNode[i],
                Radius = ToGodotFloat(definition.Nodes[i].Radius, nameof(NodeDef.Radius)),
            };
            _beamBodies[anchorBeamPerNode[i]].AddChild(visual);
            _nodeVisuals[i] = visual;
        }
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
                SensorKind.LineOfSight => new LosSensor(_beamBodies[beamIndex], _beamInitialRotations[beamIndex]),
                _ => throw new InvalidOperationException($"Unknown sensor kind {sensor.Kind}."),
            };
        }

        _accelerometers = _sensors.OfType<AccelerometerSensor>().ToArray();
    }

    private AccelerometerSensor CreateAccelerometer(CreatureDef definition, int beamIndex, double gravity)
    {
        var beam = definition.Beams[beamIndex];
        var nodeA = definition.Nodes[definition.NodeIndexOf(beam.NodeA)].Position;
        var nodeB = definition.Nodes[definition.NodeIndexOf(beam.NodeB)].Position;
        return new AccelerometerSensor(_beamBodies[beamIndex], Accelerometer.UpSign(nodeA, nodeB), gravity);
    }

    private void CreateNodeConnections(CreatureDef definition)
    {
        var connections = MotorTopology.BuildNodeConnections(definition);
        var motorRelations = new List<MotorRelation>();

        foreach (var connection in connections)
        {
            var nodePosition = ToGodot(definition.Nodes[connection.NodeIndex].Position);
            var referenceBody = _beamBodies[connection.ReferenceBeamIndex];
            var otherBody = _beamBodies[connection.OtherBeamIndex];

            var pin = new PinJoint2D
            {
                Name = $"Node{connection.NodeIndex}Pin{connection.ReferenceBeamIndex}-{connection.OtherBeamIndex}",
                Position = nodePosition,
                MotorEnabled = false, // driven manually via MotorRelation.Drive so torque stays capped.
            };
            AddChild(pin);
            pin.NodeA = pin.GetPathTo(referenceBody);
            pin.NodeB = pin.GetPathTo(otherBody);

            if (connection.IsMotorized)
            {
                motorRelations.Add(new MotorRelation(referenceBody, otherBody, _maxMotorTorque, _maxAngularVelocityRadPerSec));
            }
        }

        _motorRelations = motorRelations.ToArray();
    }

    private void ConfigureBrainBuffers()
    {
        if (_motorRelations.Length == 0)
        {
            Brain = null;
            _sensorValues = [];
            _motorTargets = [];
            _scratchA = [];
            _scratchB = [];
            return;
        }

        var sensorCount = _sensors.Sum(sensor => sensor.ValueNames.Count) + (_motorRelations.Length * 2);
        _sensorValues = new double[sensorCount];
        _motorTargets = new double[_motorRelations.Length];

        var shape = BrainShape.ToLayerSizes(sensorCount, _motorRelations.Length);
        var scratchSize = shape.Max();
        _scratchA = new double[scratchSize];
        _scratchB = new double[scratchSize];
    }

    // Stable order: each sensor part's values in part order (Accelerometer: along, across;
    // LOS sensor: down, forward, forward-down), then each motor relation's
    // (relativeAngle, relativeAngularVelocity), in creation order.
    private void ReadSensors(double[] values, double delta)
    {
        var index = 0;
        foreach (var sensor in _sensors)
        {
            sensor.Read(values, index, delta);
            index += sensor.ValueNames.Count;
        }

        foreach (var relation in _motorRelations)
        {
            values[index++] = relation.RelativeAngle / Math.PI;
            values[index++] = Math.Clamp(relation.RelativeAngularVelocity / _relativeAngularVelocityScale, -1, 1);
        }
    }

    // Populates the sensor-to-brain-to-motor mapping display (issue #42)
    // from the same buffers ReadSensors/_PhysicsProcess already computed
    // this tick. Read-only telemetry: never mutates simulation state.
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
                sensors.Add(new SensorReading(sensor.GroupKind, groupIndex, sensor.ValueNames[n], _sensorValues[index++]));
            }
        }

        for (var m = 0; m < _motorRelations.Length; m++)
        {
            sensors.Add(new SensorReading("Motor relation", m + 1, "angle", _sensorValues[index++]));
            sensors.Add(new SensorReading("Motor relation", m + 1, "angular velocity", _sensorValues[index++]));
        }

        for (var m = 0; m < _motorRelations.Length; m++)
        {
            motors.Add(new MotorReading(m + 1, _motorTargets[m], _motorRelations[m].LastAppliedTorque));
        }
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
