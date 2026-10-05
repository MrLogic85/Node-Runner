using Godot;
using NodeRunner.Domain;

namespace NodeRunner.Creature;

// Builds the links between node bodies: Pistons with their end stops, and Springs.
public partial class Creature
{
    // A Piston pushes on its two node bodies; its only body is the hidden end-stop cylinder. Its
    // picture is a child of the creature, on the Links layer over the beams.
    private void CreatePistons(CreatureDef definition)
    {
        _pistons = new PistonLink[definition.Pistons.Count];
        _pistonVisuals = new PistonVisual[definition.Pistons.Count];
        _cylinderBodies = new RigidBody2D[definition.Pistons.Count];
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
                Theme = Theme,
                Link = _pistons[i],
                RadiusA = ToGodotFloat(definition.Nodes[indexA].Radius, nameof(NodeDef.Radius)),
                RadiusB = ToGodotFloat(definition.Nodes[indexB].Radius, nameof(NodeDef.Radius)),
                Shortest = (float)Mechanics.Piston.ShortestLength(_pistons[i].BuiltLength, piston.Stroke),
                Longest = (float)Mechanics.Piston.LongestLength(_pistons[i].BuiltLength, piston.Stroke),
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
    // holds whatever the load.
    private void CreateEndStops(int index)
    {
        var link = _pistons[index];
        var a = link.NodeA.Position;
        var axis = (link.NodeB.Position - a).Normalized();
        var shortest = (float)Mechanics.Piston.ShortestLength(link.BuiltLength, link.Definition.Stroke);
        var longest = (float)Mechanics.Piston.LongestLength(link.BuiltLength, link.Definition.Stroke);
        var rotation = axis.Angle();

        var cylinder = new RigidBody2D
        {
            Name = $"Piston{index}Cylinder",
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
        _cylinderBodies[index] = cylinder;

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

    // A Spring is Godot's DampedSpringJoint2D between its two node bodies (#453): it pulls them
    // toward their built distance with its Stiffness and damps the speed between them with its
    // Damping coefficient, whatever they weigh (#801). It has no body or collider of its own and no brain ports; its
    // picture is a child of the creature, on the Links layer over the beams.
    private void CreateSprings(CreatureDef definition)
    {
        _springVisuals = new SpringVisual[definition.Springs.Count];

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
                RestLength = built,
                Stiffness = (float)spring.Stiffness,
                Damping = (float)(spring.Damping / dampingPasses),
            };
            AddChild(joint);
            joint.NodeA = joint.GetPathTo(nodeA);
            joint.NodeB = joint.GetPathTo(nodeB);

            var visual = new SpringVisual
            {
                Name = $"Spring{i}Visual",
                Theme = Theme,
                NodeA = nodeA,
                NodeB = nodeB,
                RadiusA = ToGodotFloat(definition.Nodes[indexA].Radius, nameof(NodeDef.Radius)),
                RadiusB = ToGodotFloat(definition.Nodes[indexB].Radius, nameof(NodeDef.Radius)),
            };
            AddChild(visual);
            _springVisuals[i] = visual;
        }
    }
}
