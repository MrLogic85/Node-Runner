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
    [InlineData("d24270 u24281 g24306 d38827 u38828 g38828", "FF")]
    [InlineData("d193955 G193956 e194373 u194374 g194377", "Fr")]
    public void A_press_recorded_on_the_emulator_is_taken_once(string steps, string expected) =>
        Replay(steps).ShouldBe(expected);

    [Theory]
    [InlineData("d0 G1 e500 G500 u2500 g2501", "Frr")]
    [InlineData("g0 d13 e500 G500 u5000 g5001", "Frr")]
    public void A_long_held_press_is_taken_once(string steps, string expected) =>
        Replay(steps).ShouldBe(expected);

    [Theory]
    [InlineData("g0 d13 u14 g14 g150 d163 u164 g164", "FrrF")]
    [InlineData("d0 u10 g11 d100 u110 g111", "FF")]
    public void A_quick_second_press_is_a_new_press(string steps, string expected) =>
        Replay(steps).ShouldBe(expected);

    [Theory]
    [InlineData("d0 u1 g2 g3 d5000 G5005 u5100 g5101", "FrFr")]
    [InlineData("g0 d13 g14 d5000 G5005 u5100 g5101", "FrFr")]
    [InlineData("g0 d13 g14 d5000 u5001 g5002", "FrF")]
    [InlineData("g0 d5000 G5005 u5100 g5101", "FFr")]
    [InlineData("g0 d5000 u5010 g5035", "FF")]
    public void A_press_after_an_odd_one_is_not_swallowed(string steps, string expected) =>
        Replay(steps).ShouldBe(expected);

    // On the S23 a press's key-down and key-up come before its only go-back.
    [Theory]
    [InlineData("d0 u60 g62 d3000 u3060 g3062 d6000 u6060 g6062", "FFF")]
    [InlineData("d0 u60 g62 g70 d3000 u3060 g3062", "FrF")]
    [InlineData("d0 u250 g252 d350 u410 g412", "FF")]
    [InlineData("d0 e500 e550 u800 g802 d900 u960 g962", "FF")]
    public void A_press_whose_key_up_comes_before_its_go_back_is_taken_once(string steps, string expected) =>
        Replay(steps).ShouldBe(expected);

    [Theory]
    [InlineData("d0 G5 g101 d5000 G5005 u5100 g5101", "FrFr")]
    [InlineData("d0 G5 d5000 u5060 g5062", "FF")]
    public void A_lost_key_up_does_not_swallow_the_next_press(string steps, string expected) =>
        Replay(steps).ShouldBe(expected);

    [Fact]
    public void An_old_key_down_without_a_go_back_does_not_make_a_later_press_act_twice() =>
        Replay("d0 g5000 d5013 u5014 g5014").ShouldBe("Fr");

    [Theory]
    [InlineData("g0 g10", "FF")]
    [InlineData("d0 u1 g2 g500 g510", "FFF")]
    public void A_go_back_without_a_key_is_a_gesture_press(string steps, string expected) =>
        Replay(steps).ShouldBe(expected);

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
