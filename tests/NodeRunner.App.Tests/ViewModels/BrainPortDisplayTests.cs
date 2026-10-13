using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BrainPortDisplayTests
{
    [Theory]
    [InlineData(PartSettingsKind.Accelerometer, PortDirection.Input, "along", PortRange.MinusOneToOne, "along")]
    [InlineData(PartSettingsKind.Accelerometer, PortDirection.Input, "across", PortRange.MinusOneToOne, "across")]
    [InlineData(PartSettingsKind.Camera, PortDirection.Input, "left2", PortRange.ZeroToOne, "far left")]
    [InlineData(PartSettingsKind.Camera, PortDirection.Input, "left1", PortRange.ZeroToOne, "left")]
    [InlineData(PartSettingsKind.Camera, PortDirection.Input, "centre", PortRange.ZeroToOne, "centre")]
    [InlineData(PartSettingsKind.Camera, PortDirection.Input, "right1", PortRange.ZeroToOne, "right")]
    [InlineData(PartSettingsKind.Camera, PortDirection.Input, "right2", PortRange.ZeroToOne, "far right")]
    [InlineData(PartSettingsKind.Camera, PortDirection.Input, "hit", PortRange.ZeroToOne, "hit")]
    [InlineData(PartSettingsKind.Piston, PortDirection.Input, "length", PortRange.ZeroToOne, "length")]
    [InlineData(PartSettingsKind.Piston, PortDirection.Input, "speed", PortRange.MinusOneToOne, "speed")]
    [InlineData(PartSettingsKind.Piston, PortDirection.Output, "position", PortRange.ZeroToOne, "length")]
    [InlineData(PartSettingsKind.Piston, PortDirection.Output, "strength", PortRange.ZeroToOne, "strength")]
    [InlineData(PartSettingsKind.Servo, PortDirection.Input, "angle", PortRange.MinusOneToOne, "angle")]
    [InlineData(PartSettingsKind.Servo, PortDirection.Input, "speed", PortRange.MinusOneToOne, "speed")]
    [InlineData(PartSettingsKind.Servo, PortDirection.Output, "angle", PortRange.MinusOneToOne, "angle")]
    [InlineData(PartSettingsKind.Servo, PortDirection.Output, "strength", PortRange.ZeroToOne, "strength")]
    [InlineData(PartSettingsKind.Touch, PortDirection.Input, "touch", PortRange.ZeroToOne, "touch")]
    [InlineData(PartSettingsKind.VelocityMotor, PortDirection.Input, "angleSin", PortRange.MinusOneToOne, "angle sin")]
    [InlineData(PartSettingsKind.VelocityMotor, PortDirection.Input, "angleCos", PortRange.MinusOneToOne, "angle cos")]
    [InlineData(PartSettingsKind.VelocityMotor, PortDirection.Input, "speed", PortRange.MinusOneToOne, "speed")]
    [InlineData(PartSettingsKind.VelocityMotor, PortDirection.Output, "speed", PortRange.MinusOneToOne, "speed")]
    [InlineData(PartSettingsKind.VelocityMotor, PortDirection.Output, "strength", PortRange.ZeroToOne, "strength")]
    [InlineData(PartSettingsKind.AngleSensor, PortDirection.Input, "angleSin", PortRange.MinusOneToOne, "angle sin")]
    [InlineData(PartSettingsKind.AngleSensor, PortDirection.Input, "angleCos", PortRange.MinusOneToOne, "angle cos")]
    [InlineData(PartSettingsKind.AngleSensor, PortDirection.Input, "speed", PortRange.MinusOneToOne, "speed")]
    public void EveryPort_HasItsRangeAndShortLabel(PartSettingsKind kind, PortDirection direction, string channel, PortRange range, string label)
    {
        var port = new BrainPort(1, channel, direction, direction == PortDirection.Input ? PortSignal.Reading : PortSignal.Position);

        BrainPortDisplay.Range(kind, port).ShouldBe(range);
        BrainPortDisplay.ShortLabel(kind, port).ShouldBe(UiText.Plain(label));
    }

    [Fact]
    public void EveryPortOfEveryPartKind_IsCovered()
    {
        var layout = PortedCreature.Layout;

        foreach (var port in layout.Inputs.Concat(layout.Outputs))
        {
            var kind = KindOf(port.PartId);
            Should.NotThrow(() => BrainPortDisplay.Range(kind, port));
            Should.NotThrow(() => BrainPortDisplay.ShortLabel(kind, port));
        }

        layout.Inputs.Count.ShouldBe(19);
        layout.Outputs.Count.ShouldBe(6);
    }

    // The bar draws the value as it leaves the brain, so each output's range must be its activation's.
    [Fact]
    public void EveryOutputsRange_IsItsActivationsRange()
    {
        foreach (var port in PortedCreature.Layout.Outputs)
        {
            var range = PortSignals.Activation(port.Signal) == NeuronActivation.Sigmoid ? PortRange.ZeroToOne : PortRange.MinusOneToOne;

            BrainPortDisplay.Range(KindOf(port.PartId), port).ShouldBe(range, port.ToString());
        }
    }

    [Fact]
    public void APistonsTargetLength_IsShownLikeItsMeasuredLength()
    {
        var target = BrainPorts.PistonOutputs(1).First();

        BrainPortDisplay.Range(PartSettingsKind.Piston, target).ShouldBe(BrainPortDisplay.Range(PartSettingsKind.Piston, BrainPort.Input(1, BrainPorts.PistonLengthChannel)));
    }

    [Theory]
    [InlineData(PartSettingsKind.Wheel, PortDirection.Input, "spin")]
    [InlineData(PartSettingsKind.Servo, PortDirection.Input, "torque")]
    [InlineData(PartSettingsKind.Accelerometer, PortDirection.Output, "along")]
    public void AnUnknownPort_Throws(PartSettingsKind kind, PortDirection direction, string channel)
    {
        var port = new BrainPort(1, channel, direction, direction == PortDirection.Input ? PortSignal.Reading : PortSignal.Position);

        Should.Throw<InvalidOperationException>(() => BrainPortDisplay.Range(kind, port));
        Should.Throw<InvalidOperationException>(() => BrainPortDisplay.ShortLabel(kind, port));
    }

    private static PartSettingsKind KindOf(int partId) => partId switch
    {
        PortedCreature.Accelerometer => PartSettingsKind.Accelerometer,
        PortedCreature.Camera => PartSettingsKind.Camera,
        PortedCreature.Servo => PartSettingsKind.Servo,
        PortedCreature.Piston => PartSettingsKind.Piston,
        PortedCreature.Touch => PartSettingsKind.Touch,
        PortedCreature.VelocityMotor => PartSettingsKind.VelocityMotor,
        PortedCreature.AngleSensor => PartSettingsKind.AngleSensor,
        _ => throw new ArgumentOutOfRangeException(nameof(partId)),
    };
}
