namespace NodeRunner.Domain;

/// <summary>
/// Where a selection is marked and hit, in canvas units (#710), shared by Build's gestures
/// and the drawing in Build and Training. Every mark's centre line sits <see cref="Gap"/> outside its
/// part's edge: a joint's halo ring, and a beam's and a Piston's halo lines. A joint is also hit
/// within that ring, so its touch area follows the zoom.
/// </summary>
public static class SelectionMarks
{
    /// <summary>How far a selection mark's centre line sits outside its part's edge.</summary>
    public const double Gap = 3;

    /// <summary>The radius of a selected joint's halo ring: its own radius plus <see cref="Gap"/>.</summary>
    public static double JointHalo(double jointRadius) => jointRadius + Gap;

    /// <summary>
    /// How far along a link from a joint's centre one of its selection lines, <paramref name="offset"/>
    /// from the link's axis, meets a circle of <paramref name="reach"/> round that joint: the joint's
    /// edge, or its halo when the joint is selected too. Zero when the line passes outside the circle.
    /// </summary>
    public static double LineEnd(double reach, double offset) =>
        Math.Abs(offset) < reach ? Math.Sqrt((reach * reach) - (offset * offset)) : 0;
}
