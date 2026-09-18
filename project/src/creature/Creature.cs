using Godot;
using NodeRunner.Domain;
using NodeRunner.ML;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

public partial class Creature : Node2D
{
    private const int _hiddenNeuronCount = 8;
    private const double _twitchFrequencyHz = 3.2;
    private const double _brainInfluence = 0.45;
    private const double _twitchInfluence = 0.85;
    private const float _lineHitTolerancePixels = 16;

    private readonly List<Muscle> _muscles = [];
    private readonly List<Connection> _bones = [];
    private readonly List<Connection> _muscleConnections = [];
    private RigidBody2D[] _joints = [];
    private Sensors? _sensors;
    private double[] _sensorValues = [];
    private double[] _muscleTargets = [];
    private double[] _twitchPhases = [];
    private double[] _scratchA = [];
    private double[] _scratchB = [];
    private bool _isBuilt;
    private double _brainTimeSeconds;

    public CreatureDef? Definition { get; set; }

    public VisualTheme Theme { get; set; } = VisualTheme.Neon;

    public NeuralNetwork? Brain { get; private set; }

    public int BrainSeed { get; private set; }

    public bool IsBuilt => _isBuilt;

    public override void _Ready()
    {
        if (!_isBuilt)
        {
            BuildFrom(Definition ?? HardcodedWormFactory.Create());
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_sensors is null || Brain is null || _muscles.Count == 0)
        {
            return;
        }

        _brainTimeSeconds += delta;
        _sensors.Read(_sensorValues, _brainTimeSeconds);
        Brain.Forward(_sensorValues, _muscleTargets, _scratchA, _scratchB);

        for (var i = 0; i < _muscles.Count; i++)
        {
            var pulse = Math.Sin((_brainTimeSeconds * Math.Tau * _twitchFrequencyHz) + _twitchPhases[i]);
            var target = Math.Clamp((_muscleTargets[i] * _brainInfluence) + (pulse * _twitchInfluence), -1, 1);
            _muscles[i].ApplyTarget(target);
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

        _muscles.Clear();
        _bones.Clear();
        _muscleConnections.Clear();
        _isBuilt = true;

        _joints = CreateJoints(definition.Joints);
        CreateBoneSprings(definition.Bones, definition.Joints, _joints);
        CreateMuscles(definition.Muscles, _joints);
        ConfigureBrainBuffers();
        if (_muscles.Count > 0)
        {
            RandomizeBrain(CreateSeed());
        }
    }

    public void RandomizeBrain(int seed)
    {
        if (_sensors is null || _muscles.Count == 0)
        {
            Brain = null;
            BrainSeed = seed;
            return;
        }

        BrainSeed = seed;
        _brainTimeSeconds = 0;
        var random = new Random(seed);
        Brain = new NeuralNetwork(new[] { _sensors.Count, _hiddenNeuronCount, _muscles.Count }, Activation.Tanh, random);
        for (var i = 0; i < _twitchPhases.Length; i++)
        {
            _twitchPhases[i] = random.NextDouble() * Math.Tau;
        }

        GD.Print($"Node Runner brain seed: {seed}");
    }

    public bool TrySelectPart(Vector2 globalPosition, out CreatureElementSelection? selection)
    {
        for (var i = 0; i < _joints.Length; i++)
        {
            var radius = ToGodotFloat(Definition!.Joints[i].Radius, nameof(JointDef.Radius));
            if (_joints[i].GlobalPosition.DistanceSquaredTo(globalPosition) <= radius * radius)
            {
                selection = new CreatureElementSelection(CreatureElementKind.Joint, i);
                return true;
            }
        }

        var lineTolerance = GetLineHitTolerance();
        if (TrySelectConnection(_muscleConnections, CreatureElementKind.Muscle, globalPosition, lineTolerance, out selection))
        {
            return true;
        }

        return TrySelectConnection(_bones, CreatureElementKind.Bone, globalPosition, lineTolerance, out selection);
    }

    private static float BendDirection(int muscleIndex)
    {
        return muscleIndex % 2 == 0 ? 1 : -1;
    }

    private RigidBody2D[] CreateJoints(IReadOnlyList<JointDef> jointDefs)
    {
        var joints = new RigidBody2D[jointDefs.Count];

        for (var i = 0; i < jointDefs.Count; i++)
        {
            var jointDef = jointDefs[i];
            var body = new RigidBody2D
            {
                Name = $"Joint{i}",
                Position = ToGodot(jointDef.Position),
                Mass = 1.2f,
                LinearDamp = 0.55f,
                AngularDamp = 0.55f,
                CanSleep = false,
                ContinuousCd = RigidBody2D.CcdMode.CastRay,
            };

            var collision = new CollisionShape2D
            {
                Shape = new CircleShape2D { Radius = ToGodotFloat(jointDef.Radius, nameof(jointDef.Radius)) },
            };
            body.AddChild(collision);

            var visual = new JointVisual
            {
                Theme = Theme,
                Radius = ToGodotFloat(jointDef.Radius, nameof(jointDef.Radius)),
                IsHead = i == 0,
            };
            body.AddChild(visual);

            AddChild(body);
            joints[i] = body;
        }

        return joints;
    }

    private void CreateBoneSprings(
        IReadOnlyList<BoneDef> boneDefs,
        IReadOnlyList<JointDef> jointDefs,
        IReadOnlyList<RigidBody2D> joints)
    {
        for (var i = 0; i < boneDefs.Count; i++)
        {
            var boneDef = boneDefs[i];
            var length = Distance(jointDefs[boneDef.JointA].Position, jointDefs[boneDef.JointB].Position);
            var spring = CreateSpring(
                $"Bone{i}",
                joints[boneDef.JointA],
                joints[boneDef.JointB],
                length,
                stiffness: 85,
                damping: 12);

            spring.Modulate = Theme.Bone;
            _bones.Add(new Connection(joints[boneDef.JointA], joints[boneDef.JointB]));
        }
    }

    private void CreateMuscles(IReadOnlyList<MuscleDef> muscleDefs, IReadOnlyList<RigidBody2D> joints)
    {
        for (var i = 0; i < muscleDefs.Count; i++)
        {
            var muscleDef = muscleDefs[i];
            var spring = CreateSpring(
                $"Muscle{i}",
                joints[muscleDef.JointA],
                joints[muscleDef.JointB],
                ToGodotFloat(muscleDef.RestLength, nameof(muscleDef.RestLength)),
                stiffness: ToGodotFloat(muscleDef.MaxForce / 18, nameof(muscleDef.MaxForce)),
                damping: 6);

            spring.Modulate = Theme.Muscle;
            _muscles.Add(new Muscle(
                muscleDef,
                spring,
                joints[muscleDef.JointA],
                joints[muscleDef.JointB],
                BendDirection(i),
                ToGodotFloat(muscleDef.MaxForce, nameof(muscleDef.MaxForce))));
            _muscleConnections.Add(new Connection(joints[muscleDef.JointA], joints[muscleDef.JointB]));
        }
    }

    private float GetLineHitTolerance()
    {
        var canvasTransform = GetViewport().GetCanvasTransform();
        var pixelsPerWorldUnit = Math.Max(canvasTransform.X.Length(), canvasTransform.Y.Length());
        return pixelsPerWorldUnit > 0 ? _lineHitTolerancePixels / pixelsPerWorldUnit : _lineHitTolerancePixels;
    }

    private static bool TrySelectConnection(
        IReadOnlyList<Connection> connections,
        CreatureElementKind kind,
        Vector2 point,
        float tolerance,
        out CreatureElementSelection? selection)
    {
        for (var i = 0; i < connections.Count; i++)
        {
            var connection = connections[i];
            if (DistanceSquaredToSegment(point, connection.JointA.GlobalPosition, connection.JointB.GlobalPosition) <= tolerance * tolerance)
            {
                selection = new CreatureElementSelection(kind, i);
                return true;
            }
        }

        selection = null;
        return false;
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

    private DampedSpringJoint2D CreateSpring(
        string name,
        RigidBody2D jointA,
        RigidBody2D jointB,
        float restLength,
        float stiffness,
        float damping)
    {
        var spring = new DampedSpringJoint2D
        {
            Name = name,
            Length = restLength,
            RestLength = restLength,
            Stiffness = stiffness,
            Damping = damping,
            DisableCollision = false,
        };
        AddChild(spring);
        spring.NodeA = spring.GetPathTo(jointA);
        spring.NodeB = spring.GetPathTo(jointB);

        var line = new SegmentVisual
        {
            Name = $"{name}Visual",
            ZIndex = -1,
            Width = name.StartsWith("Bone", StringComparison.Ordinal) ? Theme.BoneWidth : Theme.MuscleWidth,
            Color = name.StartsWith("Bone", StringComparison.Ordinal) ? Theme.Bone : Theme.Muscle,
        };
        line.Connect(jointA, jointB);
        AddChild(line);

        return spring;
    }

    private void ConfigureBrainBuffers()
    {
        if (_muscles.Count == 0)
        {
            Brain = null;
            _sensors = null;
            _sensorValues = [];
            _muscleTargets = [];
            _twitchPhases = [];
            _scratchA = [];
            _scratchB = [];
            return;
        }

        _sensors = new Sensors(_joints);
        _sensorValues = new double[_sensors.Count];
        _muscleTargets = new double[_muscles.Count];
        _twitchPhases = new double[_muscles.Count];

        var scratchSize = Math.Max(_sensors.Count, Math.Max(_hiddenNeuronCount, _muscles.Count));
        _scratchA = new double[scratchSize];
        _scratchB = new double[scratchSize];
    }

    private static int CreateSeed()
    {
        return Random.Shared.Next(int.MinValue, int.MaxValue);
    }

    private static Vector2 ToGodot(Vector2D value)
    {
        return new Vector2(ToGodotFloat(value.X, nameof(value.X)), ToGodotFloat(value.Y, nameof(value.Y)));
    }

    private static float Distance(Vector2D a, Vector2D b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return ToGodotFloat(Math.Sqrt((dx * dx) + (dy * dy)), "distance");
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

    private sealed record Connection(RigidBody2D JointA, RigidBody2D JointB);
}
