using Godot;
using NodeRunner.Ui.Widgets;

namespace NodeRunner.Ui.Tests;

public sealed class ArenaBestMarkerTests
{
    private const float _flagWidth = 60;
    private static readonly Rect2 _view = new(-100, -50, 400, 100);

    [Theory]
    [InlineData(0, false)]
    [InlineData(240, false)]
    [InlineData(241, true)]
    [InlineData(300, true)]
    public void The_flag_flies_left_only_when_it_would_run_past_the_right_edge(float lineX, bool left)
    {
        ArenaBestMarker.FliesLeft(lineX, _flagWidth, _view).ShouldBe(left);
    }

    [Fact]
    public void A_view_too_narrow_for_either_side_keeps_the_flag_on_the_right()
    {
        ArenaBestMarker.FliesLeft(10, _flagWidth, new Rect2(0, 0, 50, 50)).ShouldBeFalse();
    }
}
