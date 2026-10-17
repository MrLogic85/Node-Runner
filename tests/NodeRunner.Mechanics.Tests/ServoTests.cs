using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class ServoTests
{
    private const double _step = 1.0 / 60;

    private static readonly ServoDef _servo = new(1, 2, 3, 4);

    // Heavy enough that the push part always asks for more than the rise time allows.
    private const double _heavy = 1e9;

    // Range 90° each way of its built angle by default: its angle input is piecewise around the built angle,
    // whichever stop it starts at, and its position output asks for an angle on the same scale.
    [Fact]
    public void ItsAngleInputAndTarget_ArePiecewiseAroundTheBuiltAngle()
    {
        foreach (var (angle, input) in new[] { (-Math.PI / 2, -1.0), (0, 0), (Math.PI / 4, 0.5), (Math.PI / 2, 1) })
        {
            Servo.AngleInput(angle, _servo).ShouldBe(input, 1e-12);
            Servo.TargetAngle(input, _servo).ShouldBe(angle, 1e-12);
        }

        foreach (var start in new[] { 0, 0.5, 1 })
        {
            var servo = _servo.WithSettings(_servo.Strength, Math.PI, start, _servo.MaxSpeed, _servo.RiseTime);
            Servo.AngleInput(Servo.LowerAngle(servo), servo).ShouldBe(start == 0 ? 0 : -1, 1e-12);
            Servo.AngleInput(Servo.UpperAngle(servo), servo).ShouldBe(start == 1 ? 0 : 1, 1e-12);
        }

        var target = Servo.TargetAngle(0.5, _servo, builtAngle: 0.3);
        Servo.WantedSpeed(0.5, _servo, builtAngle: 0.3, angle: target).ShouldBe(0);
        Servo.WantedSpeed(0.5, _servo, builtAngle: 0.3, angle: target - 0.01).ShouldBe(0.1, 1e-12);
        Servo.WantedSpeed(0.5, _servo, builtAngle: 0.3, angle: target + 0.01).ShouldBe(-0.1, 1e-12);
        Servo.WantedSpeed(1, _servo, builtAngle: 0, angle: -Math.PI).ShouldBe(_servo.MaxSpeed);
        Servo.WantedSpeed(-1, _servo, builtAngle: 0, angle: Math.PI).ShouldBe(-_servo.MaxSpeed);
    }

    // From rest it builds towards its target at the rise rate, up to the chosen share of its strength.
    [Fact]
    public void ItsTorque_BuildsAtTheRiseRate_UpToTheChosenShare()
    {
        var turn = Next(_servo, position: 1, strength: 1, default);
        turn.Torque.ShouldBe(_servo.Strength * _step / _servo.RiseTime, 1e-6);
        Next(_servo, position: 1, strength: 0.2, new MotorState(_servo.Strength, _servo.Strength)).Torque
            .ShouldBe(OutputSignals.StrengthFromOutput(0.2, _servo.Strength));
    }

    // A spring and damper past its stops, nothing inside; at the slider extremes its gains are capped so it
    // rebounds no faster than it hit and does not diverge.
    [Fact]
    public void ItsEndStops_PushBackPastItsLimits_WithoutRebounding()
    {
        foreach (var (angle, speed, torque) in new[] { (0.0, 10.0, 0.0), (-1.2, 0, 20), (1.2, 0, -20), (1.2, -1, -10) })
        {
            Servo.EndStopTorque(angle, speed, lower: -1, upper: 1, stiffness: 100, damping: 10).ShouldBe(torque, 1e-12);
        }

        var slow = ExtremeServo(30);
        var gains = Servo.StableEndStopGains(slow, effectiveInertia: 1000, step: _step);
        gains.Damping.ShouldBeLessThan(slow.Strength / slow.MaxSpeed);
        gains.Stiffness.ShouldBeLessThan(4 * slow.Strength / slow.Range);
        Servo.EndStopTorque(slow, 0, Servo.UpperAngle(slow) + 0.1, speed: 1, effectiveInertia: 1000, step: _step).ShouldBeLessThan(0);
        foreach (var servo in new[] { slow, ExtremeServo(720) })
        {
            var (fastest, finalAngle) = RunEndStop(servo, effectiveInertia: 1000, speed: 1);
            fastest.ShouldBeLessThanOrEqualTo(1 + 1e-9);
            finalAngle.ShouldBeLessThanOrEqualTo(Servo.UpperAngle(servo) + 0.01);
        }
    }

    // At the slider extremes it settles without oscillating, even when its inertia guess of 1000 is far off.
    [Theory]
    [InlineData(30, 1000)]
    [InlineData(720, 340)]
    [InlineData(30, 500)]
    [InlineData(720, 16000)]
    [InlineData(30, 30000)]
    [InlineData(720, 30000)]
    public void AtSliderExtremes_ItSettlesWithoutOscillating(double maxSpeedDegrees, double realInertia)
    {
        var servo = ExtremeServo(maxSpeedDegrees);

        var result = RunMotor(servo, effectiveInertia: 1000, realInertia, load: 0, position: 1);

        result.Fastest.ShouldBeLessThanOrEqualTo(1.5 * servo.MaxSpeed);
        result.FinalAngle.ShouldBe(Servo.TargetAngle(1, servo), 0.01);
        result.LastSecondSwing.ShouldBeLessThan(0.001);
    }

    // It holds a steady load of half its strength, and on a planted leg 30 times heavier than it reads it stops
    // gathering at full strength and settles.
    [Theory]
    [InlineData(30, 0, 1385, 1385, -0.5)]
    [InlineData(360, -1, 1385, 1385, -0.5)]
    [InlineData(720, 1, 1385, 1385, -0.5)]
    [InlineData(720, 1, 1385, 27700, -0.5)]
    [InlineData(360, 0.5, 6750, 6750 * 30, 0)]
    public void ItHoldsASteadyLoadBelowItsStrength(double maxSpeedDegrees, double position, double effectiveInertia, double realInertia, double loadShare)
    {
        var servo = _servo.WithSettings(_servo.Strength, _servo.Range, _servo.Start, Degrees(maxSpeedDegrees), _servo.RiseTime);
        var load = loadShare * servo.Strength;

        var result = RunMotor(servo, effectiveInertia, realInertia, load, position);

        result.FinalAngle.ShouldBe(Servo.TargetAngle(position, servo), 0.01);
        result.LastSecondSwing.ShouldBeLessThan(0.001);
        result.Torque.ShouldBe(-load, 0.01 * servo.Strength);
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

    private static MotorState Next(ServoDef servo, double position, double strength, MotorState previous) =>
        Servo.NextMotor(servo, builtAngle: 0, angle: 0, speed: 0, position, strength, previous, _step, _heavy);

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
