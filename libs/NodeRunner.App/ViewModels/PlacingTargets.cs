namespace NodeRunner.App.ViewModels;

/// <summary>
/// What a part being placed or a sensor being moved marks (#1107): the <see cref="Beams"/> and
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

    /// <summary>The radius of the joint part being placed, round which each joint that takes it is ringed (#1055); 0 otherwise.</summary>
    public double JointRing { get; init; }
}
