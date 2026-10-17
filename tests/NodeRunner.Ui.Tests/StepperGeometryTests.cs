using Godot;
using NodeRunner.Domain;
using NodeRunner.Theme;

namespace NodeRunner.Ui.Tests;

/// <summary>The Stepper's detent wheel and pawl (<c>docs/WORLD_VISUALS.md</c> → Stepper), checked on its geometry.</summary>
public sealed class StepperGeometryTests
{
    private const float _tolerance = 1e-3f;

    // How far a ring drawn in 96 chords sags inside its radius at the mark's 30: 30·(1 − cos(π/96)).
    private const float _chordSag = 0.02f;

    [Fact]
    public void It_stands_in_for_a_joint_motors_joint()
    {
        StepperGeometry.Radius.ShouldBe((float)JointMotor.Radius);
        StepperGeometry.RimRadius.ShouldBe(StepperGeometry.Radius - 1);
    }

    [Theory]
    [InlineData(8, 4.5f, 6)]
    [InlineData(12, 4.35f, 6)]
    [InlineData(24, 2.2f, 4.7f)]
    public void A_notchs_mouth_and_depth_shrink_with_its_spacing(int steps, float mouthHalfWidth, float depth)
    {
        // The spec gives them to a tenth of a unit.
        StepperGeometry.MouthHalfWidth(steps).ShouldBe(mouthHalfWidth, 0.05f);
        StepperGeometry.Depth(steps).ShouldBe(depth, 0.05f);
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(8, 1.3f)]
    [InlineData(24, -2.5f)]
    public void The_rim_has_a_notch_per_step_with_notch_0_on_the_built_angle(int steps, float builtAngle)
    {
        var outline = StepperGeometry.Outline(steps, builtAngle);
        var bottoms = Bottoms(outline, steps);

        bottoms.Length.ShouldBe(steps);
        AngleOff(bottoms[0].Angle(), builtAngle).ShouldBe(0, _tolerance);
        for (var notch = 0; notch < steps; notch++)
        {
            AngleOff(bottoms[notch].Angle(), StepperGeometry.NotchAngle(notch, steps, builtAngle)).ShouldBe(0, _tolerance);
        }

        outline[0].ShouldBe(outline[^1]);
        outline.ShouldAllBe(point => point.Length() <= StepperGeometry.RimRadius + _tolerance);
    }

    // Godot's y points down, so a notch counted counter-clockwise on screen, as the mechanics count it, lies at a falling rotation.
    [Fact]
    public void Notch_1_lies_counter_clockwise_on_screen_from_notch_0() =>
        StepperGeometry.NotchAngle(1, 8, 0).ShouldBe(-Mathf.Pi / 4, _tolerance);

    [Theory]
    [InlineData(-2.8f, 2.9f)]
    public void Built_notch_0_lies_on_the_target_with_both_sides(float fixedAngle, float targetAngle)
    {
        var (rim, pawl, built) = StepperGeometry.BuildAngles(fixedAngle, targetAngle);

        rim.ShouldBe(fixedAngle);
        pawl.ShouldBe(targetAngle);
        ShouldBeNear(Vector2.FromAngle(rim + StepperGeometry.NotchAngle(0, 8, built)), Vector2.FromAngle(targetAngle));
    }

    [Theory]
    [InlineData(0.5f, null, 0.5f)]
    [InlineData(null, 2f, 2f)]
    [InlineData(null, null, 0f)]
    public void With_a_side_missing_notch_0_lies_along_the_side_there_is(float? fixedAngle, float? targetAngle, float along)
    {
        var (rim, pawl, built) = StepperGeometry.BuildAngles(fixedAngle, targetAngle);

        rim.ShouldBe(along);
        pawl.ShouldBe(along);
        built.ShouldBe(0);
    }

    [Theory]
    [InlineData(24, 0.6f, true)]
    [InlineData(8, 0.34f, false)]
    public void Below_a_3px_mouth_the_rim_is_a_plain_circle(int steps, float pixelScale, bool plain) =>
        StepperGeometry.IsTooSmall(steps, pixelScale).ShouldBe(plain);

    [Theory]
    [InlineData(8, 4f)]
    public void A_shadows_rim_is_a_plain_circle_however_large(int steps, float pixelScale)
    {
        StepperGeometry.IsPlain(steps, pixelScale, simplified: true).ShouldBeTrue();
        StepperGeometry.IsPlain(steps, pixelScale, simplified: false).ShouldBeFalse();
    }

    [Theory]
    [InlineData(8)]
    public void A_seated_pawl_sits_in_its_notch_and_a_lifted_one_clears_the_rim(int steps)
    {
        var seated = StepperGeometry.Pawl(steps, seated: true);
        var lifted = StepperGeometry.Pawl(steps, seated: false);

        seated[0].ShouldBe(new Vector2(StepperGeometry.RimRadius - StepperGeometry.Depth(steps), 0));
        lifted[0].ShouldBe(new Vector2(StepperGeometry.Radius, 0));
        seated[1].ShouldBe(new Vector2(StepperGeometry.PawlBase, StepperGeometry.PawlHalfWidth));
        seated[2].ShouldBe(new Vector2(StepperGeometry.PawlBase, -StepperGeometry.PawlHalfWidth));
        StepperGeometry.PawlBase.ShouldBe(33);
        (2 * StepperGeometry.PawlHalfWidth).ShouldBe(12);
    }

    [Theory]
    [InlineData(-Mathf.Pi / 4)]
    [InlineData(-Mathf.Pi)]
    public void The_wedge_runs_from_the_pawl_to_its_notch_at_the_rim(float sweep)
    {
        var wedge = StepperGeometry.Wedge(sweep);

        wedge[0].ShouldBe(Vector2.Zero);
        ShouldBeNear(wedge[1], new Vector2(StepperGeometry.RimRadius, 0));
        ShouldBeNear(wedge[^1], Vector2.FromAngle(sweep) * StepperGeometry.RimRadius);
    }

    [Theory]
    [InlineData(2.1f)]
    public void The_selection_mark_leaves_the_ring_round_the_pawl(float pawlAngle)
    {
        var halo = StepperGeometry.Halo(keyhole: true).Select(point => point.Rotated(pawlAngle)).ToArray();
        var axis = Vector2.FromAngle(pawlAngle);
        var keyhole = halo.Where(point => point.Length() > StepperGeometry.Radius + (float)SelectionMarks.Gap + _tolerance).ToArray();

        halo[0].ShouldBe(halo[^1]);
        keyhole.Length.ShouldBe(2);
        keyhole.ShouldAllBe(point => MathF.Abs(point.Dot(axis) - StepperGeometry.KeyholeReach) < _tolerance);
        keyhole.ShouldAllBe(point => MathF.Abs(MathF.Abs(point.Cross(axis)) - StepperGeometry.KeyholeHalfWidth) < _tolerance);
        StepperGeometry.KeyholeReach.ShouldBe(37);
        StepperGeometry.KeyholeHalfWidth.ShouldBe(10);
    }

    [Theory]
    [InlineData(2, 0f, 0f, true)]
    [InlineData(24, -1f, -3f, true)]
    [InlineData(24, 0.2f, 0f, false)]
    public void The_selection_mark_keeps_its_gap_from_the_rim_and_the_pawl(int steps, float builtAngle, float pawlAngle, bool keyhole)
    {
        // The rim turns with the Fixed side and the mark with the pawl, so the rim lies at any angle under it.
        var halo = StepperGeometry.Halo(keyhole).Select(point => point.Rotated(pawlAngle)).ToArray();
        var rimEdge = StepperGeometry.Radius - StepperGeometry.RimRadius;

        Clearance(halo, StepperGeometry.Outline(steps, builtAngle)).ShouldBeGreaterThanOrEqualTo((float)SelectionMarks.Gap + rimEdge - _chordSag);
        if (keyhole)
        {
            foreach (var seated in new[] { true, false })
            {
                var pawl = StepperGeometry.Pawl(steps, seated).Select(point => point.Rotated(pawlAngle)).ToArray();
                Clearance(halo, pawl).ShouldBeGreaterThanOrEqualTo((float)SelectionMarks.Gap + StepperGeometry.PawlEdge - _chordSag);
            }
        }
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(8, false)]
    public void On_a_cushion_the_selection_ring_keeps_its_gap_from_the_cushion_and_the_pawl(int steps, bool seated)
    {
        var cushionEdge = StepperGeometry.Radius + (float)TouchSensorDef.Cushion;
        var pawlReach = StepperGeometry.Pawl(steps, seated).Max(point => point.Length());

        (StepperGeometry.CushionedHaloRadius - cushionEdge).ShouldBeGreaterThanOrEqualTo((float)SelectionMarks.Gap);
        (StepperGeometry.CushionedHaloRadius - pawlReach).ShouldBeGreaterThanOrEqualTo((float)SelectionMarks.Gap + StepperGeometry.PawlEdge - _tolerance);
    }

    [Fact]
    public void Without_a_pawl_the_selection_mark_is_a_joint_motors_halo_ring() =>
        StepperGeometry.Halo(keyhole: false).ShouldAllBe(point => MathF.Abs(point.Length() - (float)SelectionMarks.JointHalo(JointMotor.Radius)) < _tolerance);

    private static Vector2[] Bottoms(Vector2[] outline, int steps)
    {
        var bottom = StepperGeometry.RimRadius - StepperGeometry.Depth(steps);
        return outline[..^1].Where(point => MathF.Abs(point.Length() - bottom) < _tolerance).ToArray();
    }

    private static float Clearance(Vector2[] a, Vector2[] b)
    {
        var least = float.MaxValue;
        for (var i = 0; i < a.Length - 1; i++)
        {
            for (var j = 0; j < b.Length - 1; j++)
            {
                least = MathF.Min(least, MathF.Min(
                    MathF.Min(Distance(a[i], b[j], b[j + 1]), Distance(a[i + 1], b[j], b[j + 1])),
                    MathF.Min(Distance(b[j], a[i], a[i + 1]), Distance(b[j + 1], a[i], a[i + 1]))));
            }
        }

        return least;
    }

    private static float Distance(Vector2 point, Vector2 from, Vector2 to)
    {
        var along = to - from;
        var t = along.LengthSquared() == 0 ? 0 : Math.Clamp((point - from).Dot(along) / along.LengthSquared(), 0, 1);
        return point.DistanceTo(from + (along * t));
    }

    private static float AngleOff(float a, float b) => MathF.Abs(Mathf.AngleDifference(a, b));

    private static void ShouldBeNear(Vector2 actual, Vector2 expected)
    {
        actual.X.ShouldBe(expected.X, _tolerance);
        actual.Y.ShouldBe(expected.Y, _tolerance);
    }
}
