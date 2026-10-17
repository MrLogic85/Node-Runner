using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// Adapts a Stepper's mechanics state to the <see cref="StepperLook"/> its visual shows, so the
/// theme never reads physics. While it releases, the pawl is lifted, the wedge running from it to the
/// notch it steps to; otherwise, caught or pulled to its nearest notch, it shows as Hold.
/// </summary>
public static class StepperLooks
{
    /// <param name="releasing">Whether it is stepping on (<c>Mechanics.StepperState.Releasing</c>).</param>
    /// <param name="notch">The notch it holds or last held, counted counter-clockwise from the built angle.</param>
    /// <param name="direction">The way it steps while releasing, ±1, counter-clockwise positive.</param>
    /// <param name="angleFromBuilt">The angle between its sides from the built angle, counter-clockwise, counted on across turns.</param>
    /// <param name="stepsPerTurn">How many notches make a full turn.</param>
    public static StepperLook Next(bool releasing, int notch, int direction, double angleFromBuilt, int stepsPerTurn)
    {
        if (!releasing)
        {
            return StepperLook.Hold;
        }

        var target = notch + direction;
        return new StepperLook(Lifted: true, (float)(angleFromBuilt - (target * 2 * Math.PI / stepsPerTurn)));
    }
}
