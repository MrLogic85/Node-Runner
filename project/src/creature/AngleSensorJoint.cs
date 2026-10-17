using NodeRunner.Domain;

namespace NodeRunner.Creature;

/// <summary>
/// An Angle sensor between the two <see cref="HingePartSides"/> at its joint: it reads the
/// angle between the sides and how fast it changes, as a Velocity motor does, and applies no force.
/// </summary>
public sealed class AngleSensorJoint
{
    public AngleSensorJoint(AngleSensorDef definition, HingePartSides sides)
    {
        Definition = definition;
        Sides = sides;
    }

    public AngleSensorDef Definition { get; }

    public HingePartSides Sides { get; }

    /// <summary>
    /// Whether its Fixed or Target beam has torn at either end: its network inputs then keep the
    /// values they last had (Creature.Tears).
    /// </summary>
    public bool IsPassive { get; private set; }

    public void GoPassive() => IsPassive = true;

    /// <summary>The angle between the sides as its sine and cosine, the same as a Velocity motor on those sides reads.</summary>
    public (double Sin, double Cos) AngleInputs => Mechanics.AngleSensor.AngleInputs(Sides.ContinuousAngle);

    public double SpeedInput => Mechanics.AngleSensor.SpeedInput(Sides.Speed);
}
