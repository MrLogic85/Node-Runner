namespace NodeRunner.Domain;

/// <summary>
/// A joint motor: a Servo (#452) or a Velocity motor (#454), the powered <see cref="IMotorOrBrakeDef"/>.
/// It turns its Target side against its Fixed side. See <see cref="JointMotor"/>.
/// </summary>
public interface IJointMotorDef : IMotorOrBrakeDef
{
    /// <summary>Which kind of motor this is, as it is selected.</summary>
    CreatureElementKind Kind { get; }
}
