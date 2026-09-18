using Godot;

namespace NodeRunner.Creature;

/// <summary>
/// One controllable connection between two beams that share a node. The
/// model outputs a target angular velocity in [-1, 1]; the motor drives
/// torque (capped at <see cref="MaxTorque"/>) to chase that target, scaled by
/// <see cref="MaxAngularVelocity"/>. Both limits are static per relation for
/// 0.2.0 — see docs/CREATURE_MODEL.md.
/// </summary>
public sealed class MotorRelation
{
    // How aggressively the motor chases its target velocity before the
    // result is clamped to MaxTorque. Not itself exposed as a creature
    // parameter — it only shapes how quickly MaxTorque is reached.
    private const float _velocityGain = 40f;

    public MotorRelation(RigidBody2D referenceBeam, RigidBody2D otherBeam, float maxTorque, float maxAngularVelocity)
    {
        ReferenceBeam = referenceBeam;
        OtherBeam = otherBeam;
        MaxTorque = maxTorque;
        MaxAngularVelocity = maxAngularVelocity;
    }

    public RigidBody2D ReferenceBeam { get; }

    public RigidBody2D OtherBeam { get; }

    public float MaxTorque { get; }

    public float MaxAngularVelocity { get; }

    public double RelativeAngle => Mathf.Wrap(OtherBeam.Rotation - ReferenceBeam.Rotation, -Mathf.Pi, Mathf.Pi);

    public double RelativeAngularVelocity => OtherBeam.AngularVelocity - ReferenceBeam.AngularVelocity;

    /// <param name="target">Desired angular velocity in [-1, 1].</param>
    public void Drive(double target)
    {
        var targetAngularVelocity = Math.Clamp(target, -1, 1) * MaxAngularVelocity;
        var error = targetAngularVelocity - RelativeAngularVelocity;
        var torque = (float)Math.Clamp(error * _velocityGain, -MaxTorque, MaxTorque);
        OtherBeam.ApplyTorque(torque);
        ReferenceBeam.ApplyTorque(-torque);
    }
}
