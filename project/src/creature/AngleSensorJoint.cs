using NodeRunner.Domain;

namespace NodeRunner.Creature;

/// <summary>
/// An Angle sensor (#1204) between the two <see cref="HingePartSides"/> at its joint: it reads the
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

    /// <summary>The angle between the sides as its sine and cosine, the same as a Velocity motor on those sides reads.</summary>
    public (double Sin, double Cos) AngleInputs => Mechanics.AngleSensor.AngleInputs(Sides.ContinuousAngle);

    public double SpeedInput => Mechanics.AngleSensor.SpeedInput(Sides.Speed);
}
