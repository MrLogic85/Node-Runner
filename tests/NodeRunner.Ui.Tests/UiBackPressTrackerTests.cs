using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Tests;

/// <summary>
/// Replays Back sequences through <see cref="UiBackPressTracker"/>. Steps are a kind and a time in
/// milliseconds: <c>g</c> go-back with the key up, <c>G</c> go-back with the key held, <c>d</c>
/// key-down, <c>e</c> key-down echo, <c>u</c> key-up. The expected string has one letter per go-back:
/// <c>F</c> for a press's first signal, <c>r</c> for a repeat.
/// </summary>
public sealed class UiBackPressTrackerTests
{
    [Theory]
    [InlineData("g9680 d9693 u9694 g9694", "Fr")]
    [InlineData("d9705 G9711 G9910 u9913", "Fr")]
    [InlineData("d9678 G9682 G10186 e10187 u10187 g10188", "Frr")]
    public void A_press_recorded_on_the_S25_is_taken_once(string steps, string expected) =>
        Replay(steps).ShouldBe(expected);

    [Theory]
    [InlineData("g0 d13 u14 g14 g300 d313 u314 g314", "FrFr")]
    [InlineData("g0 d13 u14 g14 g80 d93 u94 g94", "FrFr")]
    [InlineData("d0 G5 u100 g101 d180 G185 u280 g281", "FrFr")]
    [InlineData("d0 G5 G205 u208 d260 G265 u400 g401", "FrFr")]
    public void A_quick_second_press_is_a_new_press(string steps, string expected) =>
        Replay(steps).ShouldBe(expected);

    // On the S23 (#838) a press's key-down and key-up come before its only go-back, and every other
    // press was swallowed.
    [Theory]
    [InlineData("d0 u60 g62 d3000 u3060 g3062 d6000 u6060 g6062", "FFF")]
    [InlineData("d0 u60 g62 g70 d3000 u3060 g3062", "FrF")]
    [InlineData("d0 u250 g252 d350 u410 g412", "FF")]
    [InlineData("d0 e500 e550 u800 g802 d900 u960 g962", "FF")]
    public void A_press_whose_key_up_comes_before_its_go_back_is_taken_once(string steps, string expected) =>
        Replay(steps).ShouldBe(expected);

    // A key-up that never arrives must not swallow the next press (#838).
    [Theory]
    [InlineData("d0 G5 g101 d5000 G5005 u5100 g5101", "FrFr")]
    [InlineData("d0 G5 d5000 u5060 g5062", "FF")]
    public void A_lost_key_up_does_not_swallow_the_next_press(string steps, string expected) =>
        Replay(steps).ShouldBe(expected);

    [Fact]
    public void An_old_key_down_without_a_go_back_does_not_make_a_later_press_act_twice() =>
        Replay("d0 g5000 d5013 u5014 g5014").ShouldBe("Fr");

    [Theory]
    [InlineData("g0 g10", "Fr")]
    [InlineData("g0 g199", "Fr")]
    [InlineData("g0 g200", "FF")]
    public void Without_key_events_a_pause_ends_the_press(string steps, string expected) =>
        Replay(steps).ShouldBe(expected);

    [Fact]
    public void A_go_back_well_after_the_release_is_a_new_press() =>
        Replay("d0 G5 u100 g100 g150").ShouldBe("FrF");

    private static string Replay(string steps)
    {
        var tracker = new UiBackPressTracker();
        var taken = new List<char>();
        foreach (var step in steps.Split(' '))
        {
            var now = ulong.Parse(step[1..], System.Globalization.CultureInfo.InvariantCulture);
            switch (step[0])
            {
                case 'g' or 'G':
                    taken.Add(tracker.GoBack(now, backHeld: step[0] == 'G') ? 'F' : 'r');
                    break;
                case 'd':
                    tracker.Key(now, pressed: true, echo: false);
                    break;
                case 'e':
                    tracker.Key(now, pressed: true, echo: true);
                    break;
                case 'u':
                    tracker.Key(now, pressed: false, echo: false);
                    break;
                default:
                    throw new ArgumentException($"Unknown step {step}", nameof(steps));
            }
        }

        return new string([.. taken]);
    }
}
