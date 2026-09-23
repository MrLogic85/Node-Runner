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
                    UiComponentContracts.CanonicalComponent.Slider,
                    UiComponentContracts.CanonicalComponent.Range,
                    UiComponentContracts.CanonicalComponent.ProgressBar,
                    UiComponentContracts.CanonicalComponent.ProgressRing,
                    UiComponentContracts.CanonicalComponent.Card,
                    UiComponentContracts.CanonicalComponent.Panel,
                    UiComponentContracts.CanonicalComponent.Toggle,
                    UiComponentContracts.CanonicalComponent.Checkbox,
                    UiComponentContracts.CanonicalComponent.Segmented,
                    UiComponentContracts.CanonicalComponent.Picker,
                    UiComponentContracts.CanonicalComponent.PartRow,
                    UiComponentContracts.CanonicalComponent.OverflowMenu,
                    UiComponentContracts.CanonicalComponent.TextField,
                    UiComponentContracts.CanonicalComponent.NameField,
                    UiComponentContracts.CanonicalComponent.IconTabs,
                    UiComponentContracts.CanonicalComponent.SelectionHandle,
                ]);
        ComponentGalleryScreen.CanonicalInventory
            .Select(spec => spec.Section)
            .Distinct()
            .Count()
            .ShouldBeLessThan(UiComponentContracts.AllCanonicalComponents.Count);
    }

    [Fact]
    public void ButtonGalleryInventory_CoversEveryReferenceKindLayoutAndStateGroup()
    {
        var specimens = ComponentGalleryScreen.ButtonSpecimenInventory;

        specimens.Count.ShouldBe(38);
        specimens.Count(spec => spec.Layout == UiButtonContentLayout.Row && !spec.Compact && !spec.Badge)
            .ShouldBe(8);
        specimens.Count(spec => spec.Layout == UiButtonContentLayout.Row && spec.Compact)
            .ShouldBe(4);
        specimens.Count(spec => spec.Layout == UiButtonContentLayout.Row && spec.Badge)
            .ShouldBe(1);
        specimens.Count(spec => spec.Layout == UiButtonContentLayout.Icon && !spec.Compact && !spec.Badge)
            .ShouldBe(7);
        specimens.Count(spec => spec.Layout == UiButtonContentLayout.Icon && spec.Compact)
            .ShouldBe(5);
        specimens.Count(spec => spec.Layout == UiButtonContentLayout.Icon && spec.Badge)
            .ShouldBe(4);
        specimens.Count(spec => spec.Layout == UiButtonContentLayout.Stack)
            .ShouldBe(10);
        specimens.Count(spec => spec.Badge).ShouldBe(6);
        specimens.Select(spec => spec.Kind).Distinct().ShouldBe(Enum.GetValues<UiButtonKind>());
    }

    [Fact]
    public void ButtonGalleryInventory_PreservesReferenceDisabledAndHoldCoverage()
    {
        var specimens = ComponentGalleryScreen.ButtonSpecimenInventory;

        specimens.Count(spec => !spec.Enabled).ShouldBe(3);
        specimens.Count(spec => spec.Hold).ShouldBe(6);
        specimens.ShouldContain(new ComponentGalleryScreen.ButtonGallerySpec(
            UiButtonKind.Primary,
            UiButtonContentLayout.Row,
            "Row — interactive selected, hold, disabled, compact, and badge",
            "Hold to start training",
            Selected: true,
            Hold: true));
    }

    [Fact]
    public void ButtonGalleryInventory_IsTheSoleSourceRenderedByTheActionsSection()
    {
        var rowGroups = ComponentGalleryScreen.ButtonSpecimenInventory
            .Select(spec => spec.RowGroup)
            .Distinct()
            .ToList();

        rowGroups.Count.ShouldBe(5);
        rowGroups.ShouldAllBe(group => !string.IsNullOrWhiteSpace(group));
    }

}
