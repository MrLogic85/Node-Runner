using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.App.Tests.ViewModels;

public sealed class BrainPortDisplayTests
{
    [Theory]
    [InlineData(PartKind.Accelerometer, PortDirection.Input, "along", PortRange.MinusOneToOne, "along")]
    [InlineData(PartKind.Accelerometer, PortDirection.Input, "across", PortRange.MinusOneToOne, "across")]
    [InlineData(PartKind.Camera, PortDirection.Input, "left2", PortRange.ZeroToOne, "far left")]
    [InlineData(PartKind.Camera, PortDirection.Input, "left1", PortRange.ZeroToOne, "left")]
    [InlineData(PartKind.Camera, PortDirection.Input, "centre", PortRange.ZeroToOne, "centre")]
    [InlineData(PartKind.Camera, PortDirection.Input, "right1", PortRange.ZeroToOne, "right")]
    [InlineData(PartKind.Camera, PortDirection.Input, "right2", PortRange.ZeroToOne, "far right")]
    [InlineData(PartKind.Camera, PortDirection.Input, "hit", PortRange.ZeroToOne, "hit")]
    [InlineData(PartKind.Piston, PortDirection.Input, "length", PortRange.ZeroToOne, "length")]
    [InlineData(PartKind.Piston, PortDirection.Input, "speed", PortRange.MinusOneToOne, "speed")]
    [InlineData(PartKind.Piston, PortDirection.Output, "position", PortRange.ZeroToOne, "length")]
    [InlineData(PartKind.Piston, PortDirection.Output, "strength", PortRange.ZeroToOne, "strength")]
    [InlineData(PartKind.Servo, PortDirection.Input, "angle", PortRange.MinusOneToOne, "angle")]
    [InlineData(PartKind.Servo, PortDirection.Input, "speed", PortRange.MinusOneToOne, "speed")]
    [InlineData(PartKind.Servo, PortDirection.Output, "angle", PortRange.MinusOneToOne, "angle")]
    [InlineData(PartKind.Servo, PortDirection.Output, "strength", PortRange.ZeroToOne, "strength")]
    [InlineData(PartKind.TouchSensor, PortDirection.Input, "touch", PortRange.ZeroToOne, "touch")]
    [InlineData(PartKind.VelocityMotor, PortDirection.Input, "angleSin", PortRange.MinusOneToOne, "angle sin")]
    [InlineData(PartKind.VelocityMotor, PortDirection.Input, "angleCos", PortRange.MinusOneToOne, "angle cos")]
    [InlineData(PartKind.VelocityMotor, PortDirection.Input, "speed", PortRange.MinusOneToOne, "speed")]
    [InlineData(PartKind.VelocityMotor, PortDirection.Output, "speed", PortRange.MinusOneToOne, "speed")]
    [InlineData(PartKind.VelocityMotor, PortDirection.Output, "strength", PortRange.ZeroToOne, "strength")]
    [InlineData(PartKind.AngleSensor, PortDirection.Input, "angleSin", PortRange.MinusOneToOne, "angle sin")]
    [InlineData(PartKind.AngleSensor, PortDirection.Input, "angleCos", PortRange.MinusOneToOne, "angle cos")]
    [InlineData(PartKind.AngleSensor, PortDirection.Input, "speed", PortRange.MinusOneToOne, "speed")]
    [InlineData(PartKind.Stepper, PortDirection.Input, "angleSin", PortRange.MinusOneToOne, "angle sin")]
    [InlineData(PartKind.Stepper, PortDirection.Input, "angleCos", PortRange.MinusOneToOne, "angle cos")]
    [InlineData(PartKind.Stepper, PortDirection.Input, "frequency", PortRange.ZeroToOne, "frequency")]
    [InlineData(PartKind.Stepper, PortDirection.Output, "frequency", PortRange.ZeroToOne, "frequency")]
    [InlineData(PartKind.Stepper, PortDirection.Output, "power", PortRange.MinusOneToOne, "power")]
    public void EveryPort_HasItsRangeAndShortLabel(PartKind kind, PortDirection direction, string channel, PortRange range, string label)
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

        layout.Inputs.Count.ShouldBe(22);
        layout.Outputs.Count.ShouldBe(8);
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

        BrainPortDisplay.Range(PartKind.Piston, target).ShouldBe(BrainPortDisplay.Range(PartKind.Piston, BrainPort.Input(1, BrainPorts.PistonLengthChannel)));
    }

    [Theory]
    [InlineData(PartKind.Wheel, PortDirection.Input, "spin")]
    [InlineData(PartKind.Servo, PortDirection.Input, "torque")]
    [InlineData(PartKind.Accelerometer, PortDirection.Output, "along")]
    public void AnUnknownPort_Throws(PartKind kind, PortDirection direction, string channel)
    {
        var port = new BrainPort(1, channel, direction, direction == PortDirection.Input ? PortSignal.Reading : PortSignal.Position);

        Should.Throw<InvalidOperationException>(() => BrainPortDisplay.Range(kind, port));
        Should.Throw<InvalidOperationException>(() => BrainPortDisplay.ShortLabel(kind, port));
    }

    private static PartKind KindOf(int partId) => partId switch
    {
        PortedCreature.Accelerometer => PartKind.Accelerometer,
        PortedCreature.Camera => PartKind.Camera,
        PortedCreature.Servo => PartKind.Servo,
        PortedCreature.Piston => PartKind.Piston,
        PortedCreature.Touch => PartKind.TouchSensor,
        PortedCreature.VelocityMotor => PartKind.VelocityMotor,
        PortedCreature.AngleSensor => PartKind.AngleSensor,
        PortedCreature.Stepper => PartKind.Stepper,
        _ => throw new ArgumentOutOfRangeException(nameof(partId)),
    };
}
