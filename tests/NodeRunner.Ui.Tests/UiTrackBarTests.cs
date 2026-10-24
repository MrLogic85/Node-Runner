using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>Where a track strip's fill ends and its marker stands: a fraction of the track, clamped to it.</summary>
public sealed class UiTrackBarTests
{
    private static readonly Rect2 _track = new(10, 2, 100, 6);

    [Theory]
    [InlineData(0, 10)]
    [InlineData(0.25, 35)]
    [InlineData(1, 110)]
    [InlineData(-0.5, 10)]
    [InlineData(2, 110)]
    public void At_IsTheFractionAlongTheTrack_ClampedToIt(double fraction, float x) =>
        UiTrackBar.At(_track, fraction).ShouldBe(x);

    [Fact]
    public void At_WithNoValue_IsNothing() =>
        UiTrackBar.At(_track, double.NaN).ShouldBeNull();
}
