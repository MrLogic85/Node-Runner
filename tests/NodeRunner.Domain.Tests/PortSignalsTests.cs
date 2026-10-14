namespace NodeRunner.Domain.Tests;

public sealed class PortSignalsTests
{
    [Theory]
    [InlineData(PortSignal.Reading, NeuronActivation.Identity)]
    [InlineData(PortSignal.Velocity, NeuronActivation.Tanh)]
    [InlineData(PortSignal.Position, NeuronActivation.Tanh)]
    [InlineData(PortSignal.Length, NeuronActivation.Sigmoid)]
    [InlineData(PortSignal.Strength, NeuronActivation.Sigmoid)]
    [InlineData(PortSignal.Frequency, NeuronActivation.Sigmoid)]
    [InlineData(PortSignal.Power, NeuronActivation.Tanh)]
    public void Activation_MatchesThePhysicalSignal(PortSignal signal, NeuronActivation activation) =>
        PortSignals.Activation(signal).ShouldBe(activation);

    [Theory]
    [InlineData(PortSignal.Reading, 0)]
    [InlineData(PortSignal.Velocity, 0)]
    [InlineData(PortSignal.Position, 0)]
    [InlineData(PortSignal.Length, 0)]
    [InlineData(PortSignal.Strength, -4)]
    [InlineData(PortSignal.Frequency, -4)]
    [InlineData(PortSignal.Power, 0)]
    public void PassiveBias_HoldsStrengthAndFrequencyDown(PortSignal signal, double bias) =>
        PortSignals.PassiveBias(signal).ShouldBe(bias);

    [Fact]
    public void PassiveStrength_IsAboutTwoPercent()
    {
        static double Sigmoid(double x) => 1 / (1 + Math.Exp(-x));

        Sigmoid(PortSignals.PassiveStrengthBias).ShouldBe(0.018, tolerance: 0.001);
    }

    [Fact]
    public void Output_CannotCarryAReading() =>
        Should.Throw<ArgumentOutOfRangeException>(() => BrainPort.Output(1, "x", PortSignal.Reading));
}
