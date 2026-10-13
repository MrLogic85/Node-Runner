using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class VelocityMotorTests
{
    private const double _step = 1.0 / 60;

    private static readonly VelocityMotorDef _motor = new(1, 2, 3, 4);

    // Heavy enough that the push part always asks for more than the rise time allows.
    private const double _heavy = 1e9;

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(Math.PI / 2, 1, 0)]
    [InlineData(Math.PI, 0, -1)]
    [InlineData(-Math.PI / 2, -1, 0)]
    [InlineData((2 * Math.PI) + (Math.PI / 2), 1, 0)]
    public void AngleInputs_ReadTheAngleBetweenTheSides(double angle, double sin, double cos)
    {
        var inputs = VelocityMotor.AngleInputs(angle);

        inputs.Sin.ShouldBe(sin, tolerance: 1e-12);
        inputs.Cos.ShouldBe(cos, tolerance: 1e-12);
    }

    [Fact]
    public void AngleInputs_ReadAWheelTargetAQuarterTurnCounterClockwise_AsOneAndZero()
    {
        // Fixed points right; the Wheel's body is turned a quarter turn counter-clockwise on screen, y-down.
        var fixedSide = MotorSideGeometry.LinkAngle(new Vector2D(0, 0), new Vector2D(10, 0));
        var wheel = MotorSideGeometry.WheelAngle(bodyRotation: -Math.PI / 2);

        var (sin, cos) = VelocityMotor.AngleInputs(wheel - fixedSide);

        sin.ShouldBe(1, tolerance: 1e-12);
        cos.ShouldBe(0, tolerance: 1e-12);
    }

    [Fact]
    public void AngleInputs_DoNotJump_WhenTheAnglePassesHalfATurn()
    {
        var before = VelocityMotor.AngleInputs(Math.PI - 1e-6);
        var after = VelocityMotor.AngleInputs(-Math.PI + 1e-6);

        before.Sin.ShouldBe(after.Sin, tolerance: 1e-5);
        before.Cos.ShouldBe(after.Cos, tolerance: 1e-5);
    }

    [Fact]
    public void WantedSpeed_IsTheOutputTimesMaxSpeed_ClampedToIt()
    {
        VelocityMotor.WantedSpeed(0.5, _motor).ShouldBe(0.5 * _motor.MaxSpeed);
        VelocityMotor.WantedSpeed(-0.25, _motor).ShouldBe(-0.25 * _motor.MaxSpeed);
        VelocityMotor.WantedSpeed(3, _motor).ShouldBe(_motor.MaxSpeed);
        VelocityMotor.WantedSpeed(-3, _motor).ShouldBe(-_motor.MaxSpeed);
    }

    [Fact]
    public void NextMotor_FromRest_BuildsInTheWantedDirection_AtTheRiseRate()
    {
        var forward = Next(speedOutput: 1, strength: 1, default);
        var back = Next(speedOutput: -1, strength: 1, default);

        forward.Torque.ShouldBe(_motor.Strength * _step / _motor.RiseTime, tolerance: 1e-6);
        back.Torque.ShouldBe(-forward.Torque, tolerance: 1e-6);
    }

    [Fact]
    public void NextMotor_WithStrengthZero_IsZero() =>
        Next(speedOutput: 1, strength: 0, default).Torque.ShouldBe(0);

    [Fact]
    public void NextMotor_KeepsTheChosenSpeed_AgainstALoadBelowItsStrength()
    {
        var load = -0.5 * _motor.Strength;

        var (speed, torque) = Run(speedOutput: 0.5, load, realInertia: 1385);

        speed.ShouldBe(0.5 * _motor.MaxSpeed, tolerance: 0.01 * _motor.MaxSpeed);
        torque.ShouldBe(-load, tolerance: 0.01 * _motor.Strength);
    }

    [Fact]
    public void NextMotor_AgainstALoadPastItsStrength_GivesAllItCan_AndIsPushedBack()
    {
        var (speed, torque) = Run(speedOutput: 0.5, load: -2 * _motor.Strength, realInertia: 1385);

        torque.ShouldBe(_motor.Strength);
        speed.ShouldBeLessThan(0);
    }

    private static MotorState Next(double speedOutput, double strength, MotorState previous) =>
        VelocityMotor.NextMotor(_motor, speed: 0, speedOutput, strength, previous, _step, _heavy);

    private static (double Speed, double Torque) Run(double speedOutput, double load, double realInertia)
    {
        var speed = 0.0;
        var motor = default(MotorState);
        for (var step = 0; step < 1800; step++)
        {
            motor = VelocityMotor.NextMotor(_motor, speed, speedOutput, strength: 1, motor, _step, effectiveInertia: realInertia);
            speed += (motor.Torque + load) / realInertia * _step;
        }

        return (speed, motor.Torque);
    }
}
