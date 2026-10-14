using NodeRunner.Domain;

namespace NodeRunner.Creature;

/// <summary>
/// A Servo (#452) between the two <see cref="HingePartSides"/> at its joint: it drives the Target
/// toward the brain's angle, with a soft stop outside its range.
/// </summary>
public sealed class ServoJoint
{
    private Mechanics.MotorState _motor;

    public ServoJoint(ServoDef definition, HingePartSides sides)
    {
        Definition = definition;
        Sides = sides;
    }

    public ServoDef Definition { get; }

    public HingePartSides Sides { get; }

    /// <summary>
    /// Whether its Fixed or Target beam has torn at either end (#962): it then drives nothing, and its
    /// brain inputs keep the values they last had (Creature.Tears).
    /// </summary>
    public bool IsPassive { get; private set; }

    public void GoPassive() => IsPassive = true;

    public double AngleInput => Mechanics.Servo.AngleInput(Sides.ContinuousAngle, Definition, Sides.BuiltAngle);

    public double SpeedInput => Mechanics.MotorLoop.SpeedInput(Sides.Speed, Definition.MaxSpeed);

    /// <param name="position">The brain's angle output, −1 lower end … +1 upper end.</param>
    /// <param name="strength">The brain's strength output, 0…1 of its Max strength setting.</param>
    /// <param name="step">The physics step, in seconds.</param>
    public void Drive(double position, double strength, double step)
    {
        if (IsPassive)
        {
            return;
        }

        var angle = Sides.ContinuousAngle;
        var speed = Sides.Speed;
        var effectiveInertia = Sides.EffectiveInertia;
        _motor = Mechanics.Servo.NextMotor(Definition, Sides.BuiltAngle, angle, speed, position, strength, _motor, step, effectiveInertia);
        // Godot's angular joints cannot uniformly limit Beam, Piston and Spring link directions.
        // The soft stop is explicit-step stable by capping its gains to the live effective inertia.
        var torque = _motor.Torque + Mechanics.Servo.EndStopTorque(Definition, Sides.BuiltAngle, angle, speed, effectiveInertia, step);
        Sides.ApplyTorque(torque);
    }
}
