using NodeRunner.Domain;

namespace NodeRunner.Mechanics.Tests;

public sealed class HingePartSideGeometryTests
{
    private const double _heavy = 1e12;

    // A link's angle and speed follow its far joint around the motor joint, y-down; a Wheel side turns like a link to
    // a mark on its tyre 20 units out (#1118), and a link's couple turns the body as the Wheel's body torque does.
    [Fact]
    public void ASide_TurnsWithItsFarJoint_OrAMarkOnItsTyre()
    {
        var hub = new Vector2D(0, 0);
        HingePartSideGeometry.LinkAngle(hub, new Vector2D(0, -20)).ShouldBe(Math.PI / 2, 1e-12);
        HingePartSideGeometry.LinkAngularSpeed(hub, new Vector2D(20, 0), hub, new Vector2D(0, -20)).ShouldBe(1, 1e-12);

        const double spin = 3;
        var mark = new Vector2D(0, 20);
        HingePartSideGeometry.WheelAngle(Math.PI / 2).ShouldBe(HingePartSideGeometry.LinkAngle(hub, mark), 1e-12);
        HingePartSideGeometry.WheelAngularSpeed(spin).ShouldBe(HingePartSideGeometry.LinkAngularSpeed(hub, mark, hub, new Vector2D(-20 * spin, 0)), 1e-12);
        HingePartSideGeometry.WheelBodyTorque(60).ShouldBe(20 * HingePartSideGeometry.CoupleForTorque(hub, new Vector2D(20, 0), torque: 60).ForceOnFar.Y, 1e-12);
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
            .ShouldBe(notch, 1e-12);

        // Held on the target side's groove, the fixed side's far joint is pulled onto the line at the notch.
        var onTarget = HingePartSideGeometry.WeldGrooveDirection(joint, fixedFar, overshoot, heldIsTarget: false);
        (HingePartSideGeometry.LinkAngle(joint, targetFar) - HingePartSideGeometry.LinkAngle(new Vector2D(0, 0), onTarget))
            .ShouldBe(notch, 1e-12);
        (Math.Sqrt((onTarget.X * onTarget.X) + (onTarget.Y * onTarget.Y))).ShouldBe(1, 1e-12);

        // As the creature welds two links: the held point is the target's, where the fixed side's far joint stands.
        var atFixedFar = HingePartSideGeometry.WeldGrooveDirection(joint, fixedFar, overshoot, heldIsTarget: true);
        HingePartSideGeometry.LinkAngle(new Vector2D(0, 0), atFixedFar).ShouldBe(-overshoot, 1e-12);
    }

    // A couple of equal, opposite forces with the asked moment, its force clamped on a very short lever arm; a link
    // shorter than the lever arm reads an inertia that shrinks with its length.
    [Fact]
    public void ACouple_TurnsALinkWithTheAskedTorque_ClampedOnAShortLeverArm()
    {
        var couple = HingePartSideGeometry.CoupleForTorque(new Vector2D(0, 0), new Vector2D(20, 0), torque: 60);
        (couple.ForceOnJoint, couple.ForceOnFar).ShouldBe((new Vector2D(0, 3), new Vector2D(0, -3)));
        var shortArm = HingePartSideGeometry.CoupleForTorque(new Vector2D(0, 0), new Vector2D(1, 0), torque: 30);
        (shortArm.ForceOnJoint, shortArm.ForceOnFar).ShouldBe((new Vector2D(0, 30 / HingePartSideGeometry.MinimumLeverArm), new Vector2D(0, -30 / HingePartSideGeometry.MinimumLeverArm)));

        var joint = new Vector2D(0, 0);
        HingePartSideGeometry.LinkInertia(joint, new Vector2D(30, 0), 1, 1).ShouldBe(0.5 * 30 * 30, 1e-9);
        HingePartSideGeometry.LinkInertia(joint, new Vector2D(1, 0), 1, 1).ShouldBe(0.5 * HingePartSideGeometry.MinimumLeverArm, 1e-9);
        HingePartSideGeometry.LinkInertia(joint, joint, 1, 1).ShouldBeInRange(double.Epsilon, 1e-3);
    }

    // Pushing a straight joint's node sideways turns both links: a quarter of m·L², where two links in series would
    // read half. At a right angle each link's push runs along the other, so it matches two links in series. Folded,
    // the joint is pushed both ways at once and only the far ends turn. The same either way, and positive.
    [Fact]
    public void SharedJointInertia_CountsTheJointOnce()
    {
        var joint = new Vector2D(0, 0);
        HingePartSideGeometry.SharedJointInertia(joint, new Vector2D(-100, 0), new Vector2D(100, 0), 2, _heavy, _heavy).ShouldBe(2 * 100 * 100 / 4.0, 1e-3);
        HingePartSideGeometry.SharedJointInertia(joint, new Vector2D(0, 100), new Vector2D(100, 0), 2, _heavy, _heavy).ShouldBe(2 * 100 * 100 / 2.0, 1e-3);
        HingePartSideGeometry.SharedJointInertia(joint, new Vector2D(100, 0), new Vector2D(100, 0), 2, 3, 6).ShouldBe(3.0 * 6 / 9 * 100 * 100, 1e-6);
        var fixedSide = HingePartSideGeometry.LinkInertia(joint, new Vector2D(0, 100), 2, 3);
        var targetSide = HingePartSideGeometry.LinkInertia(joint, new Vector2D(100, 0), 2, 5);
        var series = fixedSide * targetSide / (fixedSide + targetSide);
        HingePartSideGeometry.SharedJointInertia(joint, new Vector2D(0, 100), new Vector2D(100, 0), 2, 3, 5).ShouldBe(series, 1e-9 * series);

        var a = new Vector2D(-40, 90);
        var b = new Vector2D(130, -15);
        HingePartSideGeometry.SharedJointInertia(new Vector2D(10, 20), a, b, 1.2, 0.6, 3).ShouldBe(HingePartSideGeometry.SharedJointInertia(new Vector2D(10, 20), b, a, 1.2, 3, 0.6), 1e-9);
        HingePartSideGeometry.SharedJointInertia(joint, joint, joint, 1, 1, 1).ShouldBeGreaterThan(0);
    }

    // Crossing π it stays continuous, and it counts a full turn.
    [Fact]
    public void UnwrapAngle_StaysContinuous_OverAFullTurn()
    {
        HingePartSideGeometry.UnwrapAngle(Math.PI - 0.1, Math.PI - 0.1, -Math.PI + 0.1).ShouldBe(Math.PI + 0.1, 1e-12);
        var continuous = 0.0;
        var previous = 0.0;
        foreach (var current in new[] { Math.PI / 2, Math.PI - 0.1, -Math.PI + 0.1, -Math.PI / 2, 0 })
        {
            continuous = HingePartSideGeometry.UnwrapAngle(continuous, previous, current);
            previous = current;
        }

        continuous.ShouldBe(Math.Tau, 1e-12);
    }
}
