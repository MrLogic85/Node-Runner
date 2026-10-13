using NodeRunner.Domain;

namespace NodeRunner.Creature;

/// <summary>
/// A Velocity motor (#454) between the two <see cref="HingePartSides"/> at its joint: it drives the
/// Target's speed against the Fixed side toward the brain's speed, round and round with no end
/// stops. At zero strength it pushes nothing, so the joint turns freely.
/// </summary>
public sealed class VelocityMotorJoint
{
    private Mechanics.MotorState _motor;

    public VelocityMotorJoint(VelocityMotorDef definition, HingePartSides sides)
    {
        Definition = definition;
        Sides = sides;
    }

    public VelocityMotorDef Definition { get; }

    public HingePartSides Sides { get; }

    /// <summary>The angle between the sides as its sine and cosine (#1207), which never jump across a whole turn.</summary>
    public (double Sin, double Cos) AngleInputs => Mechanics.VelocityMotor.AngleInputs(Sides.ContinuousAngle);

    public double SpeedInput => Mechanics.MotorLoop.SpeedInput(Sides.Speed, Definition.MaxSpeed);

    /// <param name="speedOutput">The brain's speed output, −1…1 of its Max speed setting, counter-clockwise positive.</param>
    /// <param name="strength">The brain's strength output, 0…1 of its Max strength setting.</param>
    /// <param name="step">The physics step, in seconds.</param>
    public void Drive(double speedOutput, double strength, double step)
    {
        _motor = Mechanics.VelocityMotor.NextMotor(Definition, Sides.Speed, speedOutput, strength, _motor, step, Sides.EffectiveInertia);
        Sides.ApplyTorque(_motor.Torque);
    }
}
