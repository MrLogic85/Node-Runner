namespace NodeRunner.Theme;

/// <summary>
/// What a <see cref="StepperPart"/> shows of its Stepper's motion (#1067), as plain state the creature
/// layer adapts from the mechanics. The default is Build's and Hold's look: the pawl seated.
/// </summary>
/// <param name="Lifted">Whether the pawl is lifted out of its notch to step on.</param>
/// <param name="Sweep">While <paramref name="Lifted"/>, the rotation from the pawl to the notch it steps to: the soft wedge.</param>
public readonly record struct StepperLook(bool Lifted, float Sweep)
{
    /// <summary>The pawl seated in its notch.</summary>
    public static StepperLook Hold => default;
}
