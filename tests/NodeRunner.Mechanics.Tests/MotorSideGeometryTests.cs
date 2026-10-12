using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class MotorSideGeometryTests
{
    [Fact]
    public void LinkAngleAndSpeed_FollowTheFarJointAroundTheMotorJoint()
    {
        var joint = new Vector2D(0, 0);
        var right = new Vector2D(20, 0);
        var up = new Vector2D(0, -20);

        MotorSideGeometry.LinkAngle(joint, up).ShouldBe(Math.PI / 2, tolerance: 1e-12);
        MotorSideGeometry.LinkAngularSpeed(joint, right, new Vector2D(0, 0), new Vector2D(0, -20))
            .ShouldBe(1, tolerance: 1e-12);
    }

    [Fact]
    public void WheelSide_TurnsLikeALinkToAMarkOnItsTyre()
    {
        // A mark on the tyre, 20 units right of the hub at rotation 0, read as a link's far joint (#1118).
        var hub = new Vector2D(0, 0);
        const double rotation = Math.PI / 2;
        const double spin = 3;
        var mark = new Vector2D(20 * Math.Cos(rotation), 20 * Math.Sin(rotation));
        var markVelocity = new Vector2D(-20 * spin * Math.Sin(rotation), 20 * spin * Math.Cos(rotation));

        MotorSideGeometry.WheelAngle(rotation).ShouldBe(MotorSideGeometry.LinkAngle(hub, mark), tolerance: 1e-12);
        MotorSideGeometry.WheelAngularSpeed(spin)
            .ShouldBe(MotorSideGeometry.LinkAngularSpeed(hub, mark, new Vector2D(0, 0), markVelocity), tolerance: 1e-12);

        // The couple a link would get turns the body as the Wheel's body torque does: r × F, y-down.
        var couple = MotorSideGeometry.CoupleForTorque(hub, new Vector2D(20, 0), torque: 60);
        MotorSideGeometry.WheelBodyTorque(60).ShouldBe(20 * couple.ForceOnFar.Y, tolerance: 1e-12);
    }

    [Fact]
    public void CoupleForTorque_ReturnsEqualOppositeForcesWithTheRequestedMoment()
    {
        var couple = MotorSideGeometry.CoupleForTorque(new Vector2D(0, 0), new Vector2D(20, 0), torque: 60);

        couple.ForceOnJoint.ShouldBe(new Vector2D(0, 3));
        couple.ForceOnFar.ShouldBe(new Vector2D(0, -3));
        (-(20 * couple.ForceOnFar.Y) + (0 * couple.ForceOnFar.X)).ShouldBe(60);
    }

    [Fact]
    public void CoupleForTorque_ClampsForceWhenTheLeverArmIsVeryShort()
    {
        var couple = MotorSideGeometry.CoupleForTorque(new Vector2D(0, 0), new Vector2D(1, 0), torque: 30);

        couple.ForceOnFar.ShouldBe(new Vector2D(0, -30 / MotorSideGeometry.MinimumLeverArm));
        couple.ForceOnJoint.ShouldBe(new Vector2D(0, 30 / MotorSideGeometry.MinimumLeverArm));
    }

    [Fact]
    public void LinkInertia_ForALinkShorterThanTheLeverArm_ShrinksWithItsLength()
    {
        var joint = new Vector2D(0, 0);
        var reducedMass = 0.5;

        MotorSideGeometry.LinkInertia(joint, new Vector2D(30, 0), 1, 1).ShouldBe(reducedMass * 30 * 30, tolerance: 1e-9);
        MotorSideGeometry.LinkInertia(joint, new Vector2D(1, 0), 1, 1).ShouldBe(reducedMass * 1 * MotorSideGeometry.MinimumLeverArm, tolerance: 1e-9);
        MotorSideGeometry.LinkInertia(joint, joint, 1, 1).ShouldBeGreaterThan(0);
        MotorSideGeometry.LinkInertia(joint, joint, 1, 1).ShouldBeLessThan(1e-3);
    }

    [Fact]
    public void UnwrapAngle_CrossingPi_StaysContinuous()
    {
        var previous = Math.PI - 0.1;
        var current = -Math.PI + 0.1;

        MotorSideGeometry.UnwrapAngle(previous, previous, current).ShouldBe(Math.PI + 0.1, tolerance: 1e-12);
    }

    [Fact]
    public void UnwrapAngle_AllowsAFullTurnRange()
    {
        var continuous = 0.0;
        var previous = 0.0;
        foreach (var current in new[] { Math.PI / 2, Math.PI - 0.1, -Math.PI + 0.1, -Math.PI / 2, 0 })
        {
            continuous = MotorSideGeometry.UnwrapAngle(continuous, previous, current);
            previous = current;
        }

        continuous.ShouldBe(Math.Tau, tolerance: 1e-12);
    }
}
