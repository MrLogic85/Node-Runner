using NodeRunner.Domain;

namespace NodeRunner.Mechanics;

/// <summary>
/// How a <see cref="StepperDef"/> steps (#1037), like a clock. It holds its joint locked at a notch,
/// one of <see cref="StepperDef.StepsPerTurn"/> evenly spaced angles counted from the built angle. At
/// its running frequency it unlocks and drives the joint to the neighbouring notch in Power's direction
/// with at most |Power| × Max strength: full torque, then braking just in time to arrive still. It has
/// no top speed. It locks the joint the moment it is close enough to the notch, or as soon as it has
/// passed it: an overshoot still counts one step. A release that times out between notches pulls
/// the joint with Max strength to the nearest notch and locks it as it gets there; one cut short by the
/// next steps on from that nearest notch. Locked, the host welds the joint rigid at its notch
/// (<see cref="StepperState.Locked"/>); where it can't, <see cref="StepperState.Torque"/> holds it as
/// endless friction, as a Locked Brake's does. Its
/// angle is the one between its sides, as a <see cref="VelocityMotor"/>'s, and the brain sees it the
/// same way, as a sine and cosine.
/// </summary>
public static class Stepper
{
    /// <summary>How long the running frequency takes to follow the Frequency output, in seconds: a time constant.</summary>
    public const double FrequencyTimeConstant = 1;

    /// <summary>The longest a release lasts, in seconds, before it heads for the nearest notch.</summary>
    public const double MaxReleaseTime = 0.5;

    /// <summary>How fast it pulls an unlocked joint onto its notch: the wanted speed per radian off it, per second.</summary>
    public const double HoldApproachRate = 10;

    /// <summary>How close to a notch it locks the joint, as a share of the notch spacing.</summary>
    public const double LockDistance = 0.02;

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
    /// for both: the Stepper then only holds its notch. <paramref name="welded"/> is whether the host
    /// welds the joint while <see cref="StepperState.Locked"/>; if not, the Stepper holds it by torque.
    /// </summary>
    public static StepperState Next(
        StepperDef stepper,
        double angle,
        double speed,
        double frequencyOutput,
        double powerOutput,
        StepperState previous,
        double step,
        double effectiveInertia,
        bool welded = true)
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
        var locked = previous.Locked;
        var direction = previous.Direction;
        var releaseTime = previous.ReleaseTime + step;

        if (releasing && Passed(notch + direction, position, direction))
        {
            (notch, releasing, locked) = (notch + direction, false, true);
        }
        else if (releasing && Passed(notch - direction, position, -direction))
        {
            (notch, releasing, locked) = (notch - direction, false, true);
        }
        else if (releasing && (due || releaseTime > MaxReleaseTime - (step / 2)))
        {
            releasing = false;
        }

        // A release that ends short heads for the nearest notch at once, so the next one steps on from it.
        if (!releasing && !locked)
        {
            // It catches the joint on the step it crosses its notch, not a swing later.
            var before = position - (speed * step / NotchSpacing(stepper));
            notch = NearestNotch(position);
            locked = Math.Abs(position - notch) <= LockDistance || Math.Sign(position - notch) != Math.Sign(before - notch);
        }

        if (due)
        {
            (releasing, locked) = (true, false);
            direction = powerOutput < 0 ? -1 : 1;
            releaseTime = 0;
        }
        else if (!releasing)
        {
            releaseTime = 0;
        }

        // A release drives as fast as it can still stop by the notch, at most covering what is left in a step.
        var torque = releasing ? Approach(NotchAngle(notch + direction, stepper), angle, speed, Math.Abs(Math.Clamp(powerOutput, -1, 1)) * stepper.Strength, 1 / step, previous.Last, step, effectiveInertia)
            : locked && welded ? 0
            : locked ? Brake.Next(speed, double.PositiveInfinity, previous.Last, step, effectiveInertia).Torque
            : Approach(NotchAngle(notch, stepper), angle, speed, stepper.Strength, HoldApproachRate, previous.Last, step, effectiveInertia);
        return new StepperState(frequency, phase, notch, releasing, direction, releaseTime, new BrakeState(torque, speed), locked);
    }

    private static double NotchSpacing(StepperDef stepper) => 2 * Math.PI / stepper.StepsPerTurn;

    // Whether a joint turning the given way is close enough to the notch to lock it, or past it.
    private static bool Passed(int notch, double position, int way) => (position - notch) * way >= -LockDistance;

    // A joint carried more than half a notch off its notch has slipped on: it heads for the one it is nearer.
    private static int NearestNotch(double position) => (int)Math.Round(position, MidpointRounding.AwayFromZero);

    // It turns the joint toward the target at up to rate per radian off it, but no faster than what
    // strength has left over the load can stop within the distance left, braking step by step, so it
    // arrives without passing it.
    private static double Approach(double target, double angle, double speed, double strength, double rate, BrakeState? last, double step, double effectiveInertia)
    {
        var offset = target - angle;
        var toward = Math.Sign(offset);
        var load = Brake.Load(speed, last, step, effectiveInertia);

        // It never counts on a load to help it brake: on a joint heavier than it reads, that "load" is only the
        // inertia it missed, and it would brake too late.
        var slowing = Math.Max(strength - Math.Max(toward * load, 0), 0) / effectiveInertia * step;
        var stoppable = slowing > 0 ? slowing * (Math.Sqrt(0.25 + (2 * Math.Abs(offset) / (slowing * step))) - 0.5) : 0;
        var wantedSpeed = toward * Math.Min(rate * Math.Abs(offset), stoppable);
        return Brake.Toward(wantedSpeed, speed, strength, last, step, effectiveInertia).Torque;
    }
}

/// <summary>
/// What a <see cref="Stepper"/> carries between physics steps. <see cref="Frequency"/> is its running
/// frequency in steps a second, <see cref="Phase"/> how far it is to its next release (0…1),
/// <see cref="Notch"/> the notch it holds or last held, counted from the built angle, and, while it
/// <see cref="Releasing"/>, <see cref="Direction"/> the way it steps (±1) and <see cref="ReleaseTime"/>
/// how long it has pushed. <see cref="Last"/> is the torque it turned with and the speed it read, which
/// it reads the load from. <see cref="Locked"/> is whether it holds the joint locked at its notch. The
/// default state is a trial's start: at the built notch, still, and about to lock.
/// </summary>
public readonly record struct StepperState(
    double Frequency,
    double Phase,
    int Notch,
    bool Releasing,
    int Direction,
    double ReleaseTime,
    BrakeState? Last,
    bool Locked = false)
{
    /// <summary>
    /// The torque to turn the Target with this step, counter-clockwise positive: none while
    /// <see cref="Locked"/> and welded, and a Locked Brake's endless friction, holding the joint where it
    /// stopped, while locked on a joint the host can't weld.
    /// </summary>
    public double Torque => Last?.Torque ?? 0;
}
