using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Widgets;

namespace NodeRunner.Ui.Tests;

/// <summary>The glyph Build's side panel and Training's part callout show for each kind of part.</summary>
public sealed class PartIconsTests
{
    [Fact]
    public void EveryPartKind_HasItsOwnGlyph()
    {
        var icons = Enum.GetValues<PartSettingsKind>().Select(PartIcons.For).ToList();

        icons.ShouldNotContain(UiIconId.None);
        icons.ShouldBeUnique();
    }
}
