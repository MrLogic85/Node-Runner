using Godot;
using NodeRunner.Domain;

namespace NodeRunner.Creature;

public partial class Creature : Node2D
{
    private readonly List<Muscle> _muscles = [];

    public CreatureDef? Definition { get; set; }

    public override void _Ready()
    {
        BuildFrom(Definition ?? HardcodedWormFactory.Create());
    }

    public void BuildFrom(CreatureDef definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _muscles.Clear();

        var joints = CreateJoints(definition.Joints);
        CreateBoneSprings(definition.Bones, definition.Joints, joints);
        CreateMuscles(definition.Muscles, joints);
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

            spring.Modulate = new Color(0.67f, 0.45f, 0.28f);
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

            spring.Modulate = new Color(0.96f, 0.36f, 0.47f);
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
            Width = name.StartsWith("Bone", StringComparison.Ordinal) ? 8 : 4,
            Color = name.StartsWith("Bone", StringComparison.Ordinal)
                ? new Color(0.67f, 0.45f, 0.28f)
                : new Color(0.96f, 0.36f, 0.47f),
        };
        line.Connect(jointA, jointB);
        AddChild(line);

        return spring;
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
