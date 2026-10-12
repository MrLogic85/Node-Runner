using Godot;
using NodeRunner.Domain;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>The Velocity motor's hub motor (#454, <c>docs/WORLD_VISUALS.md</c> → Motor), checked on its geometry.</summary>
public sealed class HubMotorGeometryTests
{
    // Every line of the motor is a signal stroke (VisualTheme's joint and motor widths).
    private const float _halfStroke = UiSize.Stroke.Signal / 2f;

    [Fact]
    public void Motor_CoversAJointMotorsRadius_WithItsRingInside()
    {
        HubMotorGeometry.Radius.ShouldBe((float)JointMotor.Radius);
        (HubMotorGeometry.RingRadius + _halfStroke).ShouldBe(HubMotorGeometry.Radius);
    }

    [Fact]
    public void SolidMagnets_SitOnTheWheelsCueTubes()
    {
        HubMotorGeometry.SolidMagnetAngles().ShouldBe(WheelPart.CueAngles(), tolerance: 1e-6f);
    }

    [Fact]
    public void Magnets_AlternateSolidAndOutlined_AllRoundTheTurn()
    {
        for (var index = 0; index < HubMotorGeometry.MagnetCount; index++)
        {
            HubMotorGeometry.IsSolid(index).ShouldNotBe(HubMotorGeometry.IsSolid((index + 1) % HubMotorGeometry.MagnetCount));
        }
    }

    // Neighbouring outlines stay a stroke apart, so each magnet reads on its own.
    [Fact]
    public void Magnets_DoNotOverlapEachOther()
    {
        for (var index = 0; index < HubMotorGeometry.MagnetCount; index++)
        {
            var next = (index + 1) % HubMotorGeometry.MagnetCount;

            Gap(HubMotorGeometry.MagnetShape(index), HubMotorGeometry.MagnetShape(next)).ShouldBeGreaterThan(2 * _halfStroke);
        }
    }

    [Fact]
    public void MagnetOutline_IsClosed_AndTracesItsShape()
    {
        var outline = HubMotorGeometry.MagnetOutline(1);

        outline[0].DistanceTo(outline[^1]).ShouldBeLessThan(1e-4f);
        outline.Distinct().Count().ShouldBe(HubMotorGeometry.MagnetShape(1).Length);
        outline[0].Length().ShouldBe(HubMotorGeometry.MagnetOuterRadius, tolerance: 1e-4f);
    }

    [Fact]
    public void EachCoilsStem_EndsOnTheMiddleOfItsCap()
    {
        var strokes = HubMotorGeometry.CoilStrokes();

        strokes.Length.ShouldBe(2 * HubMotorGeometry.CoilCount);
        for (var index = 0; index < HubMotorGeometry.CoilCount; index++)
        {
            var stem = strokes[2 * index];
            var cap = strokes[(2 * index) + 1];

            stem[^1].DistanceTo(cap[cap.Length / 2]).ShouldBeLessThan(1e-4f);
        }
    }

    // Neighbouring caps stay a stroke apart, so each coil reads on its own.
    [Fact]
    public void CoilCaps_DoNotOverlapEachOther()
    {
        var strokes = HubMotorGeometry.CoilStrokes();

        for (var index = 0; index < HubMotorGeometry.CoilCount; index++)
        {
            var cap = strokes[(2 * index) + 1];
            var nextCap = strokes[(2 * ((index + 1) % HubMotorGeometry.CoilCount)) + 1];

            Gap(cap, nextCap).ShouldBeGreaterThan(2 * _halfStroke);
        }
    }

    // The coils and magnets turn apart on their own layers, so neither may draw over the other.
    [Fact]
    public void Coils_EndInsideTheMagnets_AndStartAtTheCentreDisc()
    {
        (HubMotorGeometry.CoilOuterRadius + _halfStroke).ShouldBeLessThan(HubMotorGeometry.MagnetInnerRadius - _halfStroke);
        (HubMotorGeometry.MagnetOuterRadius + _halfStroke).ShouldBeLessThan(HubMotorGeometry.RingRadius - _halfStroke);
        HubMotorGeometry.CoilInnerRadius.ShouldBe(HubMotorGeometry.CentreRadius + _halfStroke);
    }

    [Fact]
    public void Halo_LeavesAGapOutsideTheMotorsEdge()
    {
        ((float)SelectionMarks.JointHalo(HubMotorGeometry.Radius) - _halfStroke).ShouldBeGreaterThan(HubMotorGeometry.Radius);
    }

    // The closest two points of two drawn shapes; their arcs are fine enough for a stroke's width.
    private static float Gap(Vector2[] first, Vector2[] second) =>
        first.Min(point => second.Min(point.DistanceTo));
}
