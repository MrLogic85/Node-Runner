namespace NodeRunner.Domain;

/// <summary>
/// A joint motor: a Servo (#452) or a Velocity motor (#454), the powered <see cref="IHingePartDef"/>.
/// It turns its Target side against its Fixed side. See <see cref="JointMotor"/>.
/// </summary>
public interface IJointMotorDef : IHingePartDef
{
}
