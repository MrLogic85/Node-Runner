using Godot;
using NodeRunner.Domain;
using NodeRunner.Mechanics;

namespace NodeRunner.Creature;

// Wheels: a Wheel is its joint's own circle body set free to turn, so Godot's solver rolls it.
public partial class Creature
{
    // Godot's default angular damping (1/s, added to the body's own) would stop a rolling wheel
    // within seconds; Replace keeps this small loss alone, so a wheel coasts but does not spin forever.
    private const float _wheelAngularDamp = 0.05f;

    // The joint body, already sized to the Wheel's radius (CreatureDef.NodeRadius) and carrying its
    // weight on top of its links' shares, is unlocked so it turns, and turns as a thin ring
    // (Mechanics.Wheel.Inertia): Godot would take its circle for a solid disc. Its links stay pinned at
    // its centre, so the pins add no torque and it spins freely between them. The tyre keeps the joint
    // body's default material, friction 1 and no bounce, so it grips like a bare joint.
    private WheelVisual MakeWheel(RigidBody2D body, WheelDef wheel, float radius)
    {
        body.LockRotation = false;
        body.Inertia = ToGodotFloat(Wheel.Inertia(wheel), nameof(Wheel.Inertia));
        body.AngularDamp = _wheelAngularDamp;
        body.AngularDampMode = RigidBody2D.DampMode.Replace;

        var visual = new WheelVisual
        {
            Name = $"Wheel{wheel.Id}Visual",
            Theme = Theme,
            Radius = radius,
        };
        body.AddChild(visual);
        return visual;
    }
}
