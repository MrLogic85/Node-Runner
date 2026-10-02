namespace NodeRunner.Domain.Tests;

public sealed class PortSignalsTests
{
    [Theory]
    [InlineData(PortSignal.Reading, NeuronActivation.Identity)]
    [InlineData(PortSignal.Velocity, NeuronActivation.Tanh)]
    [InlineData(PortSignal.Position, NeuronActivation.Tanh)]
    [InlineData(PortSignal.Strength, NeuronActivation.Sigmoid)]
    public void Activation_MatchesThePhysicalSignal(PortSignal signal, NeuronActivation activation) =>
        PortSignals.Activation(signal).ShouldBe(activation);

    [Theory]
    [InlineData(PortSignal.Reading, 0)]
    [InlineData(PortSignal.Velocity, 0)]
    [InlineData(PortSignal.Position, 0)]
    [InlineData(PortSignal.Strength, -4)]
    public void PassiveBias_OnlyHoldsStrengthDown(PortSignal signal, double bias) =>
        PortSignals.PassiveBias(signal).ShouldBe(bias);

    [Fact]
    public void PassiveStrength_IsAboutTwoPercent_WithNoDeadZone()
    {
        static double Sigmoid(double x) => 1 / (1 + Math.Exp(-x));

        Sigmoid(PortSignals.PassiveStrengthBias).ShouldBe(0.018, tolerance: 0.001);
        PortSignals.StrengthFromOutput(Sigmoid(PortSignals.PassiveStrengthBias + 0.1), 100)
            .ShouldBeGreaterThan(PortSignals.StrengthFromOutput(Sigmoid(PortSignals.PassiveStrengthBias), 100));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.25, 25)]
    [InlineData(1, 100)]
    [InlineData(2, 100)]
    public void StrengthFromOutput_IsAShareOfTheSetting(double output, double expected) =>
        PortSignals.StrengthFromOutput(output, 100).ShouldBe(expected);

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 2)]
    [InlineData(1, 3)]
    [InlineData(0.5, 2.5)]
    [InlineData(-5, 1)]
    public void PositionFromTarget_Centred_IsLinear(double target, double expected) =>
        PortSignals.PositionFromTarget(target, min: 1, built: 2, max: 3).ShouldBe(expected);

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(-0.5, 1.25)]
    [InlineData(0, 1.5)]
    [InlineData(0.5, 2.25)]
    [InlineData(1, 3)]
    public void PositionFromTarget_OffCentre_KeepsZeroAtTheBuiltPose(double target, double expected) =>
        PortSignals.PositionFromTarget(target, min: 1, built: 1.5, max: 3).ShouldBe(expected);

    [Fact]
    public void PositionFromTarget_BuiltOutsideTheRange_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => PortSignals.PositionFromTarget(0, min: 1, built: 4, max: 3));

    [Fact]
    public void Output_CannotCarryAReading() =>
        Should.Throw<ArgumentOutOfRangeException>(() => BrainPort.Output(1, "x", PortSignal.Reading));
}
