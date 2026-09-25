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
    public void ComponentGalleryInventory_ContainsOnlyCurrentLibrarySpecimens()
    {
        ComponentGalleryScreen.CanonicalInventory
            .Select(spec => spec.Component)
            .ShouldBe(
                [
                    UiComponentContracts.CanonicalComponent.Button,
                    UiComponentContracts.CanonicalComponent.IconButton,
                    UiComponentContracts.CanonicalComponent.HoldButton,
                    UiComponentContracts.CanonicalComponent.TextField,
                    UiComponentContracts.CanonicalComponent.NameField,
                    UiComponentContracts.CanonicalComponent.Note,
                    UiComponentContracts.CanonicalComponent.Slider,
                    UiComponentContracts.CanonicalComponent.Range,
                    UiComponentContracts.CanonicalComponent.ProgressBar,
                    UiComponentContracts.CanonicalComponent.ProgressRing,
                    UiComponentContracts.CanonicalComponent.Card,
                    UiComponentContracts.CanonicalComponent.Toggle,
                    UiComponentContracts.CanonicalComponent.Checkbox,
                    UiComponentContracts.CanonicalComponent.Segmented,
                    UiComponentContracts.CanonicalComponent.Picker,
                    UiComponentContracts.CanonicalComponent.PartRow,
                    UiComponentContracts.CanonicalComponent.StageCard,
                    UiComponentContracts.CanonicalComponent.Menu,
                    UiComponentContracts.CanonicalComponent.IconTabs,
                    UiComponentContracts.CanonicalComponent.SelectionHandle,
                    UiComponentContracts.CanonicalComponent.Number,
                ]);
        ComponentGalleryScreen.CanonicalInventory
            .Select(spec => spec.Section)
            .Distinct()
            .Count()
            .ShouldBeLessThan(UiComponentContracts.AllCanonicalComponents.Count);
    }

    [Fact]
    public void ComponentGallerySectionOrder_PlacesNumberDirectlyAfterSelectionHandles()
    {
        ComponentGalleryScreen.RenderedSectionOrder.ShouldContain(ComponentGalleryScreen.GallerySection.Number);
        var sectionOrder = ComponentGalleryScreen.RenderedSectionOrder.ToList();
        var selectionIndex = sectionOrder.IndexOf(ComponentGalleryScreen.GallerySection.SelectionHandles);
        var numberIndex = sectionOrder.IndexOf(ComponentGalleryScreen.GallerySection.Number);

        numberIndex.ShouldBe(selectionIndex + 1);
    }

    [Fact]
    public void ComponentGallerySectionOrder_PlacesTextInputDirectlyAfterButtons()
    {
        var sectionOrder = ComponentGalleryScreen.RenderedSectionOrder.ToList();
        var actionsIndex = sectionOrder.IndexOf(ComponentGalleryScreen.GallerySection.Actions);
        var textInputIndex = sectionOrder.IndexOf(ComponentGalleryScreen.GallerySection.TextInput);

        actionsIndex.ShouldBeGreaterThanOrEqualTo(0);
        textInputIndex.ShouldBeGreaterThanOrEqualTo(0);
        textInputIndex.ShouldBe(actionsIndex + 1);
    }

    [Fact]
    public void ComponentGallerySectionOrder_PlacesPanelRowsAndStageCardAfterPartRows()
    {
        var sectionOrder = ComponentGalleryScreen.RenderedSectionOrder.ToList();
        var partRowsIndex = sectionOrder.IndexOf(ComponentGalleryScreen.GallerySection.PartRows);
        var panelIndex = sectionOrder.IndexOf(ComponentGalleryScreen.GallerySection.PanelRows);
        var stageCardIndex = sectionOrder.IndexOf(ComponentGalleryScreen.GallerySection.StageCard);

        partRowsIndex.ShouldBeGreaterThanOrEqualTo(0);
        panelIndex.ShouldBeGreaterThanOrEqualTo(0);
        stageCardIndex.ShouldBeGreaterThanOrEqualTo(0);
        panelIndex.ShouldBe(partRowsIndex + 1);
        stageCardIndex.ShouldBe(panelIndex + 1);
    }

}
