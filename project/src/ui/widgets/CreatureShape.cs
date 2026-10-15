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
    IReadOnlyList<AngleSensorDef> AngleSensors,
    IReadOnlyList<StepperDef> Steppers)
{
    /// <summary>Every joint motor, the Servos, the Velocity motors (#454), then the Steppers (#1036), as <see cref="CreatureDef.JointMotors"/>.</summary>
    public IReadOnlyList<IJointMotorDef> JointMotors => [.. Servos, .. VelocityMotors, .. Steppers];

    /// <summary>
    /// The joint motors, Brakes (#1041) and Angle sensors (#1204, #1205), as <see cref="CreatureDef.HingeParts"/>:
    /// the slot parts drawn in place of their joint's ring.
    /// </summary>
    public IReadOnlyList<IHingePartDef> HingeParts => [.. JointMotors, .. Brakes, .. AngleSensors];

    /// <summary>Every part on a joint's slots (#1221): the hinge parts, Wheels, Touch sensors and Rubbers.</summary>
    public IReadOnlyList<IJointPartDef> JointParts => [.. HingeParts, .. Wheels, .. TouchSensors, .. Rubbers];
}
