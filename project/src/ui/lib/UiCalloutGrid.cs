namespace NodeRunner.Ui.Lib;

/// <summary>
/// The grid a <see cref="UiCallout"/> builds for its meter lines, apart from the nodes so it
/// can be tested. A caption with a glyph or a line heading has a first column, the glyph above the
/// headings; each meter then takes a label cell and a bar cell, <see cref="UiCalloutLine.MetersPerRow"/> to a row.
/// </summary>
public static class UiCalloutGrid
{
    public enum Cell
    {
        /// <summary>The line's heading in the first column, empty text when it has none.</summary>
        Heading,

        /// <summary>An empty first-column cell starting a wrapped row.</summary>
        Blank,

        /// <summary>A meter's label starting a row.</summary>
        Label,

        /// <summary>A meter's label after another meter, set off by a wider gap.</summary>
        SpacedLabel,

        Bar,
    }

    public static bool HasColumn(UiIconId iconId, IReadOnlyList<UiCalloutLine> lines) =>
        iconId != UiIconId.None || lines.Any(line => line.Heading is not null);

    public static int Columns(bool withColumn) => (UiCalloutLine.MetersPerRow * 2) + (withColumn ? 1 : 0);

    /// <summary>A meter line's cells in grid order; the heading shows on its first row only.</summary>
    public static IEnumerable<Cell> Cells(UiCalloutLine line, bool withColumn)
    {
        if (withColumn)
        {
            yield return Cell.Heading;
        }

        for (var index = 0; index < line.Meters.Count; index++)
        {
            var startsRow = index % UiCalloutLine.MetersPerRow == 0;
            if (withColumn && startsRow && index > 0)
            {
                yield return Cell.Blank;
            }

            yield return startsRow ? Cell.Label : Cell.SpacedLabel;
            yield return Cell.Bar;
        }
    }

    /// <summary>Whether lines built as <paramref name="built"/> can take <paramref name="lines"/> in place, so nothing is rebuilt.</summary>
    public static bool SameGrid(IReadOnlyList<UiCalloutLine>? built, bool builtWithColumn, IReadOnlyList<UiCalloutLine> lines, bool withColumn) =>
        builtWithColumn == withColumn && UiCalloutLine.SameShape(built, lines);
}
