namespace NodeRunner.ML.Ga;

/// <summary>
/// What one trial measured. An invalid trial (a physics blow-up, see <see cref="TrialMeasurement"/>)
/// keeps its numbers for logging, but they mean nothing.
/// </summary>
public readonly record struct TrialResult(double Distance, double TopSpeed, double Elevation, bool IsValid = true)
{
    /// <summary>The GA's score: <see cref="Distance"/>, or negative infinity for an invalid trial so it sorts below every valid one.</summary>
    public double Fitness => IsValid ? Distance : double.NegativeInfinity;
}

/// <summary>
/// Measures one trial from a sample per physics tick: the creature centre's forward position and
/// the gap between its lowest part and the ground beneath it.
/// <list type="bullet">
/// <item><see cref="TrialResult.Distance"/>: the furthest forward the centre gets from its start.
/// The running maximum rewards peak progress without penalising a creature that surges forward
/// and then settles or wobbles back before the trial ends.</item>
/// <item><see cref="TrialResult.TopSpeed"/>: the highest forward speed of the centre, averaged
/// over a sliding window so a single physics spike does not count.</item>
/// <item><see cref="TrialResult.Elevation"/>: the largest ground clearance once the creature has
/// landed, so the drop it starts every trial with doesn't count. A crawler scores 0.</item>
/// </list>
/// A trial is invalid when a sample is not finite or either value moves more than
/// <see cref="MaxPlausibleSpeed"/> allows in one tick: physics blowing up, not a creature moving.
/// </summary>
public sealed class TrialMeasurement
{
    public const double SpeedWindowSeconds = 0.5;

    /// <summary>
    /// The fastest the centre or the lowest point can plausibly move, in creature units per second.
    /// Motors turn at most 6 rad/s, so even a part 1000 units from its joint moves 6000 units a
    /// second; the 360-unit Worm moves under 200. A blow-up jumps thousands of units in one tick.
    /// </summary>
    public const double MaxPlausibleSpeed = 10_000;

    /// <summary>A clearance at or below this counts as touching the ground, in creature units.</summary>
    public const double LandedClearance = 0.5;

    private readonly double[] _window;
    private readonly double _windowSeconds;
    private readonly double _maxStep;
    private int _next;
    private int _count;
    private double _startX;
    private double _distance;
    private double _topSpeed;
    private double _elevation;
    private bool _landed;
    private bool _valid;
    private double _lastX;
    private double _lastClearance;

    public TrialMeasurement(int ticksPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ticksPerSecond, 1);
        var windowTicks = Math.Max(1, (int)Math.Round(ticksPerSecond * SpeedWindowSeconds));
        _window = new double[windowTicks];
        _windowSeconds = (double)windowTicks / ticksPerSecond;
        _maxStep = MaxPlausibleSpeed / ticksPerSecond;
    }

    public TrialResult Result => new(_distance, _topSpeed, _elevation, _valid);

    /// <summary>Begins a new trial from the centre's starting X position.</summary>
    public void Reset(double startX)
    {
        _startX = startX;
        _distance = 0;
        _topSpeed = 0;
        _elevation = 0;
        _landed = false;
        _valid = double.IsFinite(startX);
        _lastX = startX;
        _lastClearance = double.NaN;
        _next = 0;
        _count = 0;
        Push(startX);
    }

    /// <summary>Records one physics tick. A non-finite or implausibly far-moved sample makes the trial invalid and is not measured.</summary>
    /// <param name="centerX">The creature centre's X position.</param>
    /// <param name="groundClearance">The gap between the lowest part and the ground beneath it; negative when it sinks in.</param>
    public void Record(double centerX, double groundClearance)
    {
        if (!_valid)
        {
            return;
        }

        if (!IsPlausible(centerX, groundClearance))
        {
            _valid = false;
            return;
        }

        _lastX = centerX;
        _lastClearance = groundClearance;
        _landed |= groundClearance <= LandedClearance;
        if (_landed)
        {
            _elevation = Math.Max(_elevation, groundClearance);
        }

        if (_count == _window.Length)
        {
            _topSpeed = Math.Max(_topSpeed, (centerX - _window[_next]) / _windowSeconds);
        }

        Push(centerX);
        _distance = Math.Max(_distance, centerX - _startX);
    }

    private bool IsPlausible(double centerX, double groundClearance) =>
        double.IsFinite(centerX)
        && double.IsFinite(groundClearance)
        && Math.Abs(centerX - _lastX) <= _maxStep
        && (double.IsNaN(_lastClearance) || Math.Abs(groundClearance - _lastClearance) <= _maxStep);

    private void Push(double centerX)
    {
        _window[_next] = centerX;
        _next = (_next + 1) % _window.Length;
        _count = Math.Min(_count + 1, _window.Length);
    }
}
