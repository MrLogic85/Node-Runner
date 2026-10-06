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
    public void TheWarning_StaysAMinuteAfterSlowMotionEnds()
    {
        var watch = new SlowMotionWatch(_tickRate);
        Run(watch, seconds: 4, speed: 0.5).ShouldNotBeNull();

        // The last measured window may still hold some of the slow stretch.
        LastShown(watch, seconds: 120, speed: 1).ShouldNotBeNull()
            .ShouldBeInRange(SlowMotionWatch.ShowSeconds - 1, SlowMotionWatch.ShowSeconds + 1);
        watch.Advance(_frame, 1).ShouldBeFalse();
    }

    [Fact]
    public void TheWarning_StaysWhileSlowMotionLasts()
    {
        var watch = new SlowMotionWatch(_tickRate);

        LastShown(watch, seconds: 3 * SlowMotionWatch.ShowSeconds, speed: 0.5)
            .ShouldNotBeNull().ShouldBeGreaterThan(3 * SlowMotionWatch.ShowSeconds - 1);
    }

    [Fact]
    public void SlowMotionAgain_StartsTheMinuteOver()
    {
        var watch = new SlowMotionWatch(_tickRate);
        Run(watch, seconds: 4, speed: 0.5).ShouldNotBeNull();
        LastShown(watch, seconds: 50, speed: 1).ShouldNotBeNull().ShouldBeGreaterThan(49);

        Run(watch, seconds: 4, speed: 0.5).ShouldNotBeNull();

        LastShown(watch, seconds: 120, speed: 1).ShouldNotBeNull()
            .ShouldBeInRange(SlowMotionWatch.ShowSeconds - 1, SlowMotionWatch.ShowSeconds + 1);
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
    public void Restart_KeepsAShownWarning()
    {
        var watch = new SlowMotionWatch(_tickRate);
        Run(watch, seconds: 4, speed: 0.5).ShouldNotBeNull();

        watch.Restart();

        watch.Advance(_frame, 1).ShouldBeTrue();
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
    // ticks over, and returns when the warning first shows, or null if it does not.
    private static double? Run(SlowMotionWatch watch, double seconds, double speed, double frameSeconds = _frame) =>
        Feed(watch, seconds, speed, frameSeconds).Cast<double?>().FirstOrDefault();

    // Like Run, for the whole time, and returns when the warning last showed, or null if it did not.
    private static double? LastShown(SlowMotionWatch watch, double seconds, double speed, double frameSeconds = _frame) =>
        Feed(watch, seconds, speed, frameSeconds).Cast<double?>().LastOrDefault();

    private static IEnumerable<double> Feed(SlowMotionWatch watch, double seconds, double speed, double frameSeconds)
    {
        var ticksPerSecond = speed * _tickRate;
        var carried = 0.0;
        var shown = new List<double>();
        for (var elapsed = 0.0; elapsed < seconds; elapsed += frameSeconds)
        {
            carried += ticksPerSecond * frameSeconds;
            var ticks = (long)carried;
            carried -= ticks;
            if (watch.Advance(frameSeconds, ticks))
            {
                shown.Add(elapsed + frameSeconds);
            }
        }

        return shown;
    }
}
