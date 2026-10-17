using NodeRunner.Ui.Lib;
using static NodeRunner.Ui.Lib.UiCalloutGrid.Cell;

namespace NodeRunner.Ui.Tests;

/// <summary>The grid a callout builds for its meter lines.</summary>
public sealed class UiCalloutGridTests
{
    private static UiCalloutLine Line(int meters, string? heading = null) => UiCalloutLine.OfMeters(
        UiTokens.Color.Accent,
        [.. Enumerable.Range(0, meters).Select(index => new UiCalloutMeter($"m{index}", 0, Centred: true))],
        heading);

    [Fact]
    public void AWrappingHeadedLine_ShowsItsHeadingOnTheFirstRowOnly_AndKeepsEveryRowInStep()
    {
        var cells = UiCalloutGrid.Cells(Line(6, "In"), withColumn: true).ToList();

        cells.ShouldBe([
            Heading, Label, Bar, SpacedLabel, Bar, SpacedLabel, Bar,
            Blank, Label, Bar, SpacedLabel, Bar, SpacedLabel, Bar,
        ]);

        // Each row starts in the first column, so the second row's labels sit under the first's.
        var columns = UiCalloutGrid.Columns(withColumn: true);
        cells.Count.ShouldBe(2 * columns);
        cells[columns].ShouldBe(Blank);
    }

    [Fact]
    public void ALineWithoutAHeading_UnderAGlyph_StillKeepsTheColumnCell()
    {
        UiCalloutGrid.Cells(Line(2), withColumn: true).ShouldBe([Heading, Label, Bar, SpacedLabel, Bar]);
    }

    [Fact]
    public void WithoutAColumn_AWrappedRowStartsWithItsLabel()
    {
        var cells = UiCalloutGrid.Cells(Line(4), withColumn: false).ToList();

        cells.ShouldBe([Label, Bar, SpacedLabel, Bar, SpacedLabel, Bar, Label, Bar]);
        cells[UiCalloutGrid.Columns(withColumn: false)].ShouldBe(Label);
    }

    [Fact]
    public void ACaptionHasAColumn_WithAGlyphOrALineHeading()
    {
        UiCalloutGrid.HasColumn(UiIconId.PartPiston, [Line(1)]).ShouldBeTrue();
        UiCalloutGrid.HasColumn(UiIconId.None, [Line(1), Line(1, "Out")]).ShouldBeTrue();
        UiCalloutGrid.HasColumn(UiIconId.None, [Line(1), UiCalloutLine.OfNote("No network ports")]).ShouldBeFalse();
    }

    [Fact]
    public void LinesAreRebuilt_WhenTheColumnComesOrGoes_ButNotForANewHeadingText()
    {
        IReadOnlyList<UiCalloutLine> built = [Line(1, "In")];

        UiCalloutGrid.SameGrid(built, builtWithColumn: true, [Line(1, "Ent")], withColumn: true).ShouldBeTrue();
        UiCalloutGrid.SameGrid(built, builtWithColumn: true, [Line(1)], withColumn: true).ShouldBeTrue();
        UiCalloutGrid.SameGrid([Line(1)], builtWithColumn: false, [Line(1)], withColumn: true).ShouldBeFalse();
        UiCalloutGrid.SameGrid(built, builtWithColumn: true, [Line(2, "In")], withColumn: true).ShouldBeFalse();
    }
}
