using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class MotorSideGeometryTests
{
    private const double _heavy = 1e12;

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
    public void SharedJointInertia_StraightBetweenHeavyEnds_CountsTheJointOnce()
    {
        // Pushing a straight joint's node sideways turns both links: a quarter of m·L², where two
        // links in series would read half.
        MotorSideGeometry.SharedJointInertia(new Vector2D(0, 0), new Vector2D(-100, 0), new Vector2D(100, 0), 2, _heavy, _heavy)
            .ShouldBe(2 * 100 * 100 / 4.0, tolerance: 1e-3);
    }

    [Fact]
    public void SharedJointInertia_AtARightAngle_MatchesTwoLinksInSeries()
    {
        var joint = new Vector2D(0, 0);
        var fixedFar = new Vector2D(0, 100);
        var targetFar = new Vector2D(100, 0);
        var fixedSide = MotorSideGeometry.LinkInertia(joint, fixedFar, 2, 3);
        var targetSide = MotorSideGeometry.LinkInertia(joint, targetFar, 2, 5);

        var series = fixedSide * targetSide / (fixedSide + targetSide);

        // Each link's push on the joint runs along the other link, so neither turns the other.
        MotorSideGeometry.SharedJointInertia(joint, fixedFar, targetFar, 2, 3, 5).ShouldBe(series, tolerance: 1e-9 * series);
        MotorSideGeometry.SharedJointInertia(joint, fixedFar, targetFar, 2, _heavy, _heavy)
            .ShouldBe(2 * 100 * 100 / 2.0, tolerance: 1e-3);
    }

    [Fact]
    public void SharedJointInertia_Folded_TurnsOnlyTheFarEnds()
    {
        // Both links point the same way, so the joint is pushed both ways at once and stays put.
        MotorSideGeometry.SharedJointInertia(new Vector2D(0, 0), new Vector2D(100, 0), new Vector2D(100, 0), 2, 3, 6)
            .ShouldBe(3.0 * 6 / 9 * 100 * 100, tolerance: 1e-6);
    }

    [Fact]
    public void SharedJointInertia_IsTheSameEitherWay_AndPositive()
    {
        var joint = new Vector2D(10, 20);
        var a = new Vector2D(-40, 90);
        var b = new Vector2D(130, -15);

        MotorSideGeometry.SharedJointInertia(joint, a, b, 1.2, 0.6, 3).ShouldBe(MotorSideGeometry.SharedJointInertia(joint, b, a, 1.2, 3, 0.6), tolerance: 1e-9);
        MotorSideGeometry.SharedJointInertia(joint, joint, joint, 1, 1, 1).ShouldBeGreaterThan(0);
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
