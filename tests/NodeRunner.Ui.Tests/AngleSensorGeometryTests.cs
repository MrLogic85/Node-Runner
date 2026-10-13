using Godot;
using NodeRunner.Domain;
using NodeRunner.Mechanics;
using NodeRunner.Theme;

namespace NodeRunner.Ui.Tests;

/// <summary>The Angle sensor's ring and arc (#1205, <c>docs/WORLD_VISUALS.md</c> → Angle sensor), checked on its geometry.</summary>
public sealed class AngleSensorGeometryTests
{
    private const float _tolerance = 1e-4f;
    private const float _quarter = Mathf.Pi / 2;
    private const float _justUnder = 1e-3f;

    private static readonly float _bare = AngleSensorGeometry.ArcRadius(rubber: false, touch: false);

    [Fact]
    public void Ring_StandsInForAPlainJoint()
    {
        AngleSensorGeometry.Radius.ShouldBe((float)NodeDef.PlainJointRadius);
    }

    // Godot's y points down, so a counter-clockwise turn on screen is a falling Godot rotation.
    [Fact]
    public void QuarterTurns_EitherWay_DrawTheSameArcMirrored()
    {
        var counterClockwise = AngleSensorGeometry.ArcPoints(_bare, 0, -_quarter);
        var clockwise = AngleSensorGeometry.ArcPoints(_bare, 0, _quarter);

        AngleSensorGeometry.Sweep(0, -_quarter).ShouldBe(-_quarter, _tolerance);
        AngleSensorGeometry.Sweep(0, _quarter).ShouldBe(_quarter, _tolerance);
        ShouldBeNear(counterClockwise[^1], new Vector2(0, -_bare));
        ShouldBeNear(clockwise[^1], new Vector2(0, _bare));
        clockwise.Length.ShouldBe(counterClockwise.Length);
        ShouldBeNear(clockwise, [.. counterClockwise.Select(point => new Vector2(point.X, -point.Y))]);
    }

    [Fact]
    public void JustEitherSideOfAHalfTurn_TheArcFlipsSides()
    {
        var under = AngleSensorGeometry.Sweep(0, -(Mathf.Pi - _justUnder));
        var over = AngleSensorGeometry.Sweep(0, -(Mathf.Pi + _justUnder));

        under.ShouldBe(-(Mathf.Pi - _justUnder), _tolerance);
        over.ShouldBe(Mathf.Pi - _justUnder, _tolerance);
        AngleSensorGeometry.ArcPoints(_bare, 0, -(Mathf.Pi - _justUnder))[1].Y.ShouldBeLessThan(0);
        AngleSensorGeometry.ArcPoints(_bare, 0, -(Mathf.Pi + _justUnder))[1].Y.ShouldBeGreaterThan(0);
    }

    [Theory]
    [InlineData(Mathf.Pi)]
    [InlineData(-Mathf.Pi)]
    public void ExactlyAHalfTurn_RunsCounterClockwise(float target)
    {
        AngleSensorGeometry.Sweep(0, target).ShouldBe(-Mathf.Pi, _tolerance);
        AngleSensorGeometry.ArcPoints(_bare, 0, target)[1].Y.ShouldBeLessThan(0);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(3, 7)]
    [InlineData(7, 24)]
    [InlineData(-1, 2)]
    [InlineData(1, 0)]
    public void AStraightLimb_RunsCounterClockwiseWhicheverWayItWasBuilt(float x, float y)
    {
        var one = (float)Math.Atan2(y, x);
        var other = (float)Math.Atan2(-y, -x);

        AngleSensorGeometry.Sweep(one, other).ShouldBeLessThan(0);
        AngleSensorGeometry.Sweep(other, one).ShouldBeLessThan(0);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1.2f)]
    [InlineData(-2.5f)]
    public void SidesInLine_DrawADotOnTheirLine(float rotation)
    {
        AngleSensorGeometry.Sweep(rotation, rotation).ShouldBe(0, _tolerance);
        AngleSensorGeometry.ArcPoints(_bare, rotation, rotation).ShouldBe([Vector2.FromAngle(rotation) * _bare]);
        AngleSensorGeometry.ArcWidth.ShouldBe(2);
    }

    [Theory]
    [InlineData(0.4f, 1.9f)]
    [InlineData(-0.7f, 2.6f)]
    [InlineData(2f, -0.3f)]
    public void AFullTurnOfEitherSide_DrawsTheSamePose(float fixedRotation, float targetRotation)
    {
        var pose = AngleSensorGeometry.ArcPoints(_bare, fixedRotation, targetRotation);

        ShouldBeNear(AngleSensorGeometry.ArcPoints(_bare, fixedRotation, targetRotation + Mathf.Tau), pose);
        ShouldBeNear(AngleSensorGeometry.ArcPoints(_bare, fixedRotation - Mathf.Tau, targetRotation), pose);
    }

    [Fact]
    public void Arc_RunsFromTheFixedSideToTheTargetSide()
    {
        var arc = AngleSensorGeometry.ArcPoints(_bare, 0.3f, 1.4f);

        ShouldBeNear(arc[0], Vector2.FromAngle(0.3f) * _bare);
        ShouldBeNear(arc[^1], Vector2.FromAngle(1.4f) * _bare);
        arc.ShouldAllBe(point => Mathf.IsEqualApprox(point.Length(), _bare, _tolerance));
    }

    // The port bars show the sine and cosine of the brain's angle, the Target link's direction minus the Fixed
    // link's, counter-clockwise (MotorSideGeometry); the arc must span that same angle on screen.
    [Theory]
    [InlineData(0, -10)]
    [InlineData(0, 10)]
    [InlineData(-10, 0)]
    public void Angle_AgreesWithThePortBars(double targetX, double targetY)
    {
        var joint = new Vector2D(0, 0);
        var fixedFar = new Vector2D(10, 0);
        var targetFar = new Vector2D(targetX, targetY);
        var brainAngle = MotorSideGeometry.LinkAngle(joint, targetFar) - MotorSideGeometry.LinkAngle(joint, fixedFar);
        var (sin, cos) = AngleSensor.AngleInputs(brainAngle);

        var angle = AngleSensorGeometry.Angle(ScreenRotation(fixedFar), ScreenRotation(targetFar));

        Mathf.Sin(angle).ShouldBe((float)sin, _tolerance);
        Mathf.Cos(angle).ShouldBe((float)cos, _tolerance);
        ShouldBeNear(
            AngleSensorGeometry.ArcPoints(_bare, ScreenRotation(fixedFar), ScreenRotation(targetFar))[^1],
            new Vector2((float)targetX, (float)targetY).Normalized() * _bare);
    }

    [Theory]
    [InlineData(false, false, 19)]
    [InlineData(true, false, 22)]
    [InlineData(false, true, 24)]
    [InlineData(true, true, 24)]
    public void ArcRadius_LiesAGapOutsideTheJointsOutermostPart(bool rubber, bool touch, float radius)
    {
        AngleSensorGeometry.ArcRadius(rubber, touch).ShouldBe(radius);
    }

    [Fact]
    public void Halo_LiesAGapOutsideTheArc()
    {
        AngleSensorGeometry.HaloRadius(_bare).ShouldBe(24);
        AngleSensorGeometry.HaloRadius(AngleSensorGeometry.ArcRadius(rubber: false, touch: true)).ShouldBe(29);
    }

    // A Wheel side's direction is its tube 0 (#1118), which turns with the Wheel's body.
    [Theory]
    [InlineData(0f)]
    [InlineData(0.8f)]
    public void AWheelSide_StartsOnItsFirstTube(float wheelBodyRotation)
    {
        var wheelSide = (float)-MotorSideGeometry.WheelAngle(wheelBodyRotation);

        ShouldBeNear(
            AngleSensorGeometry.ArcPoints(_bare, wheelSide, 2f)[0],
            Vector2.FromAngle(WheelPart.CueAngles()[0] + wheelBodyRotation) * _bare);
    }

    [Fact]
    public void AMissingSide_DrawsNoArc()
    {
        AngleSensorGeometry.ArcPoints(_bare, null, 1f).ShouldBeEmpty();
        AngleSensorGeometry.ArcPoints(_bare, 1f, null).ShouldBeEmpty();
    }

    private static void ShouldBeNear(Vector2 actual, Vector2 expected) =>
        actual.DistanceTo(expected).ShouldBeLessThan(_tolerance, $"{actual} is not {expected}");

    private static void ShouldBeNear(Vector2[] actual, Vector2[] expected)
    {
        actual.Length.ShouldBe(expected.Length);
        foreach (var (point, expectedPoint) in actual.Zip(expected))
        {
            ShouldBeNear(point, expectedPoint);
        }
    }

    // A side's Godot rotation, as MotorSides.FixedRotation gives it: the model's angle negated.
    private static float ScreenRotation(Vector2D far) => (float)-MotorSideGeometry.LinkAngle(new Vector2D(0, 0), far);
}
