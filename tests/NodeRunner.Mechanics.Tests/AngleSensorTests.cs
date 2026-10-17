using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class AngleSensorTests
{
    // Read as a Velocity motor's from target minus fixed (y-down, so counter-clockwise on screen is toward negative y):
    // target a quarter turn counter-clockwise reads 1, 0, opposite reads 0, −1. A Wheel target turning
    // counter-clockwise (Godot's −1 rad/s) reads a positive speed; swapping the sides reverses both signs.
    [Fact]
    public void ItReadsTheTargetSideAgainstTheFixedSide()
    {
        var right = HingePartSideGeometry.LinkAngle(new Vector2D(0, 0), new Vector2D(10, 0));
        Reads(AngleSensor.AngleInputs(HingePartSideGeometry.LinkAngle(new Vector2D(0, 0), new Vector2D(0, -10)) - right), 1, 0);
        Reads(AngleSensor.AngleInputs(HingePartSideGeometry.LinkAngle(new Vector2D(0, 0), new Vector2D(-10, 0)) - right), 0, -1);
        foreach (var angle in new[] { 0, 0.7, Math.PI, -2.5, (4 * Math.PI) + 1 })
        {
            AngleSensor.AngleInputs(angle).ShouldBe(VelocityMotor.AngleInputs(angle));
        }

        var wheel = HingePartSideGeometry.WheelAngle(bodyRotation: -Math.PI / 2);
        var wheelSpeed = HingePartSideGeometry.WheelAngularSpeed(bodyAngularVelocity: -1);
        Reads(AngleSensor.AngleInputs(wheel - right), 1, 0);
        AngleSensor.SpeedInput(wheelSpeed).ShouldBeGreaterThan(0);
        AngleSensor.AngleInputs(right - wheel).Sin.ShouldBe(-1, 1e-12);
        AngleSensor.SpeedInput(-wheelSpeed).ShouldBeLessThan(0);
    }

    // tanh of the speed over 60 rpm: a Velocity motor's only when its Max speed is 60 rpm.
    [Fact]
    public void ItsSpeedInput_IsTanhOfTheSpeedOverSixtyRpm()
    {
        foreach (var (speed, input) in new[] { (0.0, 0.0), (2 * Math.PI, 0.7615941559557649), (-2 * Math.PI, -0.7615941559557649), (Math.PI, 0.46211715726000974) })
        {
            AngleSensor.SpeedInput(speed).ShouldBe(input, 1e-12);
        }

        AngleSensor.SpeedInput(3).ShouldBe(MotorLoop.SpeedInput(3, new VelocityMotorDef(1, 2, maxSpeed: 2 * Math.PI).MaxSpeed), 1e-12);
        Math.Abs(AngleSensor.SpeedInput(3) - MotorLoop.SpeedInput(3, new VelocityMotorDef(1, 2, maxSpeed: 4 * Math.PI).MaxSpeed)).ShouldBeGreaterThan(1e-6);
    }

    private static void Reads((double Sin, double Cos) inputs, double sin, double cos)
    {
        inputs.Sin.ShouldBe(sin, 1e-12);
        inputs.Cos.ShouldBe(cos, 1e-12);
    }
}
