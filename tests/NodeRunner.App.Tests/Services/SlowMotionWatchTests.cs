using NodeRunner.App.Services;

namespace NodeRunner.App.Tests.Services;

public sealed class SlowMotionWatchTests
{
    private const int _tickRate = 60;
    private const double _frame = 1.0 / 60;

    // Real time, one long frame (a new generation, or coming back from the background, running only 8
    // ticks), short slow stretches and a slow stretch cut by Restart never warn.
    [Fact]
    public void OnlySustainedSlowMotion_Warns()
    {
        var watch = new SlowMotionWatch(_tickRate);

        Run(watch, seconds: 30, speed: 1).ShouldBeNull();
        for (var i = 0; i < 10; i++)
        {
            watch.Advance(SlowMotionWatch.WarnAfterSeconds + 1, 8).ShouldBeFalse();
            Run(watch, seconds: 10, speed: 1).ShouldBeNull();
            Run(watch, seconds: 2, speed: 0.5).ShouldBeNull();
            Run(watch, seconds: 1, speed: 1).ShouldBeNull();
        }

        Run(watch, seconds: 2.5, speed: 0.5).ShouldBeNull();
        watch.Restart();
        Run(watch, seconds: 2.5, speed: 0.5).ShouldBeNull();

        // 3 fps at the engine's 8 ticks a frame: each frame is as long as a hitch, but they go on.
        Run(watch, seconds: 20, speed: 8 * 3.0 / _tickRate, frameSeconds: 1.0 / 3).ShouldNotBeNull();
        Should.Throw<ArgumentOutOfRangeException>(() => watch.Advance(-1, 0));
        Should.Throw<ArgumentOutOfRangeException>(() => watch.Advance(0, -1));
        Should.Throw<ArgumentOutOfRangeException>(() => new SlowMotionWatch(0));
    }

    // Shows after about three seconds, stays while slow and a minute after, survives Restart, and
    // slowness again starts the minute over.
    [Fact]
    public void TheWarning_ShowsAfterAboutThreeSeconds_AndStaysAMinuteAfterSlowMotionEnds()
    {
        var watch = new SlowMotionWatch(_tickRate);

        // Measured at 100 shadows on a heavy creature: about 6 fps at 0.84x real time.
        Run(watch, seconds: 10, speed: 0.84, frameSeconds: 1.0 / 6).ShouldNotBeNull()
            .ShouldBeInRange(SlowMotionWatch.WarnAfterSeconds - 0.01, SlowMotionWatch.WarnAfterSeconds + 1);
        LastShown(watch, seconds: 3 * SlowMotionWatch.ShowSeconds, speed: 0.5)
            .ShouldNotBeNull().ShouldBeGreaterThan(3 * SlowMotionWatch.ShowSeconds - 1);
        watch.Restart();
        watch.Advance(_frame, 1).ShouldBeTrue();
        LastShown(watch, seconds: 50, speed: 1).ShouldNotBeNull().ShouldBeGreaterThan(49);
        Run(watch, seconds: 4, speed: 0.5).ShouldNotBeNull();

        // The last measured window may still hold some of the slow stretch.
        LastShown(watch, seconds: 120, speed: 1).ShouldNotBeNull()
            .ShouldBeInRange(SlowMotionWatch.ShowSeconds - 1, SlowMotionWatch.ShowSeconds + 1);
        watch.Advance(_frame, 1).ShouldBeFalse();
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
