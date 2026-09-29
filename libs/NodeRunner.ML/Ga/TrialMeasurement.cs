namespace NodeRunner.ML.Ga;

/// <summary>What one trial measured: <see cref="Distance"/> is also its fitness.</summary>
public readonly record struct TrialResult(double Distance, double TopSpeed, double Elevation);

/// <summary>
/// Measures one trial from a sample per physics tick: the creature centre's forward position and
/// the gap between its lowest part and the ground beneath it.
/// <list type="bullet">
/// <item><see cref="TrialResult.Distance"/>: the furthest forward the centre gets from its start.
/// The running maximum rewards peak progress without penalising a creature that surges forward
/// and then settles or wobbles back before the trial ends.</item>
/// <item><see cref="TrialResult.TopSpeed"/>: the highest forward speed of the centre, averaged
/// over a sliding window so a single physics spike does not count.</item>
/// <item><see cref="TrialResult.Elevation"/>: the largest ground clearance. A crawler scores 0.</item>
/// </list>
/// </summary>
public sealed class TrialMeasurement
{
    public const double SpeedWindowSeconds = 0.5;

    private readonly double[] _window;
    private readonly double _windowSeconds;
    private int _next;
    private int _count;
    private double _startX;
    private double _distance;
    private double _topSpeed;
    private double _elevation;

    public TrialMeasurement(int ticksPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ticksPerSecond, 1);
        var windowTicks = Math.Max(1, (int)Math.Round(ticksPerSecond * SpeedWindowSeconds));
        _window = new double[windowTicks];
        _windowSeconds = (double)windowTicks / ticksPerSecond;
    }

    public TrialResult Result => new(_distance, _topSpeed, _elevation);

    /// <summary>Begins a new trial from the centre's starting X position.</summary>
    public void Reset(double startX)
    {
        _startX = startX;
        _distance = 0;
        _topSpeed = 0;
        _elevation = 0;
        _next = 0;
        _count = 0;
        Push(startX);
    }

    /// <summary>Records one physics tick. A non-finite sample (no beams, a physics blow-up) is skipped.</summary>
    /// <param name="centerX">The creature centre's X position.</param>
    /// <param name="groundClearance">The gap between the lowest part and the ground beneath it; negative when it sinks in.</param>
    public void Record(double centerX, double groundClearance)
    {
        if (double.IsFinite(groundClearance))
        {
            _elevation = Math.Max(_elevation, groundClearance);
        }

        if (!double.IsFinite(centerX))
        {
            return;
        }

        if (_count == _window.Length)
        {
            _topSpeed = Math.Max(_topSpeed, (centerX - _window[_next]) / _windowSeconds);
        }

        Push(centerX);
        _distance = Math.Max(_distance, centerX - _startX);
    }

    private void Push(double centerX)
    {
        _window[_next] = centerX;
        _next = (_next + 1) % _window.Length;
        _count = Math.Min(_count + 1, _window.Length);
    }
}
