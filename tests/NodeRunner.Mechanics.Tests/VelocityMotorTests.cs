using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class VelocityMotorTests
{
    private const double _step = 1.0 / 60;

    private static readonly VelocityMotorDef _motor = new(1, 2, 3, 4);

    // Heavy enough that the push part always asks for more than the rise time allows.
    private const double _heavy = 1e9;

    [Fact]
    public void AngleInputs_AreZeroAndOneAsBuilt_AndTurnWithTheAngle()
    {
        VelocityMotor.AngleInputs(0.7, builtAngle: 0.7).ShouldBe((0, 1));

        var (sin, cos) = VelocityMotor.AngleInputs(0.7 + (Math.PI / 2), builtAngle: 0.7);
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
    public void SpeedInput_IsPositiveCounterClockwise_AndSaturatesSoftly()
    {
        VelocityMotor.SpeedInput(_motor.MaxSpeed, _motor.MaxSpeed).ShouldBe(Math.Tanh(1), tolerance: 1e-12);
        VelocityMotor.SpeedInput(-1000 * _motor.MaxSpeed, _motor.MaxSpeed).ShouldBe(-1, tolerance: 1e-9);
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

    private static ServoMotor Next(double speedOutput, double strength, ServoMotor previous) =>
        VelocityMotor.NextMotor(_motor, speed: 0, speedOutput, strength, previous, _step, _heavy);

    private static (double Speed, double Torque) Run(double speedOutput, double load, double realInertia)
    {
        var speed = 0.0;
        var motor = default(ServoMotor);
        for (var step = 0; step < 1800; step++)
        {
            motor = VelocityMotor.NextMotor(_motor, speed, speedOutput, strength: 1, motor, _step, effectiveInertia: realInertia);
            speed += (motor.Torque + load) / realInertia * _step;
        }

        return (speed, motor.Torque);
    }
}
