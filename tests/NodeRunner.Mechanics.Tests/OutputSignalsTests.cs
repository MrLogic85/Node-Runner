using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class OutputSignalsTests
{
    [Fact]
    public void PassiveStrength_HasNoDeadZone()
    {
        static double Sigmoid(double x) => 1 / (1 + Math.Exp(-x));

        OutputSignals.StrengthFromOutput(Sigmoid(PortSignals.PassiveStrengthBias + 0.1), 100)
            .ShouldBeGreaterThan(OutputSignals.StrengthFromOutput(Sigmoid(PortSignals.PassiveStrengthBias), 100));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.25, 25)]
    [InlineData(1, 100)]
    [InlineData(2, 100)]
    public void StrengthFromOutput_IsAShareOfTheSetting(double output, double expected) =>
        OutputSignals.StrengthFromOutput(output, 100).ShouldBe(expected);

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 2)]
    [InlineData(1, 3)]
    [InlineData(0.5, 2.5)]
    [InlineData(-5, 1)]
    public void PositionFromTarget_Centred_IsLinear(double target, double expected) =>
        OutputSignals.PositionFromTarget(target, min: 1, built: 2, max: 3).ShouldBe(expected);

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(-0.5, 1.25)]
    [InlineData(0, 1.5)]
    [InlineData(0.5, 2.25)]
    [InlineData(1, 3)]
    public void PositionFromTarget_OffCentre_KeepsZeroAtTheBuiltPose(double target, double expected) =>
        OutputSignals.PositionFromTarget(target, min: 1, built: 1.5, max: 3).ShouldBe(expected);

    [Fact]
    public void PositionFromTarget_BuiltOutsideTheRange_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => OutputSignals.PositionFromTarget(0, min: 1, built: 4, max: 3));
}
