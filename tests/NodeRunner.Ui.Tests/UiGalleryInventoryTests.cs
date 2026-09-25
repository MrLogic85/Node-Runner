using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Screens;

namespace NodeRunner.Ui.Tests;

public sealed class UiGalleryInventoryTests
{
    [Fact]
    public void ColorsAndStylesInventory_ContainsCanonicalFoundationCounts()
    {
        ColorsAndStylesScreen.ColorTokenInventory.Count.ShouldBe(16);
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
        UiTokens.Neon.Background.ShouldNotBe(UiTokens.Paper.Background);
        UiTokens.Neon.Accent.ShouldNotBe(UiTokens.Paper.Accent);
        ColorsAndStylesScreen.ColorTokenInventory
            .ShouldContain("accent-glow");
        ColorsAndStylesScreen.ColorTokenInventory
            .ShouldContain("scrim");
    }
}
