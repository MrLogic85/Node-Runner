using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class ServoTests
{
    private const double _step = 1.0 / 60;

    private static readonly ServoDef _servo = new(1, 2, 3, 4);

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
    public void NextTorque_FromRest_BuildsTowardsItsTarget_LikePistonForce()
    {
        var turn = Servo.NextTorque(_servo, builtAngle: 0, angle: 0, speed: 0, position: 1, strength: 1, torque: 0, step: _step);
        var back = Servo.NextTorque(_servo, builtAngle: 0, angle: 0, speed: 0, position: -1, strength: 1, torque: 0, step: _step);

        var rate = _servo.Strength * _step / _servo.RiseTime;
        turn.ShouldBe(rate * Math.Tanh(3) / (1 + Math.Exp(-10)), tolerance: 1e-6);
        back.ShouldBe(-turn, tolerance: 1e-6);
    }

    [Fact]
    public void LinkAngleAndSpeed_FollowTheFarJointAroundTheServoJoint()
    {
        var joint = new Vector2D(1, 1);
        var far = new Vector2D(1, 3);

        Servo.LinkAngle(joint, far).ShouldBe(Math.PI / 2, tolerance: 1e-12);
        Servo.LinkAngularSpeed(joint, far, new Vector2D(0, 0), new Vector2D(-4, 0))
            .ShouldBe(2, tolerance: 1e-12);
    }

    [Fact]
    public void CoupleForTorque_ReturnsEqualOppositeForcesWithTheRequestedMoment()
    {
        var couple = Servo.CoupleForTorque(new Vector2D(0, 0), new Vector2D(2, 0), torque: 6);

        couple.ForceOnJoint.ShouldBe(new Vector2D(0, -3));
        couple.ForceOnFar.ShouldBe(new Vector2D(0, 3));
        ((2 * couple.ForceOnFar.Y) - (0 * couple.ForceOnFar.X)).ShouldBe(6);
    }

    [Fact]
    public void EndStopTorque_IsZeroInsideAndRestoresPastLimits()
    {
        Servo.EndStopTorque(angle: 0, speed: 10, lower: -1, upper: 1, stiffness: 100, damping: 10).ShouldBe(0);
        Servo.EndStopTorque(angle: -1.2, speed: 0, lower: -1, upper: 1, stiffness: 100, damping: 10).ShouldBe(20, tolerance: 1e-12);
        Servo.EndStopTorque(angle: 1.2, speed: 0, lower: -1, upper: 1, stiffness: 100, damping: 10).ShouldBe(-20, tolerance: 1e-12);
        Servo.EndStopTorque(angle: 1.2, speed: -1, lower: -1, upper: 1, stiffness: 100, damping: 10).ShouldBe(-10, tolerance: 1e-12);
    }
}
