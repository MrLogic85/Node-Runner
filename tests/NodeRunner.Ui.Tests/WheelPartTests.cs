using Godot;
using NodeRunner.Domain;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>The Wheel's look (#129, <c>docs/WORLD_VISUALS.md</c> → Wheel), checked on its geometry.</summary>
public sealed class WheelPartTests
{
    [Fact]
    public void Cue_IsThreeTubes_AThirdOfATurnApart_FromRotationZero()
    {
        WheelPart.CueAngles().ShouldBe([0f, Mathf.Tau / 3, 2 * Mathf.Tau / 3], tolerance: 1e-6f);
    }

    [Theory]
    [InlineData(WheelDef.MinRadius)]
    [InlineData(70)]
    [InlineData(WheelDef.MaxRadius)]
    public void Cue_KeepsItsCountAndSpan_AtEveryRadius_InTheTyreBand(double radius)
    {
        var tubes = WheelPart.CueTubes((float)radius);

        tubes.Length.ShouldBe(WheelPart.CueCount);
        for (var index = 0; index < tubes.Length; index++)
        {
            var tube = tubes[index];
            tube.ShouldAllBe(point => Math.Abs(point.Length() - ((float)radius - 3.75f)) < 1e-3f);
            var from = tube[0].Angle();
            var to = tube[^1].Angle();
            Mathf.RadToDeg(Mathf.AngleDifference(from, to)).ShouldBe(26, tolerance: 1e-3);
            Mathf.AngleDifference(WheelPart.CueAngles()[index], (from + to) / 2).ShouldBe(0, tolerance: 1e-5f);
        }
    }

    [Fact]
    public void Cue_RunsBetweenTheInnerLineAndTheTyreRing()
    {
        const float radius = (float)WheelDef.MinRadius;
        // The tyre and cue are signal strokes, the inner line a hair (VisualTheme's joint and motor widths).
        const float signal = UiSize.Stroke.Signal;
        const float hair = UiSize.Stroke.Hair;
        var cue = WheelPart.CueRadius(radius);

        (cue - (signal / 2)).ShouldBeGreaterThan(radius - WheelPart.TyreWidth + (hair / 2));
        (cue + (signal / 2)).ShouldBeLessThan(radius - signal);
    }

    [Theory]
    [InlineData(40, 2, 39)]
    [InlineData(100, 2, 99)]
    public void Tyre_HasItsOuterEdgeAtTheRadius(float radius, float width, float centre)
    {
        WheelPart.TyreRingRadius(radius, width).ShouldBe(centre);
    }

    [Fact]
    public void Hub_IsThePlainJointsSize_WithAnAxleDot()
    {
        WheelPart.HubRadius.ShouldBe((float)NodeDef.PlainJointRadius);
        WheelPart.AxleRadius.ShouldBe(2.6f);
        WheelPart.HubRadius.ShouldBeLessThan((float)WheelDef.MinRadius - WheelPart.TyreWidth);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(100)]
    public void Selection_IsTheHaloRing_TheGapOutsideTheEdge(float radius)
    {
        (WheelPart.HaloRadius(radius) - radius).ShouldBe((float)SelectionMarks.Gap);
    }
}
