namespace NodeRunner.App.Services;

/// <summary>
/// Tells when training runs in slow motion (#318). Physics always steps a fixed tick, so a phone that
/// cannot keep up runs fewer ticks than real time asks for: the results stay right, but everything
/// moves slowly. Fed every rendered frame, it warns once physics has stayed below
/// <see cref="BehindRatio"/> of real time for <see cref="WarnAfterSeconds"/>, and keeps warning until
/// <see cref="ShowSeconds"/> after the last such stretch, so a player who glances away still sees it.
/// One long frame, like building a new generation's shadows, counts as at most
/// <see cref="MaxFrameSeconds"/>, so a single hitch is not slow motion.
/// </summary>
public sealed class SlowMotionWatch
{
    public const double BehindRatio = 0.9;
    public const double WarnAfterSeconds = 3;
    public const double MaxFrameSeconds = 0.25;
    public const double ShowSeconds = 60;

    // Ticks are whole, so the speed is judged over windows long enough to hold many of them.
    private const double _windowSeconds = 0.5;

    private readonly int _ticksPerSecond;
    private double _windowElapsed;
    private long _windowTicks;
    private double _behindSeconds;
    private double _showLeft;

    public SlowMotionWatch(int ticksPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ticksPerSecond, 1);
        _ticksPerSecond = ticksPerSecond;
    }

    /// <summary>
    /// Adds one rendered frame: its real duration and the physics ticks run during it. Returns whether
    /// the warning shows now.
    /// </summary>
    public bool Advance(double frameSeconds, long physicsTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frameSeconds);
        ArgumentOutOfRangeException.ThrowIfNegative(physicsTicks);
        _showLeft = Math.Max(_showLeft - frameSeconds, 0);
        _windowElapsed += Math.Min(frameSeconds, MaxFrameSeconds);
        _windowTicks += physicsTicks;
        if (_windowElapsed < _windowSeconds)
        {
            return _showLeft > 0;
        }

        var speed = _windowTicks / (_windowElapsed * _ticksPerSecond);
        _behindSeconds = speed < BehindRatio ? _behindSeconds + _windowElapsed : 0;
        _windowElapsed = 0;
        _windowTicks = 0;
        if (_behindSeconds >= WarnAfterSeconds)
        {
            _showLeft = ShowSeconds;
        }

        return _showLeft > 0;
    }

    /// <summary>
    /// Forgets the partly measured stretch, for time that does not count: a pause, where physics
    /// stops on purpose. A shown warning keeps its time left.
    /// </summary>
    public void Restart()
    {
        _windowElapsed = 0;
        _windowTicks = 0;
        _behindSeconds = 0;
    }
}
