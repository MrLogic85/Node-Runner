using Godot;
using NodeRunner.Domain;
using NodeRunner.Mechanics;

namespace NodeRunner.Creature;

/// <summary>
/// A Touch sensor as the sim runs it (#1043, #1045): how far the nearest ground reaches into its
/// joint's cushion (<see cref="Touch.Reading"/>). A <see cref="ShapeCast2D"/> child casts the cushion
/// in place and finds that ground in any direction, so slopes and steps read as flat ground does;
/// it allocates nothing per step, unlike a direct space query. The joint's body is also a Wheel's,
/// so a tyre near the ground counts. The cast masks what the body collides with, the ground, so the
/// creature's own parts and shadows never count.
/// </summary>
public sealed class TouchSensor
{
    private readonly ShapeCast2D _cushion;
    private readonly float _jointRadius;

    public TouchSensor(RigidBody2D joint, float jointRadius)
    {
        _jointRadius = jointRadius;
        _cushion = new ShapeCast2D
        {
            Name = "TouchCushion",
            Shape = new CircleShape2D { Radius = jointRadius + (float)TouchSensorDef.Cushion },
            TargetPosition = Vector2.Zero,
            CollisionMask = joint.CollisionMask,
            MaxResults = 1,
            // Sense casts it, so the reading is of the pose the network reads in this step.
            Enabled = false,
        };
        joint.AddChild(_cushion);
    }

    /// <summary>The network input, 0–1, as of the last <see cref="Sense"/>; 0 once reset.</summary>
    public double Reading { get; private set; }

    /// <summary>Reads the ground round the joint; on a physics step, when the space can be queried.</summary>
    public void Sense()
    {
        _cushion.ForceShapecastUpdate();
        Reading = _cushion.IsColliding()
            ? Touch.Reading(_cushion.GlobalPosition.DistanceTo(_cushion.GetCollisionPoint(0)), _jointRadius)
            : 0;
    }

    public void Reset() => Reading = 0;
}
