using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiNotificationIconTests
{
    [Fact]
    public void Icon_PreservesLibraryWhenNamesOverlap()
    {
        new UiNotificationIcon(UiIconId.Beam).ResourcePath.ShouldBe(UiIcons.UiRoot + "beam.svg");
        new UiNotificationIcon(UiPartIconId.Beam).ResourcePath.ShouldBe(UiIcons.PartRoot + "beam.svg");
        new UiNotificationIcon(UiIconId.Trophy).ResourcePath.ShouldBe(UiIcons.UiRoot + "trophy.svg");
        new UiNotificationIcon(UiPartIconId.Spring).ResourcePath.ShouldBe(UiIcons.PartRoot + "spring.svg");
    }

    [Theory]
    [InlineData(UiIconId.None)]
    [InlineData((UiIconId)999)]
    public void UiIcon_RejectsMissingOrInvalidGlyph(UiIconId icon)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new UiNotificationIcon(icon));
    }

    [Fact]
    public void PartIcon_RejectsInvalidGlyph()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new UiNotificationIcon((UiPartIconId)999));
    }
}
