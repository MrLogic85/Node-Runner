namespace NodeRunner.App.ViewModels;

/// <summary>
/// What a part being placed or a sensor being moved marks: the <see cref="Beams"/> and
/// <see cref="Joints"/> that would take it, which rise alone to the selected surface, the moved
/// sensor, and the beams and joints that would refuse it. The canvas draws them and a touch hits
/// them (<see cref="BuildViewModel.DrawnPartAt"/>) from the same <see cref="BuildViewModel.RaisedParts"/>.
/// </summary>
public sealed record PlacingTargets(IReadOnlySet<int> Beams, IReadOnlySet<int> Joints, int? MovingSensor = null)
{
    private static readonly IReadOnlySet<int> _empty = new HashSet<int>();

    public static readonly PlacingTargets None = new(_empty, _empty);

    /// <summary>The beams that would refuse the sensor: each shows a dashed <c>danger</c> stroke.</summary>
    public IReadOnlySet<int> RefusedBeams { get; init; } = _empty;

    /// <summary>The joints that hold a part and would refuse the joint part: each shows a dashed <c>danger</c> ring.</summary>
    public IReadOnlySet<int> RefusedJoints { get; init; } = _empty;

    /// <summary>
    /// The radius each joint is ringed round while a joint part is placed
    /// (<see cref="PartTray.PlacingRingRadius"/>, clear of an Angle sensor's arc: <see cref="BuildViewModel.RingRadiusAt"/>); empty otherwise.
    /// </summary>
    public IReadOnlyDictionary<int, double> JointRings { get; init; } = new Dictionary<int, double>();

    /// <summary>
    /// The radius each of the <see cref="RefusedJoints"/> draws its dashed ring round: its own, clear of an
    /// Angle sensor's arc (<see cref="BuildViewModel.RingRadiusAt"/>). A touch still lands on it within
    /// <see cref="JointRingOf"/>.
    /// </summary>
    public IReadOnlyDictionary<int, double> RefusedJointRings { get; init; } = new Dictionary<int, double>();

    /// <summary>
    /// The radius joint <paramref name="nodeId"/> is ringed round while a joint part is placed: the
    /// ring the canvas draws there and where a tap or drop lands on it.
    /// </summary>
    public double JointRingOf(int nodeId) => JointRings[nodeId];

    /// <summary>The radius refused joint <paramref name="nodeId"/> draws its dashed ring round (<see cref="RefusedJointRings"/>).</summary>
    public double RefusedRingOf(int nodeId) => RefusedJointRings[nodeId];
}
