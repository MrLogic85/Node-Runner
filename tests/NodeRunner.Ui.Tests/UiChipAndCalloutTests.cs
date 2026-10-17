using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

public sealed class UiChipAndCalloutTests
{
    // Reference: a chip's border and its text/icon are the same colour, except Neutral's ink on line-strong;
    // the content is always a text colour.
    [Theory]
    [InlineData(UiChip.ChipKind.Warning, UiTokens.Color.Halo, UiTokens.Color.Halo)]
    [InlineData(UiChip.ChipKind.Danger, UiTokens.Color.Danger, UiTokens.Color.Danger)]
    [InlineData(UiChip.ChipKind.Ok, UiTokens.Color.Accent, UiTokens.Color.Accent)]
    [InlineData(UiChip.ChipKind.Neutral, UiTokens.Color.LineStrong, UiTokens.Color.Ink)]
    public void ChipKinds_ColourBorderAndContent(UiChip.ChipKind kind, UiTokens.Color border, UiTokens.Color content)
    {
        UiChip.ColorsFor(kind).ShouldBe((border, content));
        UiTokens.IsTextColor(content).ShouldBeTrue();
    }

    [Fact]
    public void CalloutKinds_ShareChipBorderColours()
    {
        foreach (var kind in Enum.GetValues<UiCallout.CalloutKind>())
        {
            UiCallout.BorderFor(kind).ShouldBe(UiChip.ColorsFor(Enum.Parse<UiChip.ChipKind>(kind.ToString())).Border, kind.ToString());
        }
    }

    [Fact]
    public void ChipIcons_AreSmall_UnlessAPartGlyphOrBesideOne()
    {
        UiChip.IconSizeFor(UiIconId.Bolt, glyphSized: false).ShouldBe(UiIconSize.Small);
        UiChip.IconSizeFor(UiIconId.PartSpring, glyphSized: false).ShouldBe(UiIconSize.Standard);
        UiChip.IconSizeFor(UiIconId.MapHills, glyphSized: true).ShouldBe(UiIconSize.Standard);
    }
}
