using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiExpandSectionTests
{
    [Theory]
    [InlineData(false, UiIconId.ChevronRight)]
    [InlineData(true, UiIconId.ChevronDown)]
    public void Chevron_points_right_when_closed_and_down_when_open(bool open, UiIconId chevron)
    {
        UiExpandSection.ChevronFor(open).ShouldBe(chevron);
    }

    [Fact]
    public void Header_tap_target_is_a_small_control_high()
    {
        UiExpandSection.HeaderHeight.ShouldBe(UiSize.Control.Small);
    }

    [Fact]
    public void Gallery_shows_a_closed_and_an_open_section()
    {
        var sections = SceneNodes.InScene("screens/ComponentGalleryScreen.tscn")
            .Where(node => node.Script?.EndsWith("/UiExpandSection.cs", StringComparison.Ordinal) == true)
            .Select(node => node.Node.Body.Contains("\nOpen = true", StringComparison.Ordinal))
            .ToList();

        sections.ShouldBe([false, true], ignoreOrder: true);
    }
}
