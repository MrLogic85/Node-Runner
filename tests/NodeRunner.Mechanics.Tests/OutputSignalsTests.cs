using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class OutputSignalsTests
{
    // A share of the setting, clamped, with no dead zone at the passive bias.
    [Fact]
    public void StrengthFromOutput_IsAShareOfTheSetting()
    {
        foreach (var (output, strength) in new[] { (0.0, 0.0), (0.25, 25), (1, 100), (2, 100) })
        {
            OutputSignals.StrengthFromOutput(output, 100).ShouldBe(strength);
        }

        static double Sigmoid(double x) => 1 / (1 + Math.Exp(-x));
        OutputSignals.StrengthFromOutput(Sigmoid(PortSignals.PassiveStrengthBias + 0.1), 100)
            .ShouldBeGreaterThan(OutputSignals.StrengthFromOutput(Sigmoid(PortSignals.PassiveStrengthBias), 100));
    }

    // Linear between the limits 1 and 3, clamped, and 0 at the built pose even off centre.
    [Fact]
    public void PositionFromTarget_KeepsZeroAtTheBuiltPose()
    {
        foreach (var (built, target, position) in new[]
        {
            (2.0, -1.0, 1.0), (2, 0, 2), (2, 1, 3), (2, 0.5, 2.5), (2, -5, 1),
            (1.5, -1, 1), (1.5, -0.5, 1.25), (1.5, 0, 1.5), (1.5, 0.5, 2.25), (1.5, 1, 3),
        })
        {
            OutputSignals.PositionFromTarget(target, min: 1, built: built, max: 3).ShouldBe(position, $"built {built}, target {target}");
        }

        Should.Throw<ArgumentOutOfRangeException>(() => OutputSignals.PositionFromTarget(0, min: 1, built: 4, max: 3));
    }
}
