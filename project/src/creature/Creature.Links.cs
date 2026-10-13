using Godot;
using NodeRunner.Domain;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

// Builds the links between body parts: Servos, Velocity motors, Brakes and Angle sensors on joints, Pistons with their end stops, and Springs.
public partial class Creature
{
    private void CreateServos(CreatureDef definition)
    {
        _servos = new ServoJoint[definition.Servos.Count];
        _servoVisuals = new ServoVisual[definition.Servos.Count];
        for (var i = 0; i < definition.Servos.Count; i++)
        {
            var servo = definition.Servos[i];
            _servos[i] = new ServoJoint(servo, MotorSidesFor(definition, servo));
            var sides = _servos[i].Sides;
            var visual = new ServoVisual
            {
                Name = $"Servo{i}Visual",
                Theme = Theme,
                Sides = sides,
                Radius = ToGodotFloat(JointMotor.Radius, nameof(JointMotor.Radius)),
                BuiltAngle = (float)sides.BuiltRelativeRotation,
                TargetAngle = (float)sides.BuiltRelativeRotation,
                Range = (float)servo.Range,
                Start = (float)servo.Start,
                HousingReach = MotorHousingReach(definition, servo),
                Position = sides.JointLocalPosition,
                Rotation = (float)sides.FixedRotation,
            };
            AddChild(visual);
            _servoVisuals[i] = visual;
        }
    }

    // A Velocity motor (#454) turns its sides as a Servo does, with no range or stops.
    private void CreateVelocityMotors(CreatureDef definition)
    {
        _velocityMotors = new VelocityMotorJoint[definition.VelocityMotors.Count];
        _velocityMotorVisuals = new VelocityMotorVisual[definition.VelocityMotors.Count];
        for (var i = 0; i < definition.VelocityMotors.Count; i++)
        {
            var motor = definition.VelocityMotors[i];
            _velocityMotors[i] = new VelocityMotorJoint(motor, MotorSidesFor(definition, motor));
            var sides = _velocityMotors[i].Sides;
            var visual = new VelocityMotorVisual
            {
                Name = $"VelocityMotor{i}Visual",
                Theme = Theme,
                Sides = sides,
                Position = sides.JointLocalPosition,
                FixedAngle = (float)sides.FixedRotation,
                TargetAngle = (float)sides.TargetRotation,
            };
            AddChild(visual);
            _velocityMotorVisuals[i] = visual;
        }
    }

    // A Brake (#1040) holds its sides as a Servo turns them, so it shares the Servo's sides. A Locked
    // one is no friction but a weld (WeldSides), unless both its sides slide at the joint: then it is
    // friction without a limit. Each is drawn whatever holds it (#1041). Run once the Piston and Spring
    // cylinders exist, as a Locked side can be one.
    private void CreateBrakes(CreatureDef definition)
    {
        var brakes = new List<BrakeJoint>();
        _brakeVisuals = new BrakeVisual[definition.Brakes.Count];
        for (var i = 0; i < definition.Brakes.Count; i++)
        {
            var brake = definition.Brakes[i];
            var sides = MotorSidesFor(definition, brake);
            if (!brake.Locked || !WeldSides(definition, brake))
            {
                brakes.Add(new BrakeJoint(brake, sides));
            }

            var visual = new BrakeVisual
            {
                Name = $"Brake{i}Visual",
                Theme = Theme,
                Sides = sides,
                Locked = brake.Locked,
                Position = sides.JointLocalPosition,
                FixedAngle = (float)sides.FixedRotation,
                TargetAngle = (float)sides.TargetRotation,
            };
            AddChild(visual);
            _brakeVisuals[i] = visual;
        }

        _brakes = [.. brakes];
    }

    // An Angle sensor (#1204) reads its sides as a Velocity motor does and never turns them, so it adds
    // no joint and no torque. Its arc (#1205) sits outside its joint's Rubber skin or Touch sensor's cushion,
    // with its knock-out under the whole creature.
    private void CreateAngleSensors(CreatureDef definition)
    {
        _angleSensors = new AngleSensorJoint[definition.AngleSensors.Count];
        _angleSensorVisuals = new AngleSensorVisual[definition.AngleSensors.Count];
        for (var i = 0; i < definition.AngleSensors.Count; i++)
        {
            var angleSensor = definition.AngleSensors[i];
            _angleSensors[i] = new AngleSensorJoint(angleSensor, MotorSidesFor(definition, angleSensor));
            var sides = _angleSensors[i].Sides;
            var arcRadius = AngleSensorGeometry.ArcRadius(
                rubber: definition.Rubbers.Any(rubber => rubber.NodeId == angleSensor.NodeId),
                touch: definition.TouchSensors.Any(touch => touch.NodeId == angleSensor.NodeId));
            var knockout = CreateKnockout(Vector2.Zero, Vector2.Zero, AngleSensorGeometry.ArcWidth / 2);
            knockout.Position = sides.JointLocalPosition;
            var visual = new AngleSensorVisual
            {
                Name = $"AngleSensor{i}Visual",
                Theme = Theme,
                Sides = sides,
                Knockout = knockout,
                ArcRadius = arcRadius,
                Position = sides.JointLocalPosition,
                FixedAngle = (float)sides.FixedRotation,
                TargetAngle = (float)sides.TargetRotation,
            };
            knockout.Path = visual.Arc;
            AddChild(knockout);
            AddChild(visual);
            _angleSensorVisuals[i] = visual;
        }
    }

    // A Locked Brake welds its two sides' bodies with a second joint away from the joint's own pin:
    // two joints hold two bodies rigidly, inside Godot's solver, so no load can turn the joint. A
    // friction torque lags a step behind the load it holds, so even an endless one gives a little
    // under a sudden load. Godot's own angular limit and motor on PinJoint2D cannot hold it
    // (godotengine/godot#91691, #87513). A Piston or Spring whose node B is the joint slides that
    // node along its cylinder, so a pin would stop its stroke; that side's far node runs in a groove
    // on the other side's body instead (SlideLock). When both sides slide no Godot joint locks only
    // the angle, so it returns false and the Brake holds as endless friction.
    private bool WeldSides(CreatureDef definition, BrakeDef brake)
    {
        var fixedSide = brake.FixedLinkId!.Value;
        var targetSide = brake.TargetLinkId!.Value;
        var fixedSlides = SlidesAt(definition, brake.NodeId, fixedSide);
        var targetSlides = SlidesAt(definition, brake.NodeId, targetSide);
        if (fixedSlides && targetSlides)
        {
            return false;
        }

        var fixedBody = SideBody(definition, brake.NodeId, fixedSide);
        var targetBody = SideBody(definition, brake.NodeId, targetSide);
        var joint = _nodeBodies[definition.NodeIndexOf(brake.NodeId)].Position;
        if (fixedSlides || targetSlides)
        {
            // The sliding side's cylinder is not in the lock, so only the holding side turns as a bar.
            var (holdingSide, holdingBody) = fixedSlides ? (targetSide, targetBody) : (fixedSide, fixedBody);
            WeighCylinderAsBar(definition, holdingSide, holdingBody);
            var sliding = definition.Link(fixedSlides ? fixedSide : targetSide);
            SlideLock($"Brake{brake.Id}Weld", holdingBody, joint, _nodeBodies[definition.NodeIndexOf(sliding.NodeA)]);
            return true;
        }

        WeighCylinderAsBar(definition, fixedSide, fixedBody);
        WeighCylinderAsBar(definition, targetSide, targetBody);

        var along = definition.IsWheel(fixedSide) ? definition.Link(targetSide) : definition.Link(fixedSide);
        var far = _nodeBodies[definition.NodeIndexOf(along.FarNodeFrom(brake.NodeId))].Position;
        var weld = new PinJoint2D { Name = $"Brake{brake.Id}Weld", Position = (joint + far) / 2 };
        AddChild(weld);
        weld.NodeA = weld.GetPathTo(fixedBody);
        weld.NodeB = weld.GetPathTo(targetBody);
        return true;
    }

    // Locks a sliding Piston's or Spring's angle on the other side's body: its far node (node A)
    // runs in a groove on that body along the line through the joint, so the stroke stays free. The
    // groove reaches far past the stroke's ends, so only the end stops stop it, and has no cap
    // (CapEndStops caps only those): a capped groove would let the lock turn, as Godot caps a
    // groove's whole push, across it too. It holds the far node, not node B, as the cylinder is
    // too light to carry the lock: grooves on it gave way when a creature landed.
    private void SlideLock(string name, RigidBody2D body, Vector2 joint, RigidBody2D farNode)
    {
        var toFar = farNode.Position - joint;
        var groove = new GrooveJoint2D
        {
            Name = name,
            Position = joint - (toFar.Normalized() * _lockGrooveReach),
            Rotation = toFar.Angle() - (Mathf.Pi / 2),
            Length = 2 * _lockGrooveReach,
            InitialOffset = _lockGrooveReach + toFar.Length(),
        };
        AddChild(groove);
        groove.NodeA = groove.GetPathTo(body);
        groove.NodeB = groove.GetPathTo(farNode);
    }

    // Whether a Piston or Spring slides its node B at this joint.
    private static bool SlidesAt(CreatureDef definition, int nodeId, int linkId) =>
        !definition.IsWheel(linkId)
        && definition.Link(linkId) is { Kind: CreatureElementKind.Piston or CreatureElementKind.Spring } link
        && link.NodeB == nodeId;

    // A welded Piston's or Spring's cylinder carries the turning of the whole other side, and a
    // cylinder turns as a thin rod on node A (CreateEndStops), far lighter to turn than what hangs
    // on it. Godot's solver launches bodies with such mismatched weights, so the cylinder turns as
    // a bar its link's length instead, like a beam's own body.
    private void WeighCylinderAsBar(CreatureDef definition, int linkId, RigidBody2D body)
    {
        if (definition.IsWheel(linkId) || definition.Link(linkId).Kind == CreatureElementKind.Beam)
        {
            return;
        }

        var link = definition.Link(linkId);
        var length = _nodeBodies[definition.NodeIndexOf(link.NodeA)].Position
            .DistanceTo(_nodeBodies[definition.NodeIndexOf(link.NodeB)].Position);
        body.Inertia = body.Mass * ((length * length) + (_beamInertiaThickness * _beamInertiaThickness)) / 12;
    }

    // The body that turns with a side: a beam's own, a Piston's or Spring's cylinder, or the Wheel's joint body.
    private RigidBody2D SideBody(CreatureDef definition, int nodeId, int linkId)
    {
        if (definition.IsWheel(linkId))
        {
            return _nodeBodies[definition.NodeIndexOf(nodeId)];
        }

        return definition.Link(linkId).Kind switch
        {
            CreatureElementKind.Beam => _beamBodies[definition.BeamIndexOf(linkId)],
            CreatureElementKind.Piston => _pistonEndStops[definition.PistonIndexOf(linkId)].Cylinder,
            CreatureElementKind.Spring => _springEndStops[definition.SpringIndexOf(linkId)].Cylinder,
            var kind => throw new InvalidOperationException($"A {kind} is no link."),
        };
    }

    private MotorSides MotorSidesFor(CreatureDef definition, IMotorOrBrakeDef motor) => new(
        MotorSideFor(definition, motor.NodeId, motor.FixedLinkId!.Value),
        MotorSideFor(definition, motor.NodeId, motor.TargetLinkId!.Value));

    private MotorSide MotorSideFor(CreatureDef definition, int motorNodeId, int linkId)
    {
        var joint = _nodeBodies[definition.NodeIndexOf(motorNodeId)];
        if (definition.IsWheel(linkId))
        {
            return new MotorWheelSide(joint);
        }

        var link = definition.Link(linkId);
        return new MotorLinkSide(joint, _nodeBodies[definition.NodeIndexOf(link.FarNodeFrom(motorNodeId))]);
    }

    private static float MotorHousingReach(CreatureDef definition, IJointMotorDef motor)
    {
        // A Wheel side has no link for the housing to run along (#1118).
        if (motor.FixedLinkId is not { } fixedLinkId || definition.IsWheel(fixedLinkId))
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
        _pistonEndStops = new EndStops[definition.Pistons.Count];
        for (var i = 0; i < _pistons.Length; i++)
        {
            var piston = definition.Pistons[i];
            var indexA = definition.NodeIndexOf(piston.NodeA);
            var indexB = definition.NodeIndexOf(piston.NodeB);
            _pistons[i] = new PistonLink(piston, _nodeBodies[indexA], _nodeBodies[indexB]);
            var built = (float)_pistons[i].BuiltLength;
            _pistonEndStops[i] = CreateEndStops(
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
    private EndStops CreateEndStops(string name, RigidBody2D nodeA, RigidBody2D nodeB, float built, float shortest, float longest)
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
        return new EndStops(cylinder, groove);
    }

    // Every end stop gives way past EndStop.MaxForce (#1115; why: AGENTS.md rule 4). Godot has no
    // such cap on the GrooveJoint2D node, only the server's MaxForce, which it keeps when it
    // remakes the joint. Run once every body exists, as the weight counts them all. A Locked
    // Brake's groove is no end stop and keeps no cap (SlideLock).
    private void CapEndStops(CreatureDef definition)
    {
        var mass = GetChildren().OfType<RigidBody2D>().Sum(body => (double)body.Mass);
        var gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsDouble();
        var maxForce = (float)Mechanics.EndStop.MaxForce(definition.Pistons.Select(piston => piston.Strength), mass, gravity);
        foreach (var stop in _pistonEndStops.Concat(_springEndStops).Select(stops => stops.Groove))
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
        _springEndStops = new EndStops[_springVisuals.Length];
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
            _springEndStops[i] = CreateEndStops($"Spring{i}", nodeA, nodeB, built, (float)shortest, (float)longest);
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

    // A Piston's or Spring's hidden cylinder and the groove that holds its node B between the stroke's ends.
    private readonly record struct EndStops(RigidBody2D Cylinder, GrooveJoint2D Groove);
}
