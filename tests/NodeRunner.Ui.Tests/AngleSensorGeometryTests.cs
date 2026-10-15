using Godot;
using NodeRunner.Creature;
using NodeRunner.Domain;
using NodeRunner.Mechanics;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>The Angle sensor's ring and arc (#1205, #1243, <c>docs/WORLD_VISUALS.md</c> → Angle sensor), checked on its geometry.</summary>
public sealed class AngleSensorGeometryTests
{
    private const float _tolerance = 1e-4f;
    private const float _quarter = Mathf.Pi / 2;
    private const float _justUnder = 1e-3f;

    private static readonly float _bare = (float)AngleSensorDef.ArcRadius(rubber: false, touch: false);

    [Fact]
    public void Ring_StandsInForAPlainJoint()
    {
        AngleSensorGeometry.Radius.ShouldBe((float)NodeDef.PlainJointRadius);
    }

    // Godot's y points down, so a counter-clockwise turn on screen is a falling Godot rotation.
    [Theory]
    [InlineData(20)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    [InlineData(340)]
    public void AtRest_TheArcRunsCounterClockwiseFromTheFixedSide(float degrees)
    {
        var target = -Mathf.DegToRad(degrees);
        var arc = AngleSensorGeometry.ArcPoints(_bare, 0, target);

        RestSweep(0, target).ShouldBe(target, _tolerance);
        arc[1].Y.ShouldBeLessThan(0);
        ShouldBeNear(arc[^1], Vector2.FromAngle(target) * _bare);
    }

    // So the part shows which side is Target (#1231).
    [Theory]
    [InlineData(0.35f, 0)]
    [InlineData(1, 2)]
    [InlineData(3, 7)]
    [InlineData(-1, 2)]
    [InlineData(1, 0)]
    public void ASwapOfTheSides_DrawsTheRestOfTheRing(float fixedRotation, float targetRotation)
    {
        var arc = AngleSensorGeometry.ArcPoints(_bare, fixedRotation, targetRotation);
        var swapped = AngleSensorGeometry.ArcPoints(_bare, targetRotation, fixedRotation);

        (RestSweep(fixedRotation, targetRotation) + RestSweep(targetRotation, fixedRotation)).ShouldBe(-Mathf.Tau, _tolerance);
        ShouldBeNear(swapped[0], arc[^1]);
        ShouldBeNear(swapped[^1], arc[0]);
    }

    [Fact]
    public void AtRest_ATargetJustClockwiseOfTheFixedSide_DrawsAnAlmostFullRing()
    {
        RestSweep(0, _justUnder).ShouldBe(-(Mathf.Tau - _justUnder), _tolerance);
    }

    // Sides in line can read a few ulps under a full turn, as their rotations are rounded separately.
    [Theory]
    [InlineData(0f)]
    [InlineData(1.2f)]
    [InlineData(-2.5f)]
    public void AtRest_SidesInLineReadJustUnderAFullTurn_DrawADot(float rotation)
    {
        var target = rotation + 1e-6f;

        AngleSensorGeometry.RestTurn(rotation, target).ShouldBe(-1);
        AngleSensorGeometry.ArcPoints(_bare, rotation, target).Length.ShouldBe(1);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1.2f)]
    [InlineData(-2.5f)]
    public void SidesInLine_DrawADotOnTheirLine(float rotation)
    {
        RestSweep(rotation, rotation).ShouldBe(0, _tolerance);
        AngleSensorGeometry.ArcPoints(_bare, rotation, rotation).ShouldBe([Vector2.FromAngle(rotation) * _bare]);
        AngleSensorGeometry.ArcWidth.ShouldBe(2);
    }

    // The angle counted on across whole turns, counter-clockwise; the expected sweep is a Godot rotation, so
    // filling counter-clockwise is negative and emptying positive.
    [Theory]
    [InlineData(-720, 0)]
    [InlineData(-370, -350)]
    [InlineData(-360, 360)]
    [InlineData(-190, 190)]
    [InlineData(-10, 10)]
    [InlineData(0, 0)]
    [InlineData(10, -10)]
    [InlineData(180, -180)]
    [InlineData(190, -190)]
    [InlineData(350, -350)]
    [InlineData(360, 360)]
    [InlineData(370, 350)]
    [InlineData(540, 180)]
    [InlineData(710, 10)]
    [InlineData(720, 0)]
    public void EvenTurnsFill_OddTurnsEmpty(int degrees, int sweepDegrees)
    {
        var turn = (int)Math.Floor(degrees / 360.0);
        var target = -Mathf.DegToRad(degrees - (360 * turn));

        AngleSensorGeometry.Sweep(0, target, turn).ShouldBe(Mathf.DegToRad(sweepDegrees), _tolerance);
    }

    // As Training draws it (AngleSensorVisual): the count follows every physics tick a tick behind the
    // bodies (Creature._PhysicsProcess runs before Godot's step), and a slow frame runs up to 8 ticks, so
    // a 240 rpm motor (24° a tick) moves the sides over half a turn between frames.
    [Theory]
    [InlineData(1, false, 0, 1, 1)]
    [InlineData(-1, false, 0, 1, 1)]
    [InlineData(1, true, 0, 1, 1)]
    [InlineData(-1, true, 0, 1, 1)]
    [InlineData(1, false, 350, 1, 1)]
    [InlineData(-1, true, 20, 1, 1)]
    [InlineData(1, false, 0, 24, 8)]
    [InlineData(-1, false, 0, 24, 8)]
    [InlineData(1, true, 90, 24, 8)]
    [InlineData(-1, true, 300, 24, 8)]
    [InlineData(1, false, 10, 170, 1)]
    [InlineData(-1, true, 10, 170, 3)]
    public void TurningOnAndOn_TheArcNeverJumps(int way, bool fixedSideTurns, int builtDegrees, int degreesPerTick, int ticksPerFrame)
    {
        const float start = 0.7f;
        var builtTarget = start - Mathf.DegToRad(builtDegrees);
        var restAngle = AngleSensorGeometry.Angle(start, builtTarget);
        var restTurn = AngleSensorGeometry.RestTurn(start, builtTarget);
        var built = (double)start - builtTarget;
        var continuous = built;
        var lastRelative = built;
        var moved = 0;
        var frameStep = Mathf.DegToRad(degreesPerTick * ticksPerFrame);
        float? sweep = null;
        var previousFixed = start;
        while (moved < 2000)
        {
            for (var tick = 0; tick < ticksPerFrame; tick++)
            {
                var (fixedNow, targetNow) = Pose(moved);
                var relative = (double)fixedNow - targetNow;
                continuous = HingePartSideGeometry.UnwrapAngle(continuous, lastRelative, relative);
                lastRelative = relative;
                moved += degreesPerTick;
            }

            var (fixedRotation, targetRotation) = Pose(moved);
            var turn = AngleSensorTurn.At(restAngle, restTurn, continuous - built, AngleSensorGeometry.Angle(fixedRotation, targetRotation));
            var nextSweep = AngleSensorGeometry.Sweep(fixedRotation, targetRotation, turn);
            var arc = AngleSensorGeometry.ArcPoints(_bare, fixedRotation, targetRotation, turn);
            var at = $"at {way * moved}°";

            // The arc's length and middle change by at most the frame's step; a full ring drawn either way
            // round is the same ring, so compare what it covers.
            if (sweep is { } previous)
            {
                Mathf.Abs(Mathf.Abs(nextSweep) - Mathf.Abs(previous)).ShouldBeLessThan(frameStep + _tolerance, at);
                Mathf.Abs(Mathf.AngleDifference(Middle(fixedRotation, nextSweep), Middle(previousFixed, previous)))
                    .ShouldBeLessThan((frameStep / 2) + _tolerance, at);
            }

            // Counted from the built pose, it fills on even turns and empties on odd ones.
            var counted = builtDegrees + (way * moved);
            var expected = Mathf.DegToRad(Mathf.PosMod(counted, 720) < 360 ? -Mathf.PosMod(counted, 360) : 360 - Mathf.PosMod(counted, 360));
            Mathf.Abs(nextSweep).ShouldBe(Mathf.Abs(expected), _tolerance, at);
            ShouldBeNear(arc[0], Vector2.FromAngle(fixedRotation) * _bare);
            ShouldBeNear(arc[^1], Vector2.FromAngle(targetRotation) * _bare);
            sweep = nextSweep;
            previousFixed = fixedRotation;
        }

        (float Fixed, float Target) Pose(int degrees)
        {
            var turned = way * Mathf.DegToRad(degrees);
            return fixedSideTurns ? (start + turned, builtTarget) : (start, builtTarget - turned);
        }
    }

    [Fact]
    public void SidesInLineReadJustUnderAFullTurn_StayOnTheirRestTurnInTraining()
    {
        var angle = AngleSensorGeometry.Angle(0, 1e-6f);

        AngleSensorTurn.At(angle, AngleSensorGeometry.RestTurn(0, 1e-6f), 0, angle).ShouldBe(-1);
    }

    // The Domain sets the widths the arc and halo are drawn with, so Build's rings can clear the arc (#1206);
    // they stay the stroke-signal token, as every other selection ring (VisualTheme.SelectionRingWidth).
    [Fact]
    public void ArcAndHalo_AreDrawnStrokeSignalWide()
    {
        AngleSensorGeometry.ArcWidth.ShouldBe(UiSize.Stroke.Signal);
        AngleSensorDef.HaloWidth.ShouldBe(UiSize.Stroke.Signal);
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

    // The port bars show the sine and cosine of the network's angle, the Target link's direction minus the Fixed
    // link's, counter-clockwise (HingePartSideGeometry); the arc must span that same angle on screen.
    [Theory]
    [InlineData(0, -10)]
    [InlineData(0, 10)]
    [InlineData(-10, 0)]
    public void Angle_AgreesWithThePortBars(double targetX, double targetY)
    {
        var joint = new Vector2D(0, 0);
        var fixedFar = new Vector2D(10, 0);
        var targetFar = new Vector2D(targetX, targetY);
        var networkAngle = HingePartSideGeometry.LinkAngle(joint, targetFar) - HingePartSideGeometry.LinkAngle(joint, fixedFar);
        var (sin, cos) = AngleSensor.AngleInputs(networkAngle);

        var angle = AngleSensorGeometry.Angle(ScreenRotation(fixedFar), ScreenRotation(targetFar));

        Mathf.Sin(angle).ShouldBe((float)sin, _tolerance);
        Mathf.Cos(angle).ShouldBe((float)cos, _tolerance);
        ShouldBeNear(
            AngleSensorGeometry.ArcPoints(_bare, ScreenRotation(fixedFar), ScreenRotation(targetFar))[^1],
            new Vector2((float)targetX, (float)targetY).Normalized() * _bare);
    }

    // A Wheel side's direction is its tube 0 (#1118), which turns with the Wheel's body.
    [Theory]
    [InlineData(0f)]
    [InlineData(0.8f)]
    public void AWheelSide_StartsOnItsFirstTube(float wheelBodyRotation)
    {
        var wheelSide = (float)-HingePartSideGeometry.WheelAngle(wheelBodyRotation);

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

    private static float Middle(float fixedRotation, float sweep) => fixedRotation + (sweep / 2);

    private static float RestSweep(float fixedRotation, float targetRotation) =>
        AngleSensorGeometry.Sweep(fixedRotation, targetRotation, AngleSensorGeometry.RestTurn(fixedRotation, targetRotation));

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

    // A side's Godot rotation, as HingePartSides.FixedRotation gives it: the model's angle negated.
    private static float ScreenRotation(Vector2D far) => (float)-HingePartSideGeometry.LinkAngle(new Vector2D(0, 0), far);
}
