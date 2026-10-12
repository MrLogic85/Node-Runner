using Godot;
using NodeRunner.Domain;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>The Velocity motor's hub motor (#454, <c>docs/WORLD_VISUALS.md</c> → Velocity motor), checked on its geometry.</summary>
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
    public void Magnets_AreSix_ASixthOfATurnApart_AlternatingSolidAndOutlined()
    {
        var angles = Enumerable.Range(0, HubMotorGeometry.MagnetCount).Select(HubMotorGeometry.MagnetAngle);
        var solid = Enumerable.Range(0, HubMotorGeometry.MagnetCount).Select(HubMotorGeometry.IsSolid);

        angles.ShouldBe([0f, Mathf.Tau / 6, 2 * Mathf.Tau / 6, 3 * Mathf.Tau / 6, 4 * Mathf.Tau / 6, 5 * Mathf.Tau / 6], tolerance: 1e-6f);
        solid.ShouldBe([true, false, true, false, true, false]);
    }

    [Fact]
    public void Magnet_IsARingSegment_OfFortyDegrees()
    {
        for (var index = 0; index < HubMotorGeometry.MagnetCount; index++)
        {
            var centre = HubMotorGeometry.MagnetAngle(index);
            var shape = HubMotorGeometry.MagnetShape(index);

            shape.ShouldAllBe(point => point.Length() > HubMotorGeometry.MagnetInnerRadius - 1e-3f && point.Length() < HubMotorGeometry.MagnetOuterRadius + 1e-3f);
            shape.Max(point => Math.Abs(Mathf.AngleDifference(centre, point.Angle()))).ShouldBe(Mathf.DegToRad(20), tolerance: 1e-5f);
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
    public void Coils_AreSixStemsWithCaps_ASixthOfATurnApart()
    {
        var strokes = HubMotorGeometry.CoilStrokes();

        strokes.Length.ShouldBe(2 * HubMotorGeometry.CoilCount);
        for (var index = 0; index < HubMotorGeometry.CoilCount; index++)
        {
            var along = index * Mathf.Tau / 6;
            var stem = strokes[2 * index];
            var cap = strokes[(2 * index) + 1];
            stem[0].Length().ShouldBe(7, tolerance: 1e-4f);
            stem[^1].Length().ShouldBe(15.5f, tolerance: 1e-4f);
            Mathf.AngleDifference(along, stem[^1].Angle()).ShouldBe(0, tolerance: 1e-5f);
            cap.ShouldAllBe(point => Math.Abs(point.Length() - 15.5f) < 1e-3f);
            Mathf.RadToDeg(Mathf.AngleDifference(cap[0].Angle(), cap[^1].Angle())).ShouldBe(38, tolerance: 1e-3);
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
    public void Halo_SitsAGapOutsideTheMotor()
    {
        SelectionMarks.JointHalo(HubMotorGeometry.Radius).ShouldBe(30);
    }
}
