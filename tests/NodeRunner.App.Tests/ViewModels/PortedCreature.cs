using NodeRunner.Domain;
using NodeRunner.ML;

namespace NodeRunner.App.Tests.ViewModels;

/// <summary>One creature with every part kind that has brain ports, and a direct brain that fits it.</summary>
internal static class PortedCreature
{
    public const int Accelerometer = 8;
    public const int Camera = 9;
    public const int Servo = 10;
    public const int Piston = 11;
    public const int Spring = 12;
    public const int Wheel = 13;

    public static CreatureDef Def { get; } = new(
        [new NodeDef(1, new Vector2D(0, 0)), new NodeDef(2, new Vector2D(2, 0)), new NodeDef(3, new Vector2D(4, 0)), new NodeDef(4, new Vector2D(0, 2))],
        [new BeamDef(5, 1, 2), new BeamDef(6, 2, 3), new BeamDef(7, 1, 4)],
        [new SensorDef(Accelerometer, 5, SensorKind.Accelerometer), new SensorDef(Camera, 6, SensorKind.Camera, "Eye", rays: 5)],
        [new ServoDef(Servo, 1, 5, 7, "Hip")],
        [new PistonDef(Piston, 1, 3, "Ram")],
        [new SpringDef(Spring, 2, 4)],
        [new WheelDef(Wheel, 3)],
        nextPartId: 14);

    public static BrainPortLayout Layout { get; } = BrainPorts.Of(Def);

    public static NeuralNetwork Brain()
    {
        int[] layers = [Layout.Inputs.Count, Layout.Outputs.Count];
        var genome = Enumerable.Range(0, NeuralNetwork.GenomeLength(layers)).Select(gene => Math.Sin(gene) * 0.5).ToArray();
        return NeuralNetwork.FromGenome(layers, genome, Activation.Tanh);
    }

    public static double[] Readings() => Enumerable.Range(0, Layout.Inputs.Count).Select(input => (input % 5 * 0.4) - 0.8).ToArray();
}
