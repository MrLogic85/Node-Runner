namespace NodeRunner.App.Services;

/// <summary>
/// Tells when training has run in slow motion for a while (#318, #531 D5). Physics always steps a
/// fixed tick, so a phone that cannot keep up runs fewer ticks than real time asks for: the results
/// stay right, but everything moves slowly. Fed every rendered frame, it reports once, the first time
/// physics has stayed below <see cref="BehindRatio"/> of real time for <see cref="WarnAfterSeconds"/>.
/// One long frame, like building a new generation's shadows, counts as at most
/// <see cref="MaxFrameSeconds"/>, so a single hitch is not slow motion.
/// </summary>
public sealed class SlowMotionWatch
{
    public const double BehindRatio = 0.9;
    public const double WarnAfterSeconds = 3;
    public const double MaxFrameSeconds = 0.25;

    // Ticks are whole, so the speed is judged over windows long enough to hold many of them.
    private const double _windowSeconds = 0.5;

    private readonly int _ticksPerSecond;
    private double _windowElapsed;
    private long _windowTicks;
    private double _behindSeconds;
    private bool _reported;

    public SlowMotionWatch(int ticksPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ticksPerSecond, 1);
        _ticksPerSecond = ticksPerSecond;
    }

    /// <summary>
    /// Adds one rendered frame: its real duration and the physics ticks run during it. Returns true
    /// exactly once, when slow motion has lasted long enough to tell the player.
    /// </summary>
    public bool Advance(double frameSeconds, long physicsTicks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frameSeconds);
        ArgumentOutOfRangeException.ThrowIfNegative(physicsTicks);
        if (_reported)
        {
            return false;
        }

        _windowElapsed += Math.Min(frameSeconds, MaxFrameSeconds);
        _windowTicks += physicsTicks;
        if (_windowElapsed < _windowSeconds)
        {
            return false;
        }

        var speed = _windowTicks / (_windowElapsed * _ticksPerSecond);
        _behindSeconds = speed < BehindRatio ? _behindSeconds + _windowElapsed : 0;
        _windowElapsed = 0;
        _windowTicks = 0;
        _reported = _behindSeconds >= WarnAfterSeconds;
        return _reported;
    }

    /// <summary>
    /// Forgets the partly measured stretch, for time that does not count: a pause, where physics
    /// stops on purpose.
    /// </summary>
    public void Restart()
    {
        _windowElapsed = 0;
        _windowTicks = 0;
        _behindSeconds = 0;
    }
}
