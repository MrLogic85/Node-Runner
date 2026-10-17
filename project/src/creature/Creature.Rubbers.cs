using Godot;
using NodeRunner.Mechanics;

namespace NodeRunner.Creature;

// Rubbers: a Rubber makes its joint's body, or its Wheel's tyre, grip the ground harder.
public partial class Creature
{
    // Godot Physics 2D combines two bodies' friction as the lower of the two, but a Rough material's
    // friction wins instead. The ground keeps Godot's default (friction 1), so the joint grips at
    // Rubber.Friction exactly; no bounce, as for every joint.
    private static void MakeRubber(RigidBody2D body)
    {
        body.PhysicsMaterialOverride = new PhysicsMaterial
        {
            Bounce = 0,
            Friction = ToGodotFloat(Rubber.Friction, nameof(Rubber.Friction)),
            Rough = true,
        };
    }
}
