using Godot;
using NodeRunner.Domain;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>The Velocity motor's hub motor (<c>docs/WORLD_VISUALS.md</c> → Motor), checked on its geometry.</summary>
public sealed class HubMotorGeometryTests
{
    // Every line of the motor is a signal stroke (VisualTheme's joint and motor widths).
    private const float _halfStroke = UiSize.Stroke.Signal / 2f;

    [Fact]
    public void Motor_CoversAJointMotorsRadius_WithItsRingInside()
    {
        HubMotorGeometry.Radius.ShouldBe((float)JointMotor.Radius);
        (HubMotorGeometry.RingRadius + _halfStroke).ShouldBe(HubMotorGeometry.Radius);
        // The selection halo leaves a gap outside the motor's edge.
        ((float)SelectionMarks.JointHalo(HubMotorGeometry.Radius) - _halfStroke).ShouldBeGreaterThan(HubMotorGeometry.Radius);
    }

    [Fact]
    public void SolidMagnets_SitOnTheWheelsCueTubes()
    {
        HubMotorGeometry.SolidMagnetAngles().ShouldBe(WheelPart.CueAngles(), tolerance: 1e-6f);
    }

    // Magnets alternate solid and outlined all round the turn, and neighbouring outlines stay a stroke apart,
    // so each magnet reads on its own.
    [Fact]
    public void Magnets_Alternate_AndDoNotOverlapEachOther()
    {
        for (var index = 0; index < HubMotorGeometry.MagnetCount; index++)
        {
            var next = (index + 1) % HubMotorGeometry.MagnetCount;
            HubMotorGeometry.IsSolid(index).ShouldNotBe(HubMotorGeometry.IsSolid(next));
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

    // Each coil's stem ends on the middle of its cap, and neighbouring caps stay a stroke apart.
    [Fact]
    public void EachCoilsStem_EndsOnTheMiddleOfItsCap_AndCapsDoNotOverlap()
    {
        var strokes = HubMotorGeometry.CoilStrokes();
        strokes.Length.ShouldBe(2 * HubMotorGeometry.CoilCount);
        for (var index = 0; index < HubMotorGeometry.CoilCount; index++)
        {
            var stem = strokes[2 * index];
            var cap = strokes[(2 * index) + 1];
            var nextCap = strokes[(2 * ((index + 1) % HubMotorGeometry.CoilCount)) + 1];
            stem[^1].DistanceTo(cap[cap.Length / 2]).ShouldBeLessThan(1e-4f);
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

    // The closest two points of two drawn shapes; their arcs are fine enough for a stroke's width.
    private static float Gap(Vector2[] first, Vector2[] second) =>
        first.Min(point => second.Min(point.DistanceTo));
}
