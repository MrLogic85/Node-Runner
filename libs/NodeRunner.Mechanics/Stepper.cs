using NodeRunner.Domain;

namespace NodeRunner.Mechanics;

/// <summary>
/// How a <see cref="StepperDef"/> steps (#1037). It holds its joint at a notch, one of
/// <see cref="StepperDef.StepsPerTurn"/> evenly spaced angles counted from the built angle. At its
/// running frequency it lets go: it pushes with |Power| × Max strength toward the neighbouring notch in
/// Power's direction and catches the first notch it reaches. The hold then stops the turning and pulls
/// the joint onto the notch, all within Hold strength; a joint too fast or a load too strong for that
/// slips on and catches the notch nearest where it stops. Its angle is the one between its sides, as a
/// <see cref="VelocityMotor"/>'s, and the brain sees it the same way, as a sine and cosine.
/// </summary>
public static class Stepper
{
    /// <summary>How long the running frequency takes to follow the Frequency output, in seconds: a time constant.</summary>
    public const double FrequencyTimeConstant = 1;

    /// <summary>The longest a release lasts, in seconds, before it catches the nearest notch.</summary>
    public const double MaxReleaseTime = 0.5;

    /// <summary>How fast the hold pulls the joint onto its notch: the wanted speed per radian off it, per second.</summary>
    public const double HoldApproachRate = 10;

    /// <summary>The angle inputs: the sine and cosine of the angle between the sides, as a Velocity motor's.</summary>
    public static (double Sin, double Cos) AngleInputs(double angle) => VelocityMotor.AngleInputs(angle);

    /// <summary>The frequency input: the running frequency as a share of Max frequency, 0…1.</summary>
    public static double FrequencyInput(StepperState state, StepperDef stepper)
    {
        ArgumentNullException.ThrowIfNull(stepper);
        return state.Frequency / stepper.MaxFrequency;
    }

    /// <summary>The angle of notch <paramref name="notch"/> from the built angle, in radians.</summary>
    public static double NotchAngle(int notch, StepperDef stepper)
    {
        ArgumentNullException.ThrowIfNull(stepper);
        return notch * NotchSpacing(stepper);
    }

    /// <summary>
    /// The Stepper's next state from its last. <paramref name="angle"/> is the angle between its sides
    /// from the built angle, counted on across whole turns; <paramref name="speed"/> its turning speed.
    /// <paramref name="frequencyOutput"/> is the brain's Frequency output (0…1 of Max frequency) and
    /// <paramref name="powerOutput"/> its Power output (−1…1 of Max strength). With no brain, pass 0
    /// for both: the Stepper then only holds its notch.
    /// </summary>
    public static StepperState Next(
        StepperDef stepper,
        double angle,
        double speed,
        double frequencyOutput,
        double powerOutput,
        StepperState previous,
        double step,
        double effectiveInertia)
    {
        ArgumentNullException.ThrowIfNull(stepper);
        MotorLoop.RequirePositive(step, nameof(step), "Step");
        var position = angle / NotchSpacing(stepper);

        var wantedFrequency = Math.Clamp(frequencyOutput, 0, 1) * stepper.MaxFrequency;
        var frequency = previous.Frequency + ((wantedFrequency - previous.Frequency) * (1 - Math.Exp(-step / FrequencyTimeConstant)));
        var phase = previous.Phase + (frequency * step);
        var due = phase >= 1;
        phase -= Math.Floor(phase);

        var notch = previous.Notch;
        var releasing = previous.Releasing;
        var direction = previous.Direction;
        var releaseTime = previous.ReleaseTime + step;

        if (releasing)
        {
            // It catches a notch the tick it would pass it, so a fast joint isn't pushed a tick too far.
            var ahead = position + (speed * step / NotchSpacing(stepper));
            if (direction * (ahead - (notch + direction)) >= 0)
            {
                (notch, releasing) = (notch + direction, false);
            }
            else if (direction * (ahead - (notch - direction)) <= 0)
            {
                (notch, releasing) = (notch - direction, false);
            }
            else if (due || releaseTime > MaxReleaseTime - (step / 2))
            {
                (notch, releasing) = (NearestNotch(position), false);
            }
        }
        else
        {
            notch = NearestNotch(position);
        }

        if (due)
        {
            releasing = true;
            direction = powerOutput < 0 ? -1 : 1;
            releaseTime = 0;
        }
        else if (!releasing)
        {
            releaseTime = 0;
        }

        var torque = releasing
            ? direction * Math.Abs(Math.Clamp(powerOutput, -1, 1)) * stepper.Strength
            : Hold(notch, angle, speed, stepper, previous.Last, step, effectiveInertia);
        return new StepperState(frequency, phase, notch, releasing, direction, releaseTime, new BrakeState(torque, speed));
    }

    private static double NotchSpacing(StepperDef stepper) => 2 * Math.PI / stepper.StepsPerTurn;

    // A held joint pushed more than half a notch off its notch has slipped on: it holds the nearest notch,
    // so it catches the one it is heading for, and a joint still slipping keeps catching the next.
    private static int NearestNotch(double position) => (int)Math.Round(position, MidpointRounding.AwayFromZero);

    // It pulls toward the notch no faster than what Hold strength has left over the load can stop
    // within the distance left, so near its limit it never lets the load run the joint past the notch.
    private static double Hold(int notch, double angle, double speed, StepperDef stepper, BrakeState? last, double step, double effectiveInertia)
    {
        var offset = NotchAngle(notch, stepper) - angle;
        var toward = Math.Sign(offset);
        var load = Brake.Load(speed, last, step, effectiveInertia);
        var spare = Math.Max(stepper.HoldStrength - (toward * load), 0) / effectiveInertia;
        var wantedSpeed = toward * Math.Min(HoldApproachRate * Math.Abs(offset), Math.Sqrt(2 * spare * Math.Abs(offset)));
        return Brake.Toward(wantedSpeed, speed, stepper.HoldStrength, last, step, effectiveInertia).Torque;
    }
}

/// <summary>
/// What a <see cref="Stepper"/> carries between physics ticks. <see cref="Frequency"/> is its running
/// frequency in steps a second, <see cref="Phase"/> how far it is to its next release (0…1),
/// <see cref="Notch"/> the notch it holds or last held, counted from the built angle, and, while it
/// <see cref="Releasing"/>, <see cref="Direction"/> the way it steps (±1) and <see cref="ReleaseTime"/>
/// how long it has pushed. <see cref="Last"/> is the torque it turned with and the speed it read, which
/// the hold reads the load from. The default state is a trial's start: at the built notch, still.
/// </summary>
public readonly record struct StepperState(
    double Frequency,
    double Phase,
    int Notch,
    bool Releasing,
    int Direction,
    double ReleaseTime,
    BrakeState? Last)
{
    /// <summary>The torque to turn the Target with this tick, counter-clockwise positive.</summary>
    public double Torque => Last?.Torque ?? 0;
}
