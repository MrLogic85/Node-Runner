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

    [Fact]
    public void ComponentGalleryInventory_RemainsExactlyTheCanonicalComponentSet()
    {
        ComponentGalleryScreen.CanonicalInventory
            .Select(spec => spec.Component)
            .ShouldBe(UiComponentContracts.AllCanonicalComponents);
        ComponentGalleryScreen.CanonicalInventory
            .Select(spec => spec.EntryName)
            .Distinct()
            .Count()
            .ShouldBe(UiComponentContracts.AllCanonicalComponents.Count);
    }

    [Theory]
    [InlineData(120, 8.4f, 112)]
    [InlineData(120, -8.4f, 128)]
    [InlineData(0, 0.4f, 0)]
    public void GalleryScroll_AppliesTouchDragInNaturalDirection(
        int currentScroll,
        float relativeY,
        int expected)
    {
        UiGalleryScroll.ApplyVerticalDrag(currentScroll, relativeY).ShouldBe(expected);
    }

    [Fact]
    public void ComponentGallery_FocusPreviewDoesNotStealRuntimeFocus()
    {
        var gallery = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "project",
            "src",
            "ui",
            "screens",
            "ComponentGalleryScreen.cs"));

        gallery.ShouldContain("ShowFocusRing = true");
        gallery.ShouldNotContain("GrabFocus");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "NodeRunner.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
