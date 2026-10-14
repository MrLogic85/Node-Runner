namespace NodeRunner.Theme;

/// <summary>
/// What a <see cref="StepperPart"/> shows of its Stepper's motion (#1067), as plain state the creature
/// layer adapts from the mechanics. The default is Build's and Hold's look: the pawl seated, nothing lit.
/// </summary>
/// <param name="Lifted">Whether the pawl is lifted out of its notch to step on.</param>
/// <param name="Sweep">While <paramref name="Lifted"/>, the rotation from the pawl to the notch it steps to: the soft wedge.</param>
/// <param name="LitNotch">The notch it steps to, or last stepped to while that fades, or null.</param>
/// <param name="LitStrength">How lit <paramref name="LitNotch"/> is, from 1 down to 0 as it fades.</param>
public readonly record struct StepperLook(bool Lifted, float Sweep, int? LitNotch, float LitStrength)
{
    /// <summary>The pawl seated in its notch, nothing lit.</summary>
    public static StepperLook Hold => default;
}
