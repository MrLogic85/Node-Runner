using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiScaleTests
{
    private static readonly Vector2I _s25Window = new(2340, 1080);
    private static readonly Vector2I _s25Safe = new(2244, 1080);
    private const float _s25Density = 450f / 160;
    private static readonly Vector2I _pixelTabletWindow = new(2560, 1600);
    private const float _pixelTabletDensity = 320f / 160;

    private static Vector2 Canvas(Vector2I window, int percent) =>
        (Vector2)window / (UiScale.BaseStretch(window) * UiScale.RootFactorFor(percent, window));

    [Fact]
    public void The_project_registers_the_ui_scale_autoload_and_stretches_canvas_items()
    {
        var settings = File.ReadAllText(Path.Combine(ThemeFile.ProjectRoot, "project.godot"));

        // BaseStretch assumes the reference canvas as base size, a fractional stretch and no extra scale.
        settings.ShouldContain($"window/size/viewport_width={UiLayout.CanvasWidth}\n");
        settings.ShouldContain($"window/size/viewport_height={UiLayout.CanvasHeight}\n");
        settings.ShouldContain("window/stretch/mode=\"canvas_items\"");
        settings.ShouldContain("window/stretch/aspect=\"expand\"");
        settings.ShouldNotContain("window/stretch/scale=");
        settings.ShouldNotContain("window/stretch/scale_mode=");
        settings.ShouldContain($"{UiScale.AutoloadName}=\"*res://src/ui/lib/UiScale.cs\"");
    }

    [Fact]
    public void S25_AutoIs280_MaxIs300_AndTheMaxCanvasFitsTheLayouts()
    {
        UiScale.AutoPercentFor(_s25Density).ShouldBe(280);
        UiScale.MaxPercentFor(_s25Safe).ShouldBe(300);

        var safeCanvas = Canvas(_s25Safe, 300);
        safeCanvas.X.ShouldBeGreaterThanOrEqualTo(UiLayout.CanvasWidth);
        safeCanvas.Y.ShouldBeGreaterThanOrEqualTo(UiLayout.CanvasHeight);
    }

    [Fact]
    public void PixelTablet_GetsMoreCanvasThanTheS25_NotABiggerUi()
    {
        var tabletAuto = UiScale.AutoPercentFor(_pixelTabletDensity);
        var s25Auto = UiScale.AutoPercentFor(_s25Density);

        tabletAuto.ShouldBe(200);
        UiScale.MaxPercentFor(_pixelTabletWindow).ShouldBe(400);
        Canvas(_pixelTabletWindow, tabletAuto).X.ShouldBeGreaterThan(Canvas(_s25Window, s25Auto).X);
        Canvas(_pixelTabletWindow, tabletAuto).Y.ShouldBeGreaterThan(Canvas(_s25Window, s25Auto).Y);
    }

    [Fact]
    public void AtAuto_A48UnitControlIsAbout48Dp()
    {
        var pixels = UiSize.Control.Touch * UiScale.AutoPercentFor(_s25Density) / 100f;

        (pixels / _s25Density).ShouldBe(48, 1);
    }

    [Theory]
    [InlineData(true, 450, 1.8f, 450f / 160)]
    [InlineData(true, 320, 2f, 2f)]
    [InlineData(false, 220, 2f, 2f)]
    [InlineData(false, 96, 1f, 1f)]
    [InlineData(true, 0, 1.8f, 1f)]
    [InlineData(false, 220, float.NaN, 1f)]
    [InlineData(false, 220, 0f, 1f)]
    public void DensityScaleFor_UsesTheDpiOnAPhoneAndTheScaleOnADesktop(bool mobile, int dpi, float screenScale, float density)
    {
        UiScale.DensityScaleFor(mobile, dpi, screenScale).ShouldBe(density, 1e-5f);
    }

    [Fact]
    public void S25_AutoComesFromItsDpi_NotItsCappedScale()
    {
        UiScale.AutoPercentFor(UiScale.DensityScaleFor(mobile: true, dpi: 450, screenScale: 1.8f)).ShouldBe(280);
    }

    [Theory]
    [InlineData(1f, 100)]
    [InlineData(2f, 200)]
    [InlineData(0.1f, 50)]
    [InlineData(float.NaN, 100)]
    public void AutoPercentFor_SnapsTheDensity(float densityScale, int auto)
    {
        UiScale.AutoPercentFor(densityScale).ShouldBe(auto);
    }

    [Theory]
    [InlineData(640, 360, 100)]
    [InlineData(2340, 1080, 300)]
    [InlineData(1000, 1000, 155)]
    [InlineData(200, 100, 50)]
    public void MaxPercentFor_FloorsToTheStepAndNeverGoesBelowTheMinimum(int width, int height, int max)
    {
        UiScale.MaxPercentFor(new Vector2I(width, height)).ShouldBe(max);
    }

    [Theory]
    [InlineData(102, 100)]
    [InlineData(102.5, 105)]
    [InlineData(103, 105)]
    [InlineData(147.4, 145)]
    [InlineData(10, 50)]
    [InlineData(1000, 1000)]
    [InlineData(double.NaN, 100)]
    public void Snap_RoundsToTheNearestStepAboveTheMinimum(double percent, int snapped)
    {
        UiScale.Snap(percent).ShouldBe(snapped);
    }

    [Theory]
    [InlineData(50, 192)]
    [InlineData(100, 96)]
    [InlineData(300, 32)]
    public void SafeAreaInset_KeepsTheCutoutsPixelsAtEveryUiSize(int percent, float insetUnits)
    {
        var window = new Rect2I(Vector2I.Zero, _s25Window);
        var canvas = Canvas(_s25Window, percent);

        var insets = UiSafeArea.Insets(window, new Rect2I(96, 0, 2244, 1080), canvas);

        insets.Left.ShouldBe(insetUnits, 1e-2f);
        (insets.Left * _s25Window.X / canvas.X).ShouldBe(96, 1e-2f);
    }
}
