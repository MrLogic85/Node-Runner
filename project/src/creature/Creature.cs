using Godot;
using NodeRunner.Domain;
using NodeRunner.ML;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

public partial class Creature : Node2D
{
    private const int _hiddenNeuronCount = 8;

    private readonly List<Muscle> _muscles = [];
    private RigidBody2D[] _joints = [];
    private Sensors? _sensors;
    private double[] _sensorValues = [];
    private double[] _muscleTargets = [];
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
            BuildFrom(Definition ?? HardcodedWormFactory.Create());
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_sensors is null || Brain is null || _muscles.Count == 0)
        {
            return;
        }

        _sensors.Read(_sensorValues);
        Brain.Forward(_sensorValues, _muscleTargets, _scratchA, _scratchB);

        for (var i = 0; i < _muscles.Count; i++)
        {
            _muscles[i].ApplyTarget(_muscleTargets[i]);
        }
    }

    public void BuildFrom(CreatureDef definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _muscles.Clear();
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
        Brain = new NeuralNetwork(new[] { _sensors.Count, _hiddenNeuronCount, _muscles.Count }, Activation.Tanh, new Random(seed));
        GD.Print($"Node Runner brain seed: {seed}");
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
                LinearDamp = 1.6f,
                AngularDamp = 1.6f,
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
            _muscles.Add(new Muscle(muscleDef, spring));
        }
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
            _scratchA = [];
            _scratchB = [];
            return;
        }

        _sensors = new Sensors(_joints);
        _sensorValues = new double[_sensors.Count];
        _muscleTargets = new double[_muscles.Count];

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
}
