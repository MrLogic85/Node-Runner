using Godot;
using NodeRunner.Domain;
using NodeRunner.ML;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

public partial class Creature : Node2D
{
    private const int _hiddenNeuronCount = 8;
    private const float _beamThickness = 12f;
    private const float _rayLength = 220f;

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
    private CoreSensors[] _coreSensors = [];
    private int[] _coreIndexByNode = [];
    private MotorRelation[] _motorRelations = [];
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

        ReadSensors(_sensorValues);
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
        CreateCores(definition, anchorBeamPerNode, anchorOffsetPerNode);
        CreateNodeConnections(definition);
        ConfigureBrainBuffers();

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
        Brain = new NeuralNetwork(new[] { _sensorValues.Length, _hiddenNeuronCount, _motorRelations.Length }, Activation.Tanh, random);

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
    /// Returns every beam body to its original built position/rotation and
    /// zeroes its velocity, so a new trial starts from the exact same
    /// physical state as the last. This is plain physical reset, not
    /// evolution/fitness logic.
    /// </summary>
    public void ResetPose()
    {
        for (var i = 0; i < _beamBodies.Length; i++)
        {
            var body = _beamBodies[i];
            body.Position = _beamInitialPositions[i];
            body.Rotation = _beamInitialRotations[i];
            body.LinearVelocity = Vector2.Zero;
            body.AngularVelocity = 0f;
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
                selection = _coreIndexByNode[nodeIndex] >= 0
                    ? new CreatureElementSelection(CreatureElementKind.Core, _coreIndexByNode[nodeIndex])
                    : new CreatureElementSelection(CreatureElementKind.Node, nodeIndex);
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
                selection = new CreatureElementSelection(CreatureElementKind.Beam, beamIndex);
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
            case CreatureElementKind.Node when selection.Index < _nodeVisuals.Length:
                _nodeVisuals[selection.Index].IsSelected = true;
                break;
            case CreatureElementKind.Beam when selection.Index < _beamVisuals.Length:
                _beamVisuals[selection.Index].IsSelected = true;
                break;
            case CreatureElementKind.Core when selection.Index < Definition!.Cores.Count:
                _nodeVisuals[Definition.Cores[selection.Index].NodeIndex].IsSelected = true;
                break;
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
            var nodeAPos = ToGodot(definition.Nodes[beamDef.NodeA].Position);
            var nodeBPos = ToGodot(definition.Nodes[beamDef.NodeB].Position);
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

            RegisterAnchor(beamDef.NodeA, i, new Vector2(-halfLength, 0), anchorBeamPerNode, anchorOffsetPerNode);
            RegisterAnchor(beamDef.NodeB, i, new Vector2(halfLength, 0), anchorBeamPerNode, anchorOffsetPerNode);
        }
    }

    private static void RegisterAnchor(int nodeIndex, int beamIndex, Vector2 localOffset, int[] anchorBeamPerNode, Vector2[] anchorOffsetPerNode)
    {
        // The lowest-indexed beam touching a node anchors its visual/core
        // mounting; any incident beam works equally well since they all meet
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
        _coreIndexByNode = new int[definition.Nodes.Count];
        Array.Fill(_coreIndexByNode, -1);

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

    private void CreateCores(CreatureDef definition, int[] anchorBeamPerNode, Vector2[] anchorOffsetPerNode)
    {
        _coreSensors = new CoreSensors[definition.Cores.Count];

        for (var i = 0; i < definition.Cores.Count; i++)
        {
            var core = definition.Cores[i];
            _coreIndexByNode[core.NodeIndex] = i;
            _nodeVisuals[core.NodeIndex].HasCore = true;

            var anchorBeam = _beamBodies[anchorBeamPerNode[core.NodeIndex]];
            var origin = anchorOffsetPerNode[core.NodeIndex];

            var rayDown = CreateRay(anchorBeam, origin, new Vector2(0, _rayLength));
            var rayForward = CreateRay(anchorBeam, origin, new Vector2(_rayLength, 0));
            var rayForwardDown = CreateRay(anchorBeam, origin, new Vector2(_rayLength, _rayLength).Normalized() * _rayLength);

            _coreSensors[i] = new CoreSensors(anchorBeam, rayDown, rayForward, rayForwardDown);
        }
    }

    private static RayCast2D CreateRay(RigidBody2D anchorBeam, Vector2 origin, Vector2 targetPosition)
    {
        var ray = new RayCast2D
        {
            Position = origin,
            TargetPosition = targetPosition,
            Enabled = true,
        };
        anchorBeam.AddChild(ray);
        return ray;
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

        var sensorCount = (_coreSensors.Length * CoreSensors.ValueCount) + (_motorRelations.Length * 2);
        _sensorValues = new double[sensorCount];
        _motorTargets = new double[_motorRelations.Length];

        var scratchSize = Math.Max(sensorCount, Math.Max(_hiddenNeuronCount, _motorRelations.Length));
        _scratchA = new double[scratchSize];
        _scratchB = new double[scratchSize];
    }

    // Stable order: each core's 6 values (see CoreSensors), then each motor
    // relation's (relativeAngle, relativeAngularVelocity), in creation order.
    private void ReadSensors(double[] values)
    {
        var index = 0;
        foreach (var core in _coreSensors)
        {
            core.Read(values, index);
            index += CoreSensors.ValueCount;
        }

        foreach (var relation in _motorRelations)
        {
            values[index++] = relation.RelativeAngle / Math.PI;
            values[index++] = Math.Clamp(relation.RelativeAngularVelocity / _relativeAngularVelocityScale, -1, 1);
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
