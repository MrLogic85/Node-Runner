using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiScaleTests
{
    private static readonly Vector2I _s25Window = new(2340, 1080);

    // The S25's canvas at 100% is 780 x 360 units; the UI size divides it.
    private static Vector2 S25Canvas(int percent) => new Vector2(780, 360) / UiScale.FactorFor(percent);

    [Fact]
    public void The_project_registers_the_ui_scale_autoload_and_stretches_canvas_items()
    {
        var settings = File.ReadAllText(Path.Combine(ThemeFile.ProjectRoot, "project.godot"));

        settings.ShouldContain("window/stretch/mode=\"canvas_items\"");
        settings.ShouldContain($"{UiScale.AutoloadName}=\"*res://src/ui/lib/UiScale.cs\"");
    }

    [Theory]
    [InlineData(50, 0.5f)]
    [InlineData(100, 1f)]
    [InlineData(200, 2f)]
    [InlineData(400, 4f)]
    public void FactorFor_TheFourSteps(int percent, float factor)
    {
        UiScale.FactorFor(percent).ShouldBe(factor);
    }

    [Theory]
    [InlineData(50, 24)]
    [InlineData(100, 48)]
    [InlineData(200, 96)]
    [InlineData(400, 192)]
    public void TouchTarget_ScalesWithNoFloor(int percent, float onReferenceCanvas)
    {
        UiScale.OnReferenceCanvas(UiSize.Control.Touch, percent).ShouldBe(onReferenceCanvas);
    }

    [Theory]
    [InlineData(102, 100)]
    [InlineData(102.5, 105)]
    [InlineData(103, 105)]
    [InlineData(147.4, 145)]
    [InlineData(10, 50)]
    [InlineData(1000, 400)]
    [InlineData(double.NaN, 100)]
    public void Snap_RoundsToTheNearestStepWithinTheRange(double percent, int snapped)
    {
        UiScale.Snap(percent).ShouldBe(snapped);
    }

    [Theory]
    [InlineData(50, 1.5f)]
    [InlineData(100, 3f)]
    [InlineData(200, 6f)]
    [InlineData(400, 12f)]
    public void PixelsPerUnit_IsTheStretchTimesTheUiSize(int percent, float pixelsPerUnit)
    {
        UiScale.PixelsPerUnit(_s25Window, S25Canvas(percent)).ShouldBe(pixelsPerUnit, 1e-4f);
    }

    [Fact]
    public void PixelsPerUnit_WithNoWindowYet_IsOne()
    {
        UiScale.PixelsPerUnit(Vector2I.Zero, Vector2.Zero).ShouldBe(1);
    }

    [Theory]
    [InlineData(10f, 1.25f, 13)]
    [InlineData(10f, 1.24f, 12)]
    [InlineData(0.1f, 1f, 1)]
    public void RasterPixels_RoundsToTheNearestWholePixelAndIsAtLeastOne(float units, float pixelsPerUnit, int pixels)
    {
        UiScale.RasterPixels(units, pixelsPerUnit).ShouldBe(pixels);
    }

    [Theory]
    [InlineData(50, 64)]
    [InlineData(100, 32)]
    [InlineData(200, 16)]
    [InlineData(400, 8)]
    public void SafeAreaInset_KeepsTheCutoutsPixelsAtEveryUiSize(int percent, float insetUnits)
    {
        var window = new Rect2I(Vector2I.Zero, _s25Window);

        var insets = UiSafeArea.Insets(window, new Rect2I(96, 0, 2244, 1080), S25Canvas(percent));

        insets.Left.ShouldBe(insetUnits, 1e-3f);
        (insets.Left * UiScale.PixelsPerUnit(_s25Window, S25Canvas(percent))).ShouldBe(96, 1e-2f);
    }
}
