using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class ServoTests
{
    private const double _step = 1.0 / 60;

    private static readonly ServoDef _servo = new(1, 2, 3, 4);

    // Heavy enough that the push part always asks for more than the rise time allows.
    private const double _heavy = 1e9;

    [Fact]
    public void AngleInput_IsPiecewiseAroundTheBuiltAngle()
    {
        Servo.AngleInput(-Math.PI / 2, _servo).ShouldBe(-1, tolerance: 1e-12);
        Servo.AngleInput(0, _servo).ShouldBe(0);
        Servo.AngleInput(Math.PI / 2, _servo).ShouldBe(1, tolerance: 1e-12);
        Servo.AngleInput(Math.PI / 4, _servo).ShouldBe(0.5, tolerance: 1e-12);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(1)]
    public void AngleInput_HandlesStartAtEitherStop(double start)
    {
        var servo = _servo.WithSettings(_servo.Strength, Math.PI, start, _servo.MaxSpeed, _servo.RiseTime);

        Servo.AngleInput(Servo.LowerAngle(servo), servo).ShouldBe(start == 0 ? 0 : -1, tolerance: 1e-12);
        Servo.AngleInput(Servo.UpperAngle(servo), servo).ShouldBe(start == 1 ? 0 : 1, tolerance: 1e-12);
    }

    [Fact]
    public void TargetAngle_MapsThePositionOutputOntoTheStops()
    {
        Servo.TargetAngle(-1, _servo).ShouldBe(-Math.PI / 2, tolerance: 1e-12);
        Servo.TargetAngle(0, _servo).ShouldBe(0, tolerance: 1e-12);
        Servo.TargetAngle(1, _servo).ShouldBe(Math.PI / 2, tolerance: 1e-12);
    }

    [Fact]
    public void WantedSpeed_ClosesTheGapToTheTarget_NoFasterThanMaxSpeed()
    {
        var target = Servo.TargetAngle(0.5, _servo, builtAngle: 0.3);

        Servo.WantedSpeed(0.5, _servo, builtAngle: 0.3, angle: target).ShouldBe(0);
        Servo.WantedSpeed(0.5, _servo, builtAngle: 0.3, angle: target - 0.01).ShouldBe(0.1, tolerance: 1e-12);
        Servo.WantedSpeed(0.5, _servo, builtAngle: 0.3, angle: target + 0.01).ShouldBe(-0.1, tolerance: 1e-12);
        Servo.WantedSpeed(1, _servo, builtAngle: 0, angle: -Math.PI).ShouldBe(_servo.MaxSpeed);
        Servo.WantedSpeed(-1, _servo, builtAngle: 0, angle: Math.PI).ShouldBe(-_servo.MaxSpeed);
    }

    [Fact]
    public void NextMotor_FromRest_BuildsTowardsItsTarget_AtTheRiseRate()
    {
        var turn = Next(_servo, angle: 0, position: 1, strength: 1, default, _heavy);
        var back = Next(_servo, angle: 0, position: -1, strength: 1, default, _heavy);

        turn.Torque.ShouldBe(_servo.Strength * _step / _servo.RiseTime, tolerance: 1e-6);
        back.Torque.ShouldBe(-turn.Torque, tolerance: 1e-6);
    }

    [Fact]
    public void NextMotor_WithStrengthZero_IsZero() =>
        Next(_servo, angle: 0, position: 1, strength: 0, default, _heavy).Torque.ShouldBe(0);

    [Fact]
    public void NextMotor_ChasesWithTheChosenShareOfItsStrength()
    {
        var chosen = OutputSignals.StrengthFromOutput(0.2, _servo.Strength);

        var motor = Next(_servo, angle: 0, position: 1, strength: 0.2, new MotorState(_servo.Strength, _servo.Strength), _heavy);

        motor.Torque.ShouldBe(chosen);
    }

    [Fact]
    public void EndStopTorque_IsZeroInsideAndRestoresPastLimits()
    {
        Servo.EndStopTorque(angle: 0, speed: 10, lower: -1, upper: 1, stiffness: 100, damping: 10).ShouldBe(0);
        Servo.EndStopTorque(angle: -1.2, speed: 0, lower: -1, upper: 1, stiffness: 100, damping: 10).ShouldBe(20, tolerance: 1e-12);
        Servo.EndStopTorque(angle: 1.2, speed: 0, lower: -1, upper: 1, stiffness: 100, damping: 10).ShouldBe(-20, tolerance: 1e-12);
        Servo.EndStopTorque(angle: 1.2, speed: -1, lower: -1, upper: 1, stiffness: 100, damping: 10).ShouldBe(-10, tolerance: 1e-12);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(720)]
    public void StableEndStopTorque_AtSliderExtremes_DoesNotReboundFasterThanItHit_OrDiverge(double maxSpeedDegrees)
    {
        var servo = ExtremeServo(maxSpeedDegrees);

        var result = RunEndStop(servo, effectiveInertia: 1000, speed: 1);

        result.Fastest.ShouldBeLessThanOrEqualTo(1 + 1e-9);
        result.FinalAngle.ShouldBeLessThanOrEqualTo(Servo.UpperAngle(servo) + 0.01);
    }

    [Fact]
    public void EndStopTorque_Overload_CapsDesiredGains()
    {
        var servo = ExtremeServo(30);
        var gains = Servo.StableEndStopGains(servo, effectiveInertia: 1000, step: _step);

        gains.Damping.ShouldBeLessThan(servo.Strength / servo.MaxSpeed);
        gains.Stiffness.ShouldBeLessThan(4 * servo.Strength / servo.Range);
        Servo.EndStopTorque(servo, 0, Servo.UpperAngle(servo) + 0.1, speed: 1, effectiveInertia: 1000, step: _step)
            .ShouldBeLessThan(0);
    }

    [Theory]
    [InlineData(30, 1000)]
    [InlineData(720, 1000)]
    [InlineData(720, 340)]
    [InlineData(30, 500)]
    [InlineData(30, 8000)]
    [InlineData(720, 16000)]
    [InlineData(30, 30000)]
    [InlineData(720, 30000)]
    public void NextMotor_AtSliderExtremes_SettlesWithoutOscillating_EvenWhenItsInertiaGuessIsFarOff(double maxSpeedDegrees, double realInertia)
    {
        var servo = ExtremeServo(maxSpeedDegrees);

        var result = RunMotor(servo, effectiveInertia: 1000, realInertia, load: 0, position: 1);

        result.Fastest.ShouldBeLessThanOrEqualTo(1.5 * servo.MaxSpeed);
        result.FinalAngle.ShouldBe(Servo.TargetAngle(1, servo), tolerance: 0.01);
        result.LastSecondSwing.ShouldBeLessThan(0.001);
    }

    [Theory]
    [InlineData(30, 0, 1385)]
    [InlineData(30, 1, 1385)]
    [InlineData(360, -1, 1385)]
    [InlineData(720, 1, 1385)]
    [InlineData(360, -1, 27700)]
    [InlineData(720, 1, 27700)]
    public void NextMotor_HoldsASteadyLoadBelowItsStrength(double maxSpeedDegrees, double position, double realInertia)
    {
        var servo = _servo.WithSettings(_servo.Strength, _servo.Range, _servo.Start, Degrees(maxSpeedDegrees), _servo.RiseTime);
        var load = -0.5 * servo.Strength;

        var result = RunMotor(servo, effectiveInertia: 1385, realInertia, load, position);

        result.FinalAngle.ShouldBe(Servo.TargetAngle(position, servo), tolerance: 0.01);
        result.LastSecondSwing.ShouldBeLessThan(0.001);
        result.Torque.ShouldBe(-load, tolerance: 0.01 * servo.Strength);
    }

    [Fact]
    public void NextMotor_OnAPlantedLeg_StopsGatheringAtFullStrength_AndSettles()
    {
        var result = RunMotor(_servo, effectiveInertia: 6750, realInertia: 6750 * 30, load: 0, position: 0.5);

        result.FinalAngle.ShouldBe(Servo.TargetAngle(0.5, _servo), tolerance: 0.01);
        result.LastSecondSwing.ShouldBeLessThan(0.001);
    }

    private static ServoDef ExtremeServo(double maxSpeedDegrees) =>
        _servo.WithSettings(strength: 2_000_000, range: Degrees(20), start: 0.5, maxSpeed: Degrees(maxSpeedDegrees), riseTime: ServoDef.DefaultRiseTime);

    private static (double Fastest, double FinalAngle) RunEndStop(ServoDef servo, double effectiveInertia, double speed)
    {
        var angle = Servo.UpperAngle(servo) + 0.02;
        var fastest = Math.Abs(speed);
        for (var step = 0; step < 240; step++)
        {
            var torque = Servo.EndStopTorque(servo, builtAngle: 0, angle, speed, effectiveInertia, _step);
            speed += torque / effectiveInertia * _step;
            angle += speed * _step;
            fastest = Math.Max(fastest, Math.Abs(speed));
        }

        return (fastest, angle);
    }

    private static MotorState Next(ServoDef servo, double angle, double position, double strength, MotorState previous, double effectiveInertia) =>
        Servo.NextMotor(servo, builtAngle: 0, angle, speed: 0, position, strength, previous, _step, effectiveInertia);

    private static (double Fastest, double FinalAngle, double LastSecondSwing, double Torque) RunMotor(
        ServoDef servo,
        double effectiveInertia,
        double realInertia,
        double load,
        double position)
    {
        var angle = 0.0;
        var speed = 0.0;
        var motor = default(MotorState);
        var fastest = 0.0;
        var steps = 1800;
        var lowest = double.MaxValue;
        var highest = double.MinValue;
        for (var step = 0; step < steps; step++)
        {
            motor = Servo.NextMotor(servo, builtAngle: 0, angle, speed, position, strength: 1, motor, _step, effectiveInertia);
            var endStop = Servo.EndStopTorque(servo, builtAngle: 0, angle, speed, effectiveInertia, _step);
            speed += (motor.Torque + endStop + load) / realInertia * _step;
            angle += speed * _step;
            fastest = Math.Max(fastest, Math.Abs(speed));
            if (step >= steps - 60)
            {
                lowest = Math.Min(lowest, angle);
                highest = Math.Max(highest, angle);
            }
        }

        return (fastest, angle, highest - lowest, motor.Torque);
    }

    private static double Degrees(double value) => value * Math.PI / 180;
}
