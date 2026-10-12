using NodeRunner.Domain;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The parts <see cref="CreatureParts"/> shows: a saved creature's, or Build's while it is still
/// being drawn and need not be a whole creature yet.
/// </summary>
public sealed record CreatureShape(
    IReadOnlyList<NodeDef> Nodes,
    IReadOnlyList<BeamDef> Beams,
    IReadOnlyList<ServoDef> Servos,
    IReadOnlyList<PistonDef> Pistons,
    IReadOnlyList<SpringDef> Springs,
    IReadOnlyList<SensorDef> Sensors,
    IReadOnlyList<WheelDef> Wheels,
    IReadOnlyList<TouchSensorDef> TouchSensors,
    IReadOnlyList<RubberDef> Rubbers,
    IReadOnlyList<VelocityMotorDef> VelocityMotors,
    IReadOnlyList<BrakeDef> Brakes)
{
    /// <summary>Every joint motor, the Servos then the Velocity motors (#454), as <see cref="CreatureDef.JointMotors"/>.</summary>
    public IReadOnlyList<IJointMotorDef> JointMotors => [.. Servos, .. VelocityMotors];

    /// <summary>What sits in each joint's motor-or-brake slot (#1044): the joint motors, then the Brakes (#1041).</summary>
    public IReadOnlyList<IMotorOrBrakeDef> MotorsAndBrakes => [.. JointMotors, .. Brakes];
}
