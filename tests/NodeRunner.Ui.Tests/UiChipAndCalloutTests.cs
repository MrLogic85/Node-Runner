using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiChipAndCalloutTests
{
    // Reference: a chip's border and its text/icon are always the same colour, whatever the kind.
    [Theory]
    [InlineData(UiChip.ChipKind.Warning, UiTokens.Color.Halo)]
    [InlineData(UiChip.ChipKind.Danger, UiTokens.Color.Danger)]
    [InlineData(UiChip.ChipKind.Ok, UiTokens.Color.Accent)]
    public void ChipKinds_ColourBorderAndContentAlike(UiChip.ChipKind kind, UiTokens.Color expected)
    {
        UiChip.ColorsFor(kind).ShouldBe((expected, expected));
    }

    [Fact]
    public void NeutralChip_IsInkOnLineStrong()
    {
        UiChip.ColorsFor(UiChip.ChipKind.Neutral).ShouldBe((UiTokens.Color.LineStrong, UiTokens.Color.Ink));
    }

    [Fact]
    public void ChipContentColours_AreTextColours()
    {
        foreach (var kind in Enum.GetValues<UiChip.ChipKind>())
        {
            UiTokens.IsTextColor(UiChip.ColorsFor(kind).Content).ShouldBeTrue(kind.ToString());
        }
    }

    [Fact]
    public void ChipHeights_AreControlSizes()
    {
        UiChip.HeightFor(large: false).ShouldBe(UiSize.Control.ExtraSmall);
        UiChip.HeightFor(large: true).ShouldBe(UiSize.Control.Default);
    }

    [Theory]
    [InlineData(UiCallout.CalloutKind.Warning, UiTokens.Color.Halo)]
    [InlineData(UiCallout.CalloutKind.Danger, UiTokens.Color.Danger)]
    [InlineData(UiCallout.CalloutKind.Ok, UiTokens.Color.Accent)]
    public void CalloutKinds_ShareChipBorderColours(UiCallout.CalloutKind kind, UiTokens.Color expected)
    {
        UiCallout.BorderFor(kind).ShouldBe(expected);
    }

    [Fact]
    public void Callout_DefaultsToWarning()
    {
        default(UiCallout.CalloutKind).ShouldBe(UiCallout.CalloutKind.Warning);
    }
}
