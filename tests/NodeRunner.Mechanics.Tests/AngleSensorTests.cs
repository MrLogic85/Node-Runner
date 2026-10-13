using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class AngleSensorTests
{
    [Fact]
    public void AngleInputs_WithTargetAQuarterTurnCounterClockwiseFromFixed_AreOneAndZero()
    {
        var fixedSide = HingePartSideGeometry.LinkAngle(new Vector2D(0, 0), new Vector2D(10, 0));
        // Creature coordinates are y-down, so counter-clockwise on screen is up, toward negative y.
        var targetSide = HingePartSideGeometry.LinkAngle(new Vector2D(0, 0), new Vector2D(0, -10));

        var (sin, cos) = AngleSensor.AngleInputs(targetSide - fixedSide);

        sin.ShouldBe(1, tolerance: 1e-12);
        cos.ShouldBe(0, tolerance: 1e-12);
    }

    [Fact]
    public void AngleInputs_WithTargetOppositeFixed_AreZeroAndMinusOne()
    {
        var fixedSide = HingePartSideGeometry.LinkAngle(new Vector2D(0, 0), new Vector2D(10, 0));
        var targetSide = HingePartSideGeometry.LinkAngle(new Vector2D(0, 0), new Vector2D(-10, 0));

        var (sin, cos) = AngleSensor.AngleInputs(targetSide - fixedSide);

        sin.ShouldBe(0, tolerance: 1e-12);
        cos.ShouldBe(-1, tolerance: 1e-12);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.7)]
    [InlineData(Math.PI)]
    [InlineData(-2.5)]
    [InlineData((4 * Math.PI) + 1)]
    public void AngleInputs_EqualAVelocityMotors_OnTheSameAngle(double angle)
    {
        var sensor = AngleSensor.AngleInputs(angle);

        sensor.ShouldBe(VelocityMotor.AngleInputs(angle));
    }

    [Fact]
    public void Readings_WithFixedRightAndTheTargetWheelTurnedAQuarterTurnCounterClockwise_AreOneZeroAndAPositiveSpeed()
    {
        // Godot's body rotation is clockwise-positive on screen, so −π/2 is a quarter turn counter-clockwise.
        var fixedSide = HingePartSideGeometry.LinkAngle(new Vector2D(0, 0), new Vector2D(10, 0));
        var wheel = HingePartSideGeometry.WheelAngle(bodyRotation: -Math.PI / 2);
        var wheelSpeed = HingePartSideGeometry.WheelAngularSpeed(bodyAngularVelocity: -1);
        const double fixedSpeed = 0;

        var (sin, cos) = AngleSensor.AngleInputs(wheel - fixedSide);
        var speed = AngleSensor.SpeedInput(wheelSpeed - fixedSpeed);

        sin.ShouldBe(1, tolerance: 1e-12);
        cos.ShouldBe(0, tolerance: 1e-12);
        speed.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Readings_WithFixedAndTargetSwapped_ReverseTheirSign()
    {
        var link = HingePartSideGeometry.LinkAngle(new Vector2D(0, 0), new Vector2D(10, 0));
        var wheel = HingePartSideGeometry.WheelAngle(bodyRotation: -Math.PI / 2);
        var wheelSpeed = HingePartSideGeometry.WheelAngularSpeed(bodyAngularVelocity: -1);
        const double linkSpeed = 0;

        var (sin, _) = AngleSensor.AngleInputs(link - wheel);
        var speed = AngleSensor.SpeedInput(linkSpeed - wheelSpeed);

        sin.ShouldBe(-1, tolerance: 1e-12);
        speed.ShouldBeLessThan(0);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2 * Math.PI, 0.7615941559557649)]
    [InlineData(-2 * Math.PI, -0.7615941559557649)]
    [InlineData(Math.PI, 0.46211715726000974)]
    public void SpeedInput_IsTanhOfTheSpeedOverSixtyRpm(double speed, double expected) =>
        AngleSensor.SpeedInput(speed).ShouldBe(expected, tolerance: 1e-12);

    [Fact]
    public void SpeedInput_EqualsAMotors_OnlyWhenItsMaxSpeedIsSixtyRpm()
    {
        const double speed = 3;
        var sixtyRpm = new VelocityMotorDef(1, 2, maxSpeed: 2 * Math.PI);
        var faster = new VelocityMotorDef(1, 2, maxSpeed: 4 * Math.PI);

        var sensor = AngleSensor.SpeedInput(speed);

        sensor.ShouldBe(MotorLoop.SpeedInput(speed, sixtyRpm.MaxSpeed), tolerance: 1e-12);
        Math.Abs(sensor - MotorLoop.SpeedInput(speed, faster.MaxSpeed)).ShouldBeGreaterThan(1e-6);
    }
}
