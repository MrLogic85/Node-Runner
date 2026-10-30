using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.ML;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>One creature with every part kind, and a direct network that fits it.</summary>
internal static class PortedCreature
{
    public const int Accelerometer = 8;
    public const int Camera = 9;
    public const int Servo = 10;
    public const int Piston = 11;
    public const int Spring = 12;
    public const int Wheel = 13;
    public const int Touch = 14;
    public const int Rubber = 15;
    public const int VelocityMotor = 16;
    public const int Brake = 17;
    public const int AngleSensor = 18;
    public const int Stepper = 22;
    public const int MemoryCell = 23;

    public static CreatureDef Def { get; } = new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0)), new NodeDef(3, new Vector2D(4, 0)), new NodeDef(4, new Vector2D(0, 2)), new NodeDef(19, new Vector2D(2, 2))],
        [new BeamDef(5, 1, 2), new BeamDef(6, 2, 3), new BeamDef(7, 1, 4), new BeamDef(20, 4, 19), new BeamDef(21, 2, 19)],
        [new SensorDef(Accelerometer, 5, SensorKind.Accelerometer), new SensorDef(Camera, 6, SensorKind.Camera, "Eye", rays: 5)],
        [new ServoDef(Servo, 1, 5, 7, "Hip")],
        [new PistonDef(Piston, 1, 3, "Ram")],
        [new SpringDef(Spring, 2, 4)],
        [new WheelDef(Wheel, 3)],
        [new TouchSensorDef(Touch, 2)],
        [new RubberDef(Rubber, 3)],
        [new VelocityMotorDef(VelocityMotor, 2, 5, 6, "Drive")],
        [new BrakeDef(Brake, 3, 6, Wheel)],
        [new AngleSensorDef(AngleSensor, 4, 7, Spring, "Knee")],
        [new StepperDef(Stepper, 19, 20, 21, "Ratchet")],
        [new MemoryCellDef(MemoryCell, 7)],
        nextPartId: 24);

    public static NetworkPortLayout Layout { get; } = NetworkPorts.Of(Def);

    /// <summary>Every port in layout order, with the part's kind and shown name, and the reading's word and range.</summary>
    public static IReadOnlyList<(NetworkPort Port, PartKind Kind, UiText Name, string Reading, PortRange Range)> Ports { get; } = Table();

    public static NeuralNetwork Network()
    {
        int[] layers = [Layout.Inputs.Count, Layout.Outputs.Count];
        var genome = Enumerable.Range(0, NeuralNetwork.GenomeLength(layers)).Select(gene => Math.Sin(gene) * 0.5).ToArray();
        return NeuralNetwork.FromGenome(layers, genome, Activation.Tanh);
    }

    public static double[] Readings() => Enumerable.Range(0, Layout.Inputs.Count).Select(input => (input % 5 * 0.4) - 0.8).ToArray();

    private static (NetworkPort, PartKind, UiText, string, PortRange)[] Table()
    {
        const PortRange signed = PortRange.MinusOneToOne;
        const PortRange unsigned = PortRange.ZeroToOne;
        var accelerometer = UiText.Format("Accel {0}", 1);
        var eye = UiText.AsWritten("Eye");
        var hip = UiText.AsWritten("Hip");
        var ram = UiText.AsWritten("Ram");
        var touch = UiText.Format("Touch {0}", 1);
        var drive = UiText.AsWritten("Drive");
        var knee = UiText.AsWritten("Knee");
        var ratchet = UiText.AsWritten("Ratchet");
        var memory = UiText.Format("Memory {0}", 1);
        return
        [
            (NetworkPort.Input(Accelerometer, "along"), PartKind.Accelerometer, accelerometer, "along", signed),
            (NetworkPort.Input(Accelerometer, "across"), PartKind.Accelerometer, accelerometer, "across", signed),
            (NetworkPort.Input(Camera, "left2"), PartKind.Camera, eye, "far left", unsigned),
            (NetworkPort.Input(Camera, "left1"), PartKind.Camera, eye, "left", unsigned),
            (NetworkPort.Input(Camera, "centre"), PartKind.Camera, eye, "centre", unsigned),
            (NetworkPort.Input(Camera, "right1"), PartKind.Camera, eye, "right", unsigned),
            (NetworkPort.Input(Camera, "right2"), PartKind.Camera, eye, "far right", unsigned),
            (NetworkPort.Input(Camera, "hit"), PartKind.Camera, eye, "hit", unsigned),
            (NetworkPort.Input(Servo, NetworkPorts.ServoAngleChannel), PartKind.Servo, hip, "angle", signed),
            (NetworkPort.Input(Servo, NetworkPorts.ServoSpeedChannel), PartKind.Servo, hip, "speed", signed),
            (NetworkPort.Input(Piston, NetworkPorts.PistonLengthChannel), PartKind.Piston, ram, "length", unsigned),
            (NetworkPort.Input(Piston, NetworkPorts.PistonSpeedChannel), PartKind.Piston, ram, "speed", signed),
            (NetworkPort.Input(Touch, NetworkPorts.TouchChannel), PartKind.TouchSensor, touch, "touch", unsigned),
            (NetworkPort.Input(VelocityMotor, NetworkPorts.VelocityMotorAngleSinChannel), PartKind.VelocityMotor, drive, "angle sin", signed),
            (NetworkPort.Input(VelocityMotor, NetworkPorts.VelocityMotorAngleCosChannel), PartKind.VelocityMotor, drive, "angle cos", signed),
            (NetworkPort.Input(VelocityMotor, NetworkPorts.VelocityMotorSpeedChannel), PartKind.VelocityMotor, drive, "speed", signed),
            (NetworkPort.Input(AngleSensor, NetworkPorts.AngleSensorAngleSinChannel), PartKind.AngleSensor, knee, "angle sin", signed),
            (NetworkPort.Input(AngleSensor, NetworkPorts.AngleSensorAngleCosChannel), PartKind.AngleSensor, knee, "angle cos", signed),
            (NetworkPort.Input(AngleSensor, NetworkPorts.AngleSensorSpeedChannel), PartKind.AngleSensor, knee, "speed", signed),
            (NetworkPort.Input(Stepper, NetworkPorts.StepperAngleSinChannel), PartKind.Stepper, ratchet, "angle sin", signed),
            (NetworkPort.Input(Stepper, NetworkPorts.StepperAngleCosChannel), PartKind.Stepper, ratchet, "angle cos", signed),
            (NetworkPort.Input(Stepper, NetworkPorts.StepperTickChannel), PartKind.Stepper, ratchet, "tick", unsigned),
            (NetworkPort.Input(MemoryCell, NetworkPorts.MemoryCellValueChannel), PartKind.MemoryCell, memory, "value", signed),
            (Layout.Outputs[0], PartKind.Servo, hip, "angle", signed),
            (Layout.Outputs[1], PartKind.Servo, hip, "strength", unsigned),
            (Layout.Outputs[2], PartKind.Piston, ram, "length", unsigned),
            (Layout.Outputs[3], PartKind.Piston, ram, "strength", unsigned),
            (Layout.Outputs[4], PartKind.VelocityMotor, drive, "speed", signed),
            (Layout.Outputs[5], PartKind.VelocityMotor, drive, "strength", unsigned),
            (Layout.Outputs[6], PartKind.Stepper, ratchet, "frequency", signed),
            (Layout.Outputs[7], PartKind.MemoryCell, memory, "target", signed),
            (Layout.Outputs[8], PartKind.MemoryCell, memory, "write", unsigned),
        ];
    }
}
