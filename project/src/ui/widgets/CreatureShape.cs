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
    IReadOnlyList<BrakeDef> Brakes,
    IReadOnlyList<AngleSensorDef> AngleSensors)
{
    /// <summary>Every joint motor, the Servos then the Velocity motors (#454), as <see cref="CreatureDef.JointMotors"/>.</summary>
    public IReadOnlyList<IJointMotorDef> JointMotors => [.. Servos, .. VelocityMotors];

    /// <summary>
    /// The joint motors, then the Brakes (#1041): the slot parts drawn in place of their joint's ring. An
    /// Angle sensor shares the slot (#1204) but is left out until its picture (#1205), so its joint draws plain.
    /// </summary>
    public IReadOnlyList<IMotorOrBrakeDef> MotorsAndBrakes => [.. JointMotors, .. Brakes];
}
