using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>UI size (docs/UI_DIRECTION.md → "UI size") and the safe area, on the S25 and a Pixel Tablet.</summary>
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
    public void S25_auto_is_280_and_its_max_canvas_fits_the_layouts_while_a_tablet_gets_more_canvas()
    {
        var s25Auto = UiScale.AutoPercentFor(UiScale.DensityScaleFor(mobile: true, dpi: 450, screenScale: 1.8f));
        var tabletAuto = UiScale.AutoPercentFor(_pixelTabletDensity);

        s25Auto.ShouldBe(280);
        (UiSize.Control.Touch * s25Auto / 100f / _s25Density).ShouldBe(48, 1);
        UiScale.MaxPercentFor(_s25Safe).ShouldBe(300);
        Canvas(_s25Safe, 300).X.ShouldBeGreaterThanOrEqualTo(UiLayout.CanvasWidth);
        Canvas(_s25Safe, 300).Y.ShouldBeGreaterThanOrEqualTo(UiLayout.CanvasHeight);
        tabletAuto.ShouldBe(200);
        UiScale.MaxPercentFor(_pixelTabletWindow).ShouldBe(400);
        Canvas(_pixelTabletWindow, tabletAuto).X.ShouldBeGreaterThan(Canvas(_s25Window, s25Auto).X);
        Canvas(_pixelTabletWindow, tabletAuto).Y.ShouldBeGreaterThan(Canvas(_s25Window, s25Auto).Y);
    }

    [Theory]
    [InlineData(true, 450, 1.8f, 450f / 160)]
    [InlineData(false, 220, 2f, 2f)]
    [InlineData(true, 0, 1.8f, 1f)]
    [InlineData(false, 220, float.NaN, 1f)]
    public void Density_comes_from_the_dpi_on_a_phone_and_the_scale_on_a_desktop(bool mobile, int dpi, float screenScale, float density) =>
        UiScale.DensityScaleFor(mobile, dpi, screenScale).ShouldBe(density, 1e-5f);

    [Theory]
    [InlineData(102.5, 105)]
    [InlineData(10, 50)]
    [InlineData(double.NaN, 100)]
    public void Snap_rounds_to_the_nearest_step_above_the_minimum(double percent, int snapped) =>
        UiScale.Snap(percent).ShouldBe(snapped);

    [Theory]
    [InlineData(1000, 1000, 155)]
    [InlineData(200, 100, 50)]
    public void Max_floors_to_the_step_and_never_goes_below_the_minimum(int width, int height, int max) =>
        UiScale.MaxPercentFor(new Vector2I(width, height)).ShouldBe(max);

    [Theory]
    [InlineData(50, 192)]
    [InlineData(300, 32)]
    public void Safe_area_inset_keeps_the_cutouts_pixels_at_every_ui_size(int percent, float insetUnits)
    {
        var canvas = Canvas(_s25Window, percent);

        var insets = UiSafeArea.Insets(new Rect2I(Vector2I.Zero, _s25Window), new Rect2I(96, 0, 2244, 1080), canvas);

        insets.Left.ShouldBe(insetUnits, 1e-2f);
        (insets.Left * _s25Window.X / canvas.X).ShouldBe(96, 1e-2f);
    }

    [Theory]
    [InlineData(100, 50, 1560, 720, 130, 50, 1530, 700, 15, 0, 0, 10)]
    [InlineData(500, 300, 1560, 720, 0, 0, 3024, 1742, 0, 0, 0, 0)]
    [InlineData(0, 0, 2340, 1080, 0, 0, 2340, 0, 0, 0, 0, 0)]
    public void Safe_area_insets_are_the_cutouts_inside_the_window_in_canvas_units(
        int wx, int wy, int ww, int wh, int sx, int sy, int sw, int sh, float left, float top, float right, float bottom) =>
        UiSafeArea.Insets(new Rect2I(wx, wy, ww, wh), new Rect2I(sx, sy, sw, sh), new Vector2(780, 360))
            .ShouldBe(new UiInsets(left, top, right, bottom));
}
