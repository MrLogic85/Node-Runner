using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class HingePartSideGeometryTests
{
    private const double _heavy = 1e12;

    [Fact]
    public void LinkAngleAndSpeed_FollowTheFarJointAroundTheMotorJoint()
    {
        var joint = new Vector2D(0, 0);
        var right = new Vector2D(20, 0);
        var up = new Vector2D(0, -20);

        HingePartSideGeometry.LinkAngle(joint, up).ShouldBe(Math.PI / 2, tolerance: 1e-12);
        HingePartSideGeometry.LinkAngularSpeed(joint, right, new Vector2D(0, 0), new Vector2D(0, -20))
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

        HingePartSideGeometry.WheelAngle(rotation).ShouldBe(HingePartSideGeometry.LinkAngle(hub, mark), tolerance: 1e-12);
        HingePartSideGeometry.WheelAngularSpeed(spin)
            .ShouldBe(HingePartSideGeometry.LinkAngularSpeed(hub, mark, new Vector2D(0, 0), markVelocity), tolerance: 1e-12);

        // The couple a link would get turns the body as the Wheel's body torque does: r × F, y-down.
        var couple = HingePartSideGeometry.CoupleForTorque(hub, new Vector2D(20, 0), torque: 60);
        HingePartSideGeometry.WheelBodyTorque(60).ShouldBe(20 * couple.ForceOnFar.Y, tolerance: 1e-12);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(-0.05)]
    public void WeldGrooveDirection_HoldsTheTargetSideAtTheNotch(double overshoot)
    {
        // The fixed side points right; the target was caught overshoot past a notch at 0.5 rad.
        const double notch = 0.5;
        var joint = new Vector2D(10, 20);
        var fixedFar = new Vector2D(30, 20);
        var caught = notch + overshoot;
        var targetFar = new Vector2D(10 + (20 * Math.Cos(caught)), 20 - (20 * Math.Sin(caught)));

        // Held on the fixed side's groove, the target's far joint is pulled onto the line at the notch.
        var onFixed = HingePartSideGeometry.WeldGrooveDirection(joint, targetFar, overshoot, heldIsTarget: true);
        (HingePartSideGeometry.LinkAngle(new Vector2D(0, 0), onFixed) - HingePartSideGeometry.LinkAngle(joint, fixedFar))
            .ShouldBe(notch, tolerance: 1e-12);

        // Held on the target side's groove, the fixed side's far joint is pulled onto the line at the notch.
        var onTarget = HingePartSideGeometry.WeldGrooveDirection(joint, fixedFar, overshoot, heldIsTarget: false);
        (HingePartSideGeometry.LinkAngle(joint, targetFar) - HingePartSideGeometry.LinkAngle(new Vector2D(0, 0), onTarget))
            .ShouldBe(notch, tolerance: 1e-12);
        (Math.Sqrt((onTarget.X * onTarget.X) + (onTarget.Y * onTarget.Y))).ShouldBe(1, tolerance: 1e-12);

        // As the creature welds two links: the held point is the target's, where the fixed side's far joint stands.
        var atFixedFar = HingePartSideGeometry.WeldGrooveDirection(joint, fixedFar, overshoot, heldIsTarget: true);
        HingePartSideGeometry.LinkAngle(new Vector2D(0, 0), atFixedFar).ShouldBe(-overshoot, tolerance: 1e-12);
    }

    [Fact]
    public void CoupleForTorque_ReturnsEqualOppositeForcesWithTheRequestedMoment()
    {
        var couple = HingePartSideGeometry.CoupleForTorque(new Vector2D(0, 0), new Vector2D(20, 0), torque: 60);

        couple.ForceOnJoint.ShouldBe(new Vector2D(0, 3));
        couple.ForceOnFar.ShouldBe(new Vector2D(0, -3));
        (-(20 * couple.ForceOnFar.Y) + (0 * couple.ForceOnFar.X)).ShouldBe(60);
    }

    [Fact]
    public void CoupleForTorque_ClampsForceWhenTheLeverArmIsVeryShort()
    {
        var couple = HingePartSideGeometry.CoupleForTorque(new Vector2D(0, 0), new Vector2D(1, 0), torque: 30);

        couple.ForceOnFar.ShouldBe(new Vector2D(0, -30 / HingePartSideGeometry.MinimumLeverArm));
        couple.ForceOnJoint.ShouldBe(new Vector2D(0, 30 / HingePartSideGeometry.MinimumLeverArm));
    }

    [Fact]
    public void LinkInertia_ForALinkShorterThanTheLeverArm_ShrinksWithItsLength()
    {
        var joint = new Vector2D(0, 0);
        var reducedMass = 0.5;

        HingePartSideGeometry.LinkInertia(joint, new Vector2D(30, 0), 1, 1).ShouldBe(reducedMass * 30 * 30, tolerance: 1e-9);
        HingePartSideGeometry.LinkInertia(joint, new Vector2D(1, 0), 1, 1).ShouldBe(reducedMass * 1 * HingePartSideGeometry.MinimumLeverArm, tolerance: 1e-9);
        HingePartSideGeometry.LinkInertia(joint, joint, 1, 1).ShouldBeGreaterThan(0);
        HingePartSideGeometry.LinkInertia(joint, joint, 1, 1).ShouldBeLessThan(1e-3);
    }

    [Fact]
    public void SharedJointInertia_StraightBetweenHeavyEnds_CountsTheJointOnce()
    {
        // Pushing a straight joint's node sideways turns both links: a quarter of m·L², where two
        // links in series would read half.
        HingePartSideGeometry.SharedJointInertia(new Vector2D(0, 0), new Vector2D(-100, 0), new Vector2D(100, 0), 2, _heavy, _heavy)
            .ShouldBe(2 * 100 * 100 / 4.0, tolerance: 1e-3);
    }

    [Fact]
    public void SharedJointInertia_AtARightAngle_MatchesTwoLinksInSeries()
    {
        var joint = new Vector2D(0, 0);
        var fixedFar = new Vector2D(0, 100);
        var targetFar = new Vector2D(100, 0);
        var fixedSide = HingePartSideGeometry.LinkInertia(joint, fixedFar, 2, 3);
        var targetSide = HingePartSideGeometry.LinkInertia(joint, targetFar, 2, 5);

        var series = fixedSide * targetSide / (fixedSide + targetSide);

        // Each link's push on the joint runs along the other link, so neither turns the other.
        HingePartSideGeometry.SharedJointInertia(joint, fixedFar, targetFar, 2, 3, 5).ShouldBe(series, tolerance: 1e-9 * series);
        HingePartSideGeometry.SharedJointInertia(joint, fixedFar, targetFar, 2, _heavy, _heavy)
            .ShouldBe(2 * 100 * 100 / 2.0, tolerance: 1e-3);
    }

    [Fact]
    public void SharedJointInertia_Folded_TurnsOnlyTheFarEnds()
    {
        // Both links point the same way, so the joint is pushed both ways at once and stays put.
        HingePartSideGeometry.SharedJointInertia(new Vector2D(0, 0), new Vector2D(100, 0), new Vector2D(100, 0), 2, 3, 6)
            .ShouldBe(3.0 * 6 / 9 * 100 * 100, tolerance: 1e-6);
    }

    [Fact]
    public void SharedJointInertia_IsTheSameEitherWay_AndPositive()
    {
        var joint = new Vector2D(10, 20);
        var a = new Vector2D(-40, 90);
        var b = new Vector2D(130, -15);

        HingePartSideGeometry.SharedJointInertia(joint, a, b, 1.2, 0.6, 3).ShouldBe(HingePartSideGeometry.SharedJointInertia(joint, b, a, 1.2, 3, 0.6), tolerance: 1e-9);
        HingePartSideGeometry.SharedJointInertia(joint, joint, joint, 1, 1, 1).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void UnwrapAngle_CrossingPi_StaysContinuous()
    {
        var previous = Math.PI - 0.1;
        var current = -Math.PI + 0.1;

        HingePartSideGeometry.UnwrapAngle(previous, previous, current).ShouldBe(Math.PI + 0.1, tolerance: 1e-12);
    }

    [Fact]
    public void UnwrapAngle_AllowsAFullTurnRange()
    {
        var continuous = 0.0;
        var previous = 0.0;
        foreach (var current in new[] { Math.PI / 2, Math.PI - 0.1, -Math.PI + 0.1, -Math.PI / 2, 0 })
        {
            continuous = HingePartSideGeometry.UnwrapAngle(continuous, previous, current);
            previous = current;
        }

        continuous.ShouldBe(Math.Tau, tolerance: 1e-12);
    }
}
