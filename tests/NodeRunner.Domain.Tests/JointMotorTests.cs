namespace NodeRunner.Domain.Tests;

public sealed class JointMotorTests
{
    private const double _maxSpeed = 6;

    [Fact]
    public void AngleInput_IsZeroAsBuilt() =>
        JointMotor.AngleInput(relativeRotation: 1.2, builtRelativeRotation: 1.2).ShouldBe(0);

    [Fact]
    public void AngleInput_IsPositiveCounterClockwiseOnScreen()
    {
        // The creature's frame is y-down, so a negative rotation turns counter-clockwise on screen.
        JointMotor.AngleInput(-Math.PI / 2, 0).ShouldBe(0.5, tolerance: 1e-12);
        JointMotor.AngleInput(Math.PI / 2, 0).ShouldBe(-0.5, tolerance: 1e-12);
    }

    [Fact]
    public void AngleInput_MeasuresFromTheBuiltPose() =>
        JointMotor.AngleInput(relativeRotation: 0.5, builtRelativeRotation: 0.5 + (Math.PI / 4)).ShouldBe(0.25, tolerance: 1e-12);

    [Fact]
    public void AngleInput_WrapsAtHalfATurn()
    {
        // 190° counter-clockwise is 170° clockwise.
        JointMotor.AngleInput(-190 * Math.PI / 180, 0).ShouldBe(-170.0 / 180, tolerance: 1e-12);
        JointMotor.AngleInput(3 * Math.Tau, 0).ShouldBe(0, tolerance: 1e-12);
    }

    [Fact]
    public void SpeedInput_SaturatesSoftlyAtTheMotorsTopSpeed()
    {
        JointMotor.SpeedInput(0, _maxSpeed).ShouldBe(0);
        JointMotor.SpeedInput(-_maxSpeed, _maxSpeed).ShouldBe(Math.Tanh(1), tolerance: 1e-12);
        JointMotor.SpeedInput(_maxSpeed, _maxSpeed).ShouldBe(-Math.Tanh(1), tolerance: 1e-12);
        JointMotor.SpeedInput(-100 * _maxSpeed, _maxSpeed).ShouldBe(1, tolerance: 1e-9);
    }

    [Fact]
    public void TargetAngularVelocity_IsCounterClockwisePositiveAndClamped()
    {
        JointMotor.TargetAngularVelocity(0.5, _maxSpeed).ShouldBe(-3);
        JointMotor.TargetAngularVelocity(-2, _maxSpeed).ShouldBe(_maxSpeed);
    }

    [Fact]
    public void Channels_AreKeyedByTheTurningBeam()
    {
        JointMotor.AngleChannel(12).ShouldBe("angle:12");
        JointMotor.SpeedChannel(12).ShouldBe("speed:12");
        JointMotor.TargetChannel(12).ShouldBe("target:12");
    }
}
