using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiNotificationIconTests
{
    [Fact]
    public void Icon_PreservesLibraryWhenNamesOverlap()
    {
        new UiNotificationIcon(UiIconId.Beam).ResourcePath.ShouldBe(UiIcons.UiRoot + "beam.svg");
        new UiNotificationIcon(UiIconId.PartBeam).ResourcePath.ShouldBe(UiIcons.PartRoot + "beam.svg");
        new UiNotificationIcon(UiIconId.Trophy).ResourcePath.ShouldBe(UiIcons.UiRoot + "trophy.svg");
        new UiNotificationIcon(UiIconId.PartSpring).ResourcePath.ShouldBe(UiIcons.PartRoot + "spring.svg");
    }

    [Theory]
    [InlineData(UiIconId.None)]
    [InlineData((UiIconId)999)]
    public void UiIcon_RejectsMissingOrInvalidGlyph(UiIconId icon)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new UiNotificationIcon(icon));
    }
}
