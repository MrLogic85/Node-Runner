using System.Diagnostics;
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
    // result is clamped to MaxTorque. Derived from maxTorque/maxAngularVelocity
    // (not itself a creature parameter) so that a full-speed target reached
    // from rest actually commands MaxTorque — with a fixed gain, MaxTorque
    // could be unreachable in practice (e.g. a beam resting flat on the
    // ground needs more torque than a low gain could ever produce, no
    // matter how high MaxTorque was set).
    private readonly float _velocityGain;

    public MotorRelation(RigidBody2D referenceBeam, RigidBody2D otherBeam, float maxTorque, float maxAngularVelocity)
    {
        // A zero (or non-finite) maxAngularVelocity would make the gain
        // below Infinity/NaN, poisoning Drive()'s torque output.
        Debug.Assert(maxAngularVelocity > 0, "maxAngularVelocity must be positive.");
        ReferenceBeam = referenceBeam;
        OtherBeam = otherBeam;
        MaxTorque = maxTorque;
        MaxAngularVelocity = maxAngularVelocity;
        _velocityGain = maxTorque / maxAngularVelocity;
    }

    public RigidBody2D ReferenceBeam { get; }

    public RigidBody2D OtherBeam { get; }

    public float MaxTorque { get; }

    public float MaxAngularVelocity { get; }

    public double RelativeAngle => Mathf.Wrap(OtherBeam.Rotation - ReferenceBeam.Rotation, -Mathf.Pi, Mathf.Pi);

    public double RelativeAngularVelocity => OtherBeam.AngularVelocity - ReferenceBeam.AngularVelocity;

    /// <summary>
    /// The torque this relation applied to <see cref="OtherBeam"/> on the
    /// last <see cref="Drive"/> call. Read-only telemetry for the mapping
    /// display (issue #42) — has no effect on physics itself.
    /// </summary>
    public float LastAppliedTorque { get; private set; }

    /// <param name="target">Desired angular velocity in [-1, 1].</param>
    public void Drive(double target)
    {
        var targetAngularVelocity = Math.Clamp(target, -1, 1) * MaxAngularVelocity;
        var error = targetAngularVelocity - RelativeAngularVelocity;
        var torque = (float)Math.Clamp(error * _velocityGain, -MaxTorque, MaxTorque);
        OtherBeam.ApplyTorque(torque);
        ReferenceBeam.ApplyTorque(-torque);
        LastAppliedTorque = torque;
    }
}
