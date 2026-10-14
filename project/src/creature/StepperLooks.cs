using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// Adapts a Stepper's mechanics state to the <see cref="StepperLook"/> its visual shows (#1067), so the
/// theme never reads physics. While it releases, the pawl is lifted and its target notch lit, the wedge
/// running from the pawl to it. When it catches the lit notch (<c>Locked</c> on it) the pawl seats and the
/// notch fades over <see cref="FadeTime"/>. A release that ends short, pulled to its nearest notch or
/// caught back at the one it left, shows as Hold at once.
/// </summary>
public static class StepperLooks
{
    /// <summary>How long a caught notch takes to fade, in seconds.</summary>
    public const double FadeTime = 0.15;

    /// <param name="previous">The look shown last frame.</param>
    /// <param name="releasing">Whether it is stepping on (<c>Mechanics.StepperState.Releasing</c>).</param>
    /// <param name="locked">Whether it holds its notch locked (<c>Mechanics.StepperState.Locked</c>).</param>
    /// <param name="notch">The notch it holds or last held, counted counter-clockwise from the built angle.</param>
    /// <param name="direction">The way it steps while releasing, ±1, counter-clockwise positive.</param>
    /// <param name="angleFromBuilt">The angle between its sides from the built angle, counter-clockwise, counted on across turns.</param>
    /// <param name="stepsPerTurn">How many notches make a full turn.</param>
    /// <param name="delta">The time since the last frame, in seconds.</param>
    public static StepperLook Next(StepperLook previous, bool releasing, bool locked, int notch, int direction, double angleFromBuilt, int stepsPerTurn, double delta)
    {
        if (releasing)
        {
            var target = notch + direction;
            var sweep = angleFromBuilt - (target * 2 * Math.PI / stepsPerTurn);
            return new StepperLook(Lifted: true, (float)sweep, target, LitStrength: 1);
        }

        var caught = locked && previous.LitNotch == notch;
        var strength = caught ? previous.LitStrength - (delta / FadeTime) : 0;
        return strength > 0 ? new StepperLook(Lifted: false, 0, notch, (float)strength) : StepperLook.Hold;
    }
}
