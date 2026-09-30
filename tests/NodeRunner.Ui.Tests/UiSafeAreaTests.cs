using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiSafeAreaTests
{
    private static readonly Rect2I _s25Window = new(0, 0, 2340, 1080);
    private static readonly Vector2 _s25Canvas = new(780, 360);

    [Fact]
    public void The_project_fills_the_screen_and_registers_the_safe_area_autoload()
    {
        var settings = File.ReadAllText(Path.Combine(ThemeFile.ProjectRoot, "project.godot"));

        settings.ShouldContain("window/stretch/aspect=\"expand\"");
        settings.ShouldContain($"{UiSafeArea.AutoloadName}=\"*res://src/ui/lib/UiSafeArea.cs\"");
    }

    [Fact]
    public void A_full_safe_area_gives_no_insets() =>
        UiSafeArea.Insets(_s25Window, _s25Window, _s25Canvas).ShouldBe(UiInsets.None);

    [Fact]
    public void A_cutout_on_the_left_insets_the_left_edge_in_canvas_units() =>
        UiSafeArea.Insets(_s25Window, new Rect2I(96, 0, 2244, 1080), _s25Canvas)
            .ShouldBe(new UiInsets(32, 0, 0, 0));

    [Fact]
    public void A_cutout_on_the_right_insets_the_right_edge_in_canvas_units() =>
        UiSafeArea.Insets(_s25Window, new Rect2I(0, 0, 2244, 1080), _s25Canvas)
            .ShouldBe(new UiInsets(0, 0, 32, 0));

    [Fact]
    public void The_window_position_on_the_screen_is_taken_into_account() =>
        UiSafeArea.Insets(new Rect2I(100, 50, 1560, 720), new Rect2I(130, 50, 1530, 700), new Vector2(780, 360))
            .ShouldBe(new UiInsets(15, 0, 0, 10));

    [Fact]
    public void A_safe_area_larger_than_the_window_never_gives_negative_insets() =>
        UiSafeArea.Insets(new Rect2I(500, 300, 1560, 720), new Rect2I(0, 0, 3024, 1742), new Vector2(780, 360))
            .ShouldBe(UiInsets.None);

    [Theory]
    [InlineData(0, 1080, 2340, 1080)]
    [InlineData(2340, 0, 2340, 1080)]
    [InlineData(2340, 1080, 0, 1080)]
    [InlineData(2340, 1080, 2340, 0)]
    public void An_empty_window_or_safe_area_gives_no_insets(int windowWidth, int windowHeight, int safeWidth, int safeHeight) =>
        UiSafeArea.Insets(new Rect2I(0, 0, windowWidth, windowHeight), new Rect2I(0, 0, safeWidth, safeHeight), _s25Canvas)
            .ShouldBe(UiInsets.None);
}
