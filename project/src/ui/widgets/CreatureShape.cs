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
    /// The Servos, Velocity motors, Brakes (#1041) and Angle sensors (#1204, #1205): the slot parts drawn
    /// in place of their joint's ring. A Stepper has no picture yet (#1038), so its joint keeps its ring,
    /// at the joint motor's radius.
    /// </summary>
    public IReadOnlyList<IHingePartDef> HingeParts => [.. Servos, .. VelocityMotors, .. Brakes, .. AngleSensors];

    /// <summary>Every part on a joint's slots (#1221): the hinge parts, Steppers included, Wheels, Touch sensors and Rubbers.</summary>
    public IReadOnlyList<IJointPartDef> JointParts => [.. HingeParts, .. Steppers, .. Wheels, .. TouchSensors, .. Rubbers];
}
