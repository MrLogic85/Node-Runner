using NodeRunner.App.Services;

namespace NodeRunner.App.Tests.Services;

public sealed class SlowMotionWatchTests
{
    private const int _tickRate = 60;
    private const double _frame = 1.0 / 60;

    [Fact]
    public void RealTime_NeverWarns()
    {
        var watch = new SlowMotionWatch(_tickRate);

        Run(watch, seconds: 30, speed: 1).ShouldBeNull();
    }

    [Fact]
    public void SlowMotion_WarnsAfterAboutThreeSeconds()
    {
        var watch = new SlowMotionWatch(_tickRate);

        // Measured at 100 shadows on a heavy creature: about 6 fps at 0.84x real time.
        var warnedAt = Run(watch, seconds: 10, speed: 0.84, frameSeconds: 1.0 / 6);

        warnedAt.ShouldNotBeNull();
        warnedAt.Value.ShouldBeInRange(SlowMotionWatch.WarnAfterSeconds - 0.01, SlowMotionWatch.WarnAfterSeconds + 1);
    }

    [Fact]
    public void Warns_OnlyOnce()
    {
        var watch = new SlowMotionWatch(_tickRate);
        Run(watch, seconds: 10, speed: 0.5).ShouldNotBeNull();

        Run(watch, seconds: 30, speed: 0.5).ShouldBeNull();
    }

    [Fact]
    public void ShortSlowStretches_DoNotAddUp()
    {
        var watch = new SlowMotionWatch(_tickRate);

        for (var i = 0; i < 10; i++)
        {
            Run(watch, seconds: 2, speed: 0.5).ShouldBeNull();
            Run(watch, seconds: 1, speed: 1).ShouldBeNull();
        }
    }

    [Fact]
    public void OneLongFrame_IsAHitch_NotSlowMotion()
    {
        var watch = new SlowMotionWatch(_tickRate);

        // Building a new generation, or coming back from the background, can stall one frame for
        // longer than the warning waits, running only 8 ticks.
        for (var i = 0; i < 10; i++)
        {
            watch.Advance(SlowMotionWatch.WarnAfterSeconds + 1, 8).ShouldBeFalse();
            Run(watch, seconds: 10, speed: 1).ShouldBeNull();
        }
    }

    [Fact]
    public void VerySlowFrames_StillWarn()
    {
        var watch = new SlowMotionWatch(_tickRate);

        // 3 fps at the engine's 8 ticks a frame: each frame is as long as a hitch, but they go on.
        Run(watch, seconds: 20, speed: 8 * 3.0 / _tickRate, frameSeconds: 1.0 / 3).ShouldNotBeNull();
    }

    [Fact]
    public void Restart_ForgetsTheSlowStretchSoFar()
    {
        var watch = new SlowMotionWatch(_tickRate);

        Run(watch, seconds: 2.5, speed: 0.5).ShouldBeNull();
        watch.Restart();
        Run(watch, seconds: 2.5, speed: 0.5).ShouldBeNull();
    }

    [Fact]
    public void RejectsNegativeInput()
    {
        var watch = new SlowMotionWatch(_tickRate);

        Should.Throw<ArgumentOutOfRangeException>(() => watch.Advance(-1, 0));
        Should.Throw<ArgumentOutOfRangeException>(() => watch.Advance(0, -1));
        Should.Throw<ArgumentOutOfRangeException>(() => new SlowMotionWatch(0));
    }

    // Feeds frames of real time with physics at a share of real time's ticks, carrying the part
    // ticks over, and returns the time of the warning, or null without one.
    private static double? Run(SlowMotionWatch watch, double seconds, double speed, double frameSeconds = _frame)
    {
        var ticksPerSecond = speed * _tickRate;
        var carried = 0.0;
        for (var elapsed = 0.0; elapsed < seconds; elapsed += frameSeconds)
        {
            carried += ticksPerSecond * frameSeconds;
            var ticks = (long)carried;
            carried -= ticks;
            if (watch.Advance(frameSeconds, ticks))
            {
                return elapsed + frameSeconds;
            }
        }

        return null;
    }
}
