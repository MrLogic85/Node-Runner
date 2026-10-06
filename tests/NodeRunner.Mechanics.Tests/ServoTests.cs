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
    public void SpeedInput_IsPositiveCounterClockwise_AndSaturatesSoftly()
    {
        Servo.SpeedInput(0, _servo.MaxSpeed).ShouldBe(0);
        Servo.SpeedInput(_servo.MaxSpeed, _servo.MaxSpeed).ShouldBe(Math.Tanh(1), tolerance: 1e-12);
        Servo.SpeedInput(-1000 * _servo.MaxSpeed, _servo.MaxSpeed).ShouldBe(-1, tolerance: 1e-9);
    }

    [Fact]
    public void TargetAngle_MapsThePositionOutputOntoTheStops()
    {
        Servo.TargetAngle(-1, _servo).ShouldBe(-Math.PI / 2, tolerance: 1e-12);
        Servo.TargetAngle(0, _servo).ShouldBe(0, tolerance: 1e-12);
        Servo.TargetAngle(1, _servo).ShouldBe(Math.PI / 2, tolerance: 1e-12);
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
    public void LinkAngleAndSpeed_FollowTheFarJointAroundTheServoJoint()
    {
        var joint = new Vector2D(0, 0);
        var right = new Vector2D(20, 0);
        var up = new Vector2D(0, -20);

        Servo.LinkAngle(joint, up).ShouldBe(Math.PI / 2, tolerance: 1e-12);
        Servo.LinkAngularSpeed(joint, right, new Vector2D(0, 0), new Vector2D(0, -20))
            .ShouldBe(1, tolerance: 1e-12);
    }

    [Fact]
    public void CoupleForTorque_ReturnsEqualOppositeForcesWithTheRequestedMoment()
    {
        var couple = Servo.CoupleForTorque(new Vector2D(0, 0), new Vector2D(20, 0), torque: 60);

        couple.ForceOnJoint.ShouldBe(new Vector2D(0, 3));
        couple.ForceOnFar.ShouldBe(new Vector2D(0, -3));
        (-(20 * couple.ForceOnFar.Y) + (0 * couple.ForceOnFar.X)).ShouldBe(60);
    }

    [Fact]
    public void CoupleForTorque_ClampsForceWhenTheLeverArmIsVeryShort()
    {
        var couple = Servo.CoupleForTorque(new Vector2D(0, 0), new Vector2D(1, 0), torque: 30);

        couple.ForceOnFar.ShouldBe(new Vector2D(0, -30 / Servo.MinimumLeverArm));
        couple.ForceOnJoint.ShouldBe(new Vector2D(0, 30 / Servo.MinimumLeverArm));
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

    [Theory]
    [InlineData(30, 1000)]
    [InlineData(720, 1000)]
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

    [Fact]
    public void LinkInertia_ForALinkShorterThanTheLeverArm_ShrinksWithItsLength()
    {
        var joint = new Vector2D(0, 0);
        var reducedMass = 0.5;

        Servo.LinkInertia(joint, new Vector2D(30, 0), 1, 1).ShouldBe(reducedMass * 30 * 30, tolerance: 1e-9);
        Servo.LinkInertia(joint, new Vector2D(1, 0), 1, 1).ShouldBe(reducedMass * 1 * Servo.MinimumLeverArm, tolerance: 1e-9);
        Servo.LinkInertia(joint, joint, 1, 1).ShouldBeGreaterThan(0);
        Servo.LinkInertia(joint, joint, 1, 1).ShouldBeLessThan(1e-3);
    }

    [Fact]
    public void UnwrapAngle_CrossingPi_StaysContinuous()
    {
        var previous = Math.PI - 0.1;
        var current = -Math.PI + 0.1;

        Servo.UnwrapAngle(previous, previous, current).ShouldBe(Math.PI + 0.1, tolerance: 1e-12);
    }

    [Fact]
    public void UnwrapAngle_AllowsAFullTurnRange()
    {
        var continuous = 0.0;
        var previous = 0.0;
        foreach (var current in new[] { Math.PI / 2, Math.PI - 0.1, -Math.PI + 0.1, -Math.PI / 2, 0 })
        {
            continuous = Servo.UnwrapAngle(continuous, previous, current);
            previous = current;
        }

        continuous.ShouldBe(Math.Tau, tolerance: 1e-12);
    }

    [Fact]
    public void NextMotor_WithStrengthZero_IsZero() =>
        Next(_servo, angle: 0, position: 1, strength: 0, default, _heavy).Torque.ShouldBe(0);

    [Fact]
    public void NextMotor_NeverExceedsChosenStrength()
    {
        var chosen = OutputSignals.StrengthFromOutput(0.2, _servo.Strength);
        var motor = Next(_servo, angle: 0, position: 1, strength: 0.2, new ServoMotor(_servo.Strength, _servo.Strength), _heavy);

        Math.Abs(motor.Torque).ShouldBeLessThanOrEqualTo(chosen);
        Math.Abs(motor.Holding).ShouldBeLessThanOrEqualTo(chosen);
    }

    [Fact]
    public void NextMotor_ReachesFullWithinAboutRiseTime()
    {
        var motor = default(ServoMotor);
        for (var step = 0; step < (int)(1.1 * _servo.RiseTime / _step); step++)
        {
            motor = Next(_servo, angle: 0, position: 1, strength: 1, motor, _heavy);
        }

        motor.Torque.ShouldBe(_servo.Strength);
    }

    [Fact]
    public void NextMotor_ALongerRiseTime_BuildsMoreSlowly()
    {
        var quick = _servo.WithSettings(_servo.Strength, _servo.Range, _servo.Start, _servo.MaxSpeed, 0.1);
        var slow = _servo.WithSettings(_servo.Strength, _servo.Range, _servo.Start, _servo.MaxSpeed, 0.5);

        var quickTorque = Next(quick, angle: 0, position: 1, strength: 1, default, _heavy).Torque;
        var slowTorque = Next(slow, angle: 0, position: 1, strength: 1, default, _heavy).Torque;

        slowTorque.ShouldBe(quickTorque / 5, tolerance: 1e-6);
    }

    [Fact]
    public void NextMotor_PastItsTarget_DropsItsPushAtOnce()
    {
        var pushing = new ServoMotor(_servo.Strength, 0);

        var motor = Next(_servo, angle: Servo.TargetAngle(1, _servo) + 0.5, position: 1, strength: 1, pushing, effectiveInertia: 1000);

        motor.Torque.ShouldBeLessThan(0);
        motor.Torque.ShouldBeGreaterThanOrEqualTo(-_servo.Strength * _step / _servo.RiseTime);
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

    private static ServoMotor Next(ServoDef servo, double angle, double position, double strength, ServoMotor previous, double effectiveInertia) =>
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
        var motor = default(ServoMotor);
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
