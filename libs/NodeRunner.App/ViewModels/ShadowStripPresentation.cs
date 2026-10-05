namespace NodeRunner.App.ViewModels;

/// <summary>One cell of the shadow strip: a shadow, its bar (0–1) and its marks.</summary>
public sealed record ShadowStripCell(int Number, double Fill, bool IsFollowed);

/// <summary>What the strip's last place holds once it pages (#387).</summary>
public enum ShadowStripTrailing
{
    None,

    /// <summary>On the first page: re-sorts by distance so far.</summary>
    Sort,

    /// <summary>On a later page: pages back toward the better shadows.</summary>
    Better,
}

/// <summary>
/// The shadow strip as shown: its cells, worst on the left and best on the right, and the paging
/// buttons around them. <see cref="Pages"/> puts a "worse" button in the first place.
/// </summary>
public sealed record ShadowStripView(
    IReadOnlyList<ShadowStripCell> Cells,
    bool Pages,
    bool CanPageWorse,
    ShadowStripTrailing Trailing)
{
    public static readonly ShadowStripView Empty = new([], false, false, ShadowStripTrailing.None);
}

/// <summary>
/// Which shadows the shadow strip shows, in what order, and how full each bar is (#387). Up to
/// <see cref="Places"/> shadows each get a cell; the strip sets the places to as many as fit its
/// width (#791). Past that the strip keeps its <see cref="Places"/> places: a "worse" button,
/// shadows in all but two places, and a last button that sorts (first page) or pages back up (later
/// pages). The order is the shadows' own, shadow 1 (the previous best)
/// on top, until the player sorts; a new generation starts over from that order and the first page.
/// Sorting is a snapshot, so cells never jump around while the player watches.
/// </summary>
public sealed class ShadowStripPresentation
{
    /// <summary>The places until the strip says how many fit.</summary>
    public const int DefaultPlaces = 8;

    /// <summary>The fewest places that still page: a "worse" button, one shadow and the last button.</summary>
    public const int FewestPlaces = 3;

    // Zero-based shadows, best (top) first.
    private int[] _ranking = [];
    private int _page;
    private int _generation = -1;
    private int _places = DefaultPlaces;

    /// <summary>
    /// The places the strip has, at least <see cref="FewestPlaces"/>. A change keeps the order and
    /// the page number, or goes to the last page if there are now fewer pages.
    /// </summary>
    public int Places
    {
        get => _places;
        set
        {
            _places = Math.Max(value, FewestPlaces);
            _page = Math.Min(_page, LastPage(_ranking.Length));
        }
    }

    // The shadows on one page once the strip pages: the places less the two buttons.
    private int CellsPerPage => _places - 2;

    /// <summary>
    /// The strip for <paramref name="shadows"/> in <paramref name="generation"/>. Bars measure each
    /// shadow's distance against this generation's leader, whose bar is full. Not against the
    /// best ever: that may come from a run with another trial length (owner decision).
    /// </summary>
    public ShadowStripView View(IReadOnlyList<ShadowStanding> shadows, int generation)
    {
        ArgumentNullException.ThrowIfNull(shadows);
        var shown = Shown(shadows.Count, generation);
        if (shadows.Count == 0)
        {
            return ShadowStripView.Empty;
        }

        var scale = Scale(shadows);
        var pages = shadows.Count > _places;
        var cells = shown
            .Reverse()
            .Select(index => shadows[index])
            .Select(shadow => new ShadowStripCell(shadow.Number, Fill(shadow.Distance, scale), shadow.IsFollowed))
            .ToArray();
        return new ShadowStripView(
            cells,
            pages,
            pages && _page < LastPage(shadows.Count),
            !pages ? ShadowStripTrailing.None : _page == 0 ? ShadowStripTrailing.Sort : ShadowStripTrailing.Better);
    }

    /// <summary>
    /// The zero-based shadows on the strip's current page, best (top) first, out of
    /// <paramref name="count"/> in <paramref name="generation"/>: all of them unless the strip pages.
    /// Training draws only these and the followed shadow (#284).
    /// </summary>
    public IReadOnlyList<int> Shown(int count, int generation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (generation != _generation || count != _ranking.Length)
        {
            _generation = generation;
            _ranking = Enumerable.Range(0, count).ToArray();
            _page = 0;
        }

        return (count > _places ? _ranking.Skip(PageStart(count)).Take(CellsPerPage) : _ranking).ToArray();
    }

    /// <summary>
    /// Ranks <paramref name="shadows"/> of <paramref name="generation"/> by distance so far,
    /// furthest on top, and shows the first page.
    /// </summary>
    public void Sort(IReadOnlyList<ShadowStanding> shadows, int generation)
    {
        ArgumentNullException.ThrowIfNull(shadows);
        _generation = generation;
        _ranking = shadows
            .Select((shadow, index) => (Distance: double.IsFinite(shadow.Distance) ? shadow.Distance : double.NegativeInfinity, Index: index))
            .OrderByDescending(shadow => shadow.Distance)
            .ThenBy(shadow => shadow.Index)
            .Select(shadow => shadow.Index)
            .ToArray();
        _page = 0;
    }

    /// <summary>Pages toward the worse shadows, unless already on the last page.</summary>
    public void PageWorse() => _page = Math.Min(_page + 1, LastPage(_ranking.Length));

    /// <summary>Pages back toward the better shadows, unless already on the first page.</summary>
    public void PageBetter() => _page = Math.Max(_page - 1, 0);

    // The last page shows the worst CellsPerPage shadows, overlapping the page before, so a page
    // never has empty places.
    private int PageStart(int count) => Math.Min(_page * CellsPerPage, count - CellsPerPage);

    private int LastPage(int count) => count <= _places ? 0 : (count - 1) / CellsPerPage;

    private static double Scale(IReadOnlyList<ShadowStanding> shadows)
    {
        var scale = 0.0;
        foreach (var shadow in shadows)
        {
            if (double.IsFinite(shadow.Distance))
            {
                scale = Math.Max(scale, shadow.Distance);
            }
        }

        return scale;
    }

    private static double Fill(double distance, double scale) =>
        scale > 0 && double.IsFinite(distance) ? Math.Clamp(distance / scale, 0, 1) : 0;
}
