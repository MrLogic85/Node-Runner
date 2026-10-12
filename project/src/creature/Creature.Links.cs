using Godot;
using NodeRunner.Domain;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

// Builds the links between body parts: Servos on joints, Pistons with their end stops, and Springs.
public partial class Creature
{
    private void CreateServos(CreatureDef definition)
    {
        _servos = new ServoJoint[definition.Servos.Count];
        _servoVisuals = new ServoVisual[definition.Servos.Count];
        for (var i = 0; i < definition.Servos.Count; i++)
        {
            var servo = definition.Servos[i];
            var fixedSide = ServoSideFor(definition, servo.NodeId, servo.FixedLinkId!.Value);
            var targetSide = ServoSideFor(definition, servo.NodeId, servo.TargetLinkId!.Value);
            _servos[i] = new ServoJoint(servo, fixedSide, targetSide);
            var visual = new ServoVisual
            {
                Name = $"Servo{i}Visual",
                Theme = Theme,
                Link = _servos[i],
                Radius = ToGodotFloat(JointMotor.Radius, nameof(JointMotor.Radius)),
                BuiltAngle = (float)_servos[i].BuiltRelativeRotation,
                TargetAngle = (float)_servos[i].BuiltRelativeRotation,
                Range = (float)servo.Range,
                Start = (float)servo.Start,
                HousingReach = ServoHousingReach(definition, servo),
                Position = _servos[i].JointLocalPosition,
                Rotation = (float)_servos[i].FixedRotation,
            };
            AddChild(visual);
            _servoVisuals[i] = visual;
        }
    }

    private ServoSide ServoSideFor(CreatureDef definition, int servoNodeId, int linkId)
    {
        var joint = _nodeBodies[definition.NodeIndexOf(servoNodeId)];
        if (definition.IsWheel(linkId))
        {
            return new ServoWheelSide(joint);
        }

        var link = definition.Link(linkId);
        return new ServoLinkSide(joint, _nodeBodies[definition.NodeIndexOf(link.FarNodeFrom(servoNodeId))]);
    }

    private static float ServoHousingReach(CreatureDef definition, ServoDef servo)
    {
        // A Wheel side has no link for the housing to run along (#1118).
        if (servo.FixedLinkId is not { } fixedLinkId || definition.IsWheel(fixedLinkId))
        {
            return 0;
        }

        var link = definition.Link(fixedLinkId);
        return ServoGeometry.HousingReach(definition.Nodes, definition.CoveredRadius, link, SensorLength(definition, link));
    }

    private static float SensorLength(CreatureDef definition, LinkRef link) =>
        link.Kind == CreatureElementKind.Beam && definition.Sensors.FirstOrDefault(sensor => sensor.BeamId == link.Id) is { } sensor ? (float)SensorPicture.SizeOf(sensor.Kind) : 0;

    // A Piston pushes on its two node bodies; its only body is the hidden end-stop cylinder. Its
    // picture is a child of the creature, on the Links layer over the beams.
    private void CreatePistons(CreatureDef definition)
    {
        _pistons = new PistonLink[definition.Pistons.Count];
        _pistonVisuals = new PistonVisual[definition.Pistons.Count];
        _pistonCylinders = new RigidBody2D[definition.Pistons.Count];
        for (var i = 0; i < _pistons.Length; i++)
        {
            var piston = definition.Pistons[i];
            var indexA = definition.NodeIndexOf(piston.NodeA);
            var indexB = definition.NodeIndexOf(piston.NodeB);
            _pistons[i] = new PistonLink(piston, _nodeBodies[indexA], _nodeBodies[indexB]);
            var built = (float)_pistons[i].BuiltLength;
            _pistonCylinders[i] = CreateEndStops(
                $"Piston{i}",
                _pistons[i].NodeA,
                _pistons[i].NodeB,
                built,
                (float)_pistons[i].ShortestLength,
                (float)_pistons[i].LongestLength);
            var visual = new PistonVisual
            {
                Name = $"Piston{i}Visual",
                Theme = Theme,
                Link = _pistons[i],
                RadiusA = ToGodotFloat(definition.NodeRadius(piston.NodeA), nameof(JointMotor.Radius)),
                RadiusB = ToGodotFloat(definition.NodeRadius(piston.NodeB), nameof(JointMotor.Radius)),
                CoveredA = ToGodotFloat(definition.CoveredRadius(piston.NodeA), nameof(JointMotor.Radius)),
                CoveredB = ToGodotFloat(definition.CoveredRadius(piston.NodeB), nameof(JointMotor.Radius)),
                Travel = (float)(_pistons[i].LongestLength - _pistons[i].ShortestLength),
            };
            AddChild(visual);
            _pistonVisuals[i] = visual;
        }
    }

    // A Piston's end stops are a hard limit, as in a real cylinder (#701): a collider-free
    // cylinder body turns freely on node A, and Godot's GrooveJoint2D lets node B
    // slide only along the cylinder between the stroke's two ends. The groove needs that body:
    // nodes have locked rotation, so a groove on node A would keep one world direction instead of
    // turning with the Piston. It weighs half a beam (#731): Godot's joints give far past an end
    // when the cylinder or its nodes are much lighter than the rest. Inside the stroke the groove
    // pushes nothing along the piston, so only the piston's own force moves it; at an end it
    // holds up to EndStop.MaxForce, then slides (CapEndStops). A Spring has the same stops (#835).
    private RigidBody2D CreateEndStops(string name, RigidBody2D nodeA, RigidBody2D nodeB, float built, float shortest, float longest)
    {
        var a = nodeA.Position;
        var axis = (nodeB.Position - a).Normalized();
        var rotation = axis.Angle();

        var cylinder = new RigidBody2D
        {
            Name = $"{name}Cylinder",
            CollisionLayer = 0,
            CollisionMask = 0,
            Position = a,
            Rotation = rotation,
            Mass = _cylinderMass,
            CenterOfMassMode = RigidBody2D.CenterOfMassModeEnum.Custom,
            CenterOfMass = Vector2.Zero,
            Inertia = _cylinderMass * _beamInertiaThickness * _beamInertiaThickness / 12,
            CanSleep = false,
        };
        AddChild(cylinder);

        var pin = new PinJoint2D { Name = $"{name}CylinderPin", Position = a };
        AddChild(pin);
        pin.NodeA = pin.GetPathTo(nodeA);
        pin.NodeB = pin.GetPathTo(cylinder);

        // The groove runs along the joint's own +Y, so it is turned a quarter back from the axis.
        var groove = new GrooveJoint2D
        {
            Name = $"{name}EndStops",
            Position = a + (axis * shortest),
            Rotation = rotation - (Mathf.Pi / 2),
            Length = longest - shortest,
            InitialOffset = built - shortest,
        };
        AddChild(groove);
        groove.NodeA = groove.GetPathTo(cylinder);
        groove.NodeB = groove.GetPathTo(nodeB);
        return cylinder;
    }

    // Every end stop gives way past EndStop.MaxForce (#1115; why: AGENTS.md rule 4). Godot has no
    // such cap on the GrooveJoint2D node, only the server's MaxForce, which it keeps when it
    // remakes the joint. Run once every body exists, as the weight counts them all.
    private void CapEndStops(CreatureDef definition)
    {
        var mass = GetChildren().OfType<RigidBody2D>().Sum(body => (double)body.Mass);
        var gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsDouble();
        var maxForce = (float)Mechanics.EndStop.MaxForce(definition.Pistons.Select(piston => piston.Strength), mass, gravity);
        foreach (var stop in GetChildren().OfType<GrooveJoint2D>())
        {
            PhysicsServer2D.JointSetParam(stop.GetRid(), PhysicsServer2D.JointParam.MaxForce, maxForce);
        }
    }

    // A Spring is Godot's DampedSpringJoint2D between its two node bodies (#453): it pulls them
    // toward its rest length with its Stiffness and damps the speed between them with its
    // Damping coefficient, whatever they weigh (#801). Godot's spring has no stops of its own, so
    // it gets a Piston's end stops for its Stroke (#835); a coil length past a stop moves its rest
    // length there, so it starts pressed against it, and its SpringLink keeps that preload
    // inside the stop. It has no collider and no brain ports; its picture is a child of the
    // creature, on the Links layer over the beams.
    private void CreateSprings(CreatureDef definition)
    {
        _springVisuals = new SpringVisual[definition.Springs.Count];
        _springCylinders = new RigidBody2D[_springVisuals.Length];
        _springs = new SpringLink[_springVisuals.Length];

        // Godot's damped spring (godot_joints_2d.cpp, checked in 4.7) damps on every second solver
        // iteration, not once per step, so it would damp several times harder than its coefficient.
        // Dividing by the passes makes it damp as its coefficient asks (measured headless at 16 iterations).
        var dampingPasses = (ProjectSettings.GetSetting("physics/2d/solver/solver_iterations").AsInt32() + 1) / 2;
        for (var i = 0; i < _springVisuals.Length; i++)
        {
            var spring = definition.Springs[i];
            var indexA = definition.NodeIndexOf(spring.NodeA);
            var indexB = definition.NodeIndexOf(spring.NodeB);
            var (nodeA, nodeB) = (_nodeBodies[indexA], _nodeBodies[indexB]);
            var built = Math.Max(nodeA.Position.DistanceTo(nodeB.Position), 1f);

            // The joint hangs its second anchor Length along its own +Y, so it is turned a quarter back from the axis.
            var joint = new DampedSpringJoint2D
            {
                Name = $"Spring{i}",
                Position = nodeA.Position,
                Rotation = (nodeB.Position - nodeA.Position).Angle() - (Mathf.Pi / 2),
                Length = built,
                RestLength = (float)Mechanics.Spring.RestLength(spring, built, Mechanics.Travel.HubRadii),
                Stiffness = (float)spring.Stiffness,
                Damping = (float)(spring.Damping / dampingPasses),
            };
            AddChild(joint);
            joint.NodeA = joint.GetPathTo(nodeA);
            joint.NodeB = joint.GetPathTo(nodeB);
            var shortest = Mechanics.Spring.ShortestLength(spring, built, Mechanics.Travel.HubRadii);
            var longest = Mechanics.Spring.LongestLength(spring, built, Mechanics.Travel.HubRadii);
            _springCylinders[i] = CreateEndStops($"Spring{i}", nodeA, nodeB, built, (float)shortest, (float)longest);
            _springs[i] = new SpringLink(joint, nodeA, nodeB, joint.RestLength, shortest, longest);

            var visual = new SpringVisual
            {
                Name = $"Spring{i}Visual",
                Theme = Theme,
                NodeA = nodeA,
                NodeB = nodeB,
                RadiusA = ToGodotFloat(definition.NodeRadius(spring.NodeA), nameof(JointMotor.Radius)),
                RadiusB = ToGodotFloat(definition.NodeRadius(spring.NodeB), nameof(JointMotor.Radius)),
                CoveredA = ToGodotFloat(definition.CoveredRadius(spring.NodeA), nameof(JointMotor.Radius)),
                CoveredB = ToGodotFloat(definition.CoveredRadius(spring.NodeB), nameof(JointMotor.Radius)),
                Travel = (float)(longest - shortest),
                Rest = joint.RestLength,
                Stiffness = spring.Stiffness,
            };
            AddChild(visual);
            _springVisuals[i] = visual;
        }
    }
}
