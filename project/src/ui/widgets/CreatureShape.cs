using Godot;
using NodeRunner.Domain;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The parts <see cref="CreatureParts"/> shows: a saved creature's, or Build's while it is still
/// being drawn and need not be a whole creature yet. It takes them as one list, and each kind's
/// typed list keeps their order, as <see cref="CreatureDef"/>'s do.
/// </summary>
public sealed class CreatureShape
{
    public CreatureShape(IEnumerable<IPartDef> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        Dictionary<int, IPartDef> byId = [];
        List<NodeDef> nodes = [];
        List<BeamDef> beams = [];
        List<ServoDef> servos = [];
        List<PistonDef> pistons = [];
        List<SpringDef> springs = [];
        List<SensorDef> sensors = [];
        List<WheelDef> wheels = [];
        List<TouchSensorDef> touchSensors = [];
        List<RubberDef> rubbers = [];
        List<VelocityMotorDef> velocityMotors = [];
        List<BrakeDef> brakes = [];
        List<AngleSensorDef> angleSensors = [];
        List<StepperDef> steppers = [];
        foreach (var part in parts)
        {
            byId[part.Id] = part;
            switch (part)
            {
                case NodeDef node:
                    nodes.Add(node);
                    break;
                case BeamDef beam:
                    beams.Add(beam);
                    break;
                case ServoDef servo:
                    servos.Add(servo);
                    break;
                case PistonDef piston:
                    pistons.Add(piston);
                    break;
                case SpringDef spring:
                    springs.Add(spring);
                    break;
                case SensorDef sensor:
                    sensors.Add(sensor);
                    break;
                case WheelDef wheel:
                    wheels.Add(wheel);
                    break;
                case TouchSensorDef touch:
                    touchSensors.Add(touch);
                    break;
                case RubberDef rubber:
                    rubbers.Add(rubber);
                    break;
                case VelocityMotorDef motor:
                    velocityMotors.Add(motor);
                    break;
                case BrakeDef brake:
                    brakes.Add(brake);
                    break;
                case AngleSensorDef angleSensor:
                    angleSensors.Add(angleSensor);
                    break;
                case StepperDef stepper:
                    steppers.Add(stepper);
                    break;
                default:
                    GD.PushError($"{part.PartKind} {part.Id} has no list in CreatureShape and is not drawn.");
                    break;
            }
        }

        PartById = byId;
        Nodes = nodes;
        Beams = beams;
        Servos = servos;
        Pistons = pistons;
        Springs = springs;
        Sensors = sensors;
        Wheels = wheels;
        TouchSensors = touchSensors;
        Rubbers = rubbers;
        VelocityMotors = velocityMotors;
        Brakes = brakes;
        AngleSensors = angleSensors;
        Steppers = steppers;
    }

    public IReadOnlyDictionary<int, IPartDef> PartById { get; }

    public IReadOnlyList<NodeDef> Nodes { get; }

    public IReadOnlyList<BeamDef> Beams { get; }

    public IReadOnlyList<ServoDef> Servos { get; }

    public IReadOnlyList<PistonDef> Pistons { get; }

    public IReadOnlyList<SpringDef> Springs { get; }

    public IReadOnlyList<SensorDef> Sensors { get; }

    public IReadOnlyList<WheelDef> Wheels { get; }

    public IReadOnlyList<TouchSensorDef> TouchSensors { get; }

    public IReadOnlyList<RubberDef> Rubbers { get; }

    public IReadOnlyList<VelocityMotorDef> VelocityMotors { get; }

    public IReadOnlyList<BrakeDef> Brakes { get; }

    public IReadOnlyList<AngleSensorDef> AngleSensors { get; }

    public IReadOnlyList<StepperDef> Steppers { get; }

    /// <summary>Every joint motor, the Servos, the Velocity motors, then the Steppers, as <see cref="CreatureDef.JointMotors"/>.</summary>
    public IReadOnlyList<IJointMotorDef> JointMotors => [.. Servos, .. VelocityMotors, .. Steppers];

    /// <summary>
    /// The joint motors, Brakes and Angle sensors, as <see cref="CreatureDef.HingeParts"/>:
    /// the slot parts drawn in place of their joint's ring.
    /// </summary>
    public IReadOnlyList<IHingePartDef> HingeParts => [.. JointMotors, .. Brakes, .. AngleSensors];

    /// <summary>Every part on a joint's slots: the hinge parts, Wheels, Touch sensors and Rubbers.</summary>
    public IReadOnlyList<IJointPartDef> JointParts => [.. HingeParts, .. Wheels, .. TouchSensors, .. Rubbers];
}
