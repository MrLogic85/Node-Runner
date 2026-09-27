using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Screens;

namespace NodeRunner.Ui.Tests;

public sealed class UiGalleryInventoryTests
{
    [Fact]
    public void ColorsAndStylesInventory_ContainsCanonicalFoundationCounts()
    {
        ColorsAndStylesScreen.ColorTokenInventory.Count.ShouldBe(14);
        ColorsAndStylesScreen.ColorTokenInventory.Distinct().Count()
            .ShouldBe(ColorsAndStylesScreen.ColorTokenInventory.Count);
        ColorsAndStylesScreen.TextStyleInventory.Count.ShouldBe(17);
        ColorsAndStylesScreen.TextStyleInventory.Distinct().Count()
            .ShouldBe(ColorsAndStylesScreen.TextStyleInventory.Count);
        ColorsAndStylesScreen.IconSizeInventory.ShouldBe(
            ["icon-sm", "icon", "icon-lg", "icon-xl"]);
        ColorsAndStylesScreen.RadiusInventory.ShouldBe(
            ["radius-sm", "radius-md", "radius-lg", "radius-pill"]);
    }

    [Fact]
    public void ColorsAndStylesInventory_UsesBothLiveThemes()
    {
        ThemeFile.For(UiTokenType.Neon).Color(UiTokens.Color.Background).ShouldNotBe(ThemeFile.For(UiTokenType.Paper).Color(UiTokens.Color.Background));
        ThemeFile.For(UiTokenType.Neon).Color(UiTokens.Color.Accent).ShouldNotBe(ThemeFile.For(UiTokenType.Paper).Color(UiTokens.Color.Accent));
        ColorsAndStylesScreen.ColorTokenInventory
            .ShouldNotContain("accent-glow");
        ColorsAndStylesScreen.ColorTokenInventory
            .ShouldNotContain("accent-soft");
        ColorsAndStylesScreen.ColorTokenInventory
            .ShouldContain("scrim");
    }
}
