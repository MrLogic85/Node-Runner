namespace NodeRunner.Domain;

/// <summary>
/// The concrete kind of a creature part (#1221): what <see cref="CreatureElementKind"/> names, with a
/// sensor told apart as an Accelerometer or a Camera. <see cref="PartKinds"/> describes each one.
/// </summary>
public enum PartKind
{
    Node,
    Beam,
    Accelerometer,
    Camera,
    Servo,
    Piston,
    Spring,
    Wheel,
    TouchSensor,
    Rubber,
    VelocityMotor,
    Brake,
    AngleSensor,
}
