using NodeRunner.Domain;

namespace NodeRunner.Creature;

/// <summary>
/// A Velocity motor (#454) between the two <see cref="MotorSides"/> at its joint: it drives the
/// Target's speed against the Fixed side toward the brain's speed, round and round with no end
/// stops. At zero strength it pushes nothing, so the joint turns freely.
/// </summary>
public sealed class VelocityMotorJoint
{
    private Mechanics.ServoMotor _motor;

    public VelocityMotorJoint(VelocityMotorDef definition, MotorSides sides)
    {
        Definition = definition;
        Sides = sides;
    }

    public VelocityMotorDef Definition { get; }

    public MotorSides Sides { get; }

    /// <summary>The turn from the built angle as its sine and cosine, which never jump across a whole turn.</summary>
    public (double Sin, double Cos) AngleInputs => Mechanics.VelocityMotor.AngleInputs(Sides.ContinuousAngle, Sides.BuiltAngle);

    public double SpeedInput => Mechanics.VelocityMotor.SpeedInput(Sides.Speed, Definition.MaxSpeed);

    /// <param name="speedOutput">The brain's speed output, −1…1 of its Max speed setting, counter-clockwise positive.</param>
    /// <param name="strength">The brain's strength output, 0…1 of its Max strength setting.</param>
    /// <param name="step">The physics step, in seconds.</param>
    public void Drive(double speedOutput, double strength, double step)
    {
        _motor = Mechanics.VelocityMotor.NextMotor(Definition, Sides.Speed, speedOutput, strength, _motor, step, Sides.EffectiveInertia);
        Sides.ApplyTorque(_motor.Torque);
    }
}
