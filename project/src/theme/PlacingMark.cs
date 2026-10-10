namespace NodeRunner.Theme;

/// <summary>
/// How a beam or joint shows a part being placed or a sensor being moved (#376, #1016): it draws
/// the mark with itself, so the mark sits on its own layer (#1107).
/// </summary>
public enum PlacingMark
{
    None,

    /// <summary>It would take the part: <c>halo</c>, raised with it to the selected surface.</summary>
    Takes,

    /// <summary>It would refuse the part: dashed <c>danger</c>.</summary>
    Refuses,
}
