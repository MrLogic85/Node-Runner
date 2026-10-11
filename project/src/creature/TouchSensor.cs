using Godot;

namespace NodeRunner.Creature;

/// <summary>
/// A Touch sensor as the sim runs it (#1043): 1 while its joint's body touches the ground, else 0.
/// Godot's contact monitor on the joint's body does the work. That body is also a Wheel's, so a
/// tyre on the ground counts. The body masks only the ground, so the creature's own parts and
/// shadows never count.
/// </summary>
public sealed class TouchSensor
{
    private readonly RigidBody2D _joint;

    public TouchSensor(RigidBody2D joint)
    {
        _joint = joint;
        _joint.ContactMonitor = true;
        _joint.MaxContactsReported = 1;
    }

    /// <summary>The brain input: whether the joint touched the ground on the last physics step.</summary>
    public double Reading => _joint.GetContactCount() > 0 ? 1 : 0;
}
