using NodeRunner.Domain;

namespace NodeRunner.App.Services;

/// <summary>How a Shadows value is likely to run for one creature (#318).</summary>
public enum ShadowsLoad
{
    /// <summary>Should run smoothly.</summary>
    Smooth,

    /// <summary>May stutter: frames drop, but training keeps real time.</summary>
    Caution,

    /// <summary>Likely too many: training falls behind real time and runs in slow motion.</summary>
    TooMany,
}

/// <summary>
/// How many shadows a creature can race before training stutters, and before it runs in slow motion
/// (#318). Every shadow is a whole creature in the physics, so the cost grows with shadows times the
/// creature's <see cref="CreatureDef.PartCount"/>. The limits are set for a
/// <see cref="ReferenceParts"/>-part creature and scale inversely with the creature's parts. There is
/// no phone test yet (#620, 0.17), so the reference limits are a fixed, careful fallback; the phone
/// test will replace them with this phone's numbers.
/// </summary>
public sealed record ShadowsBudget
{
    // Measured on a Galaxy S23 on Flat (2026-10-05): a 29-part creature ran 100 shadows in real time
    // at about 35 fps, and a 47-part one fell to 0.8x speed at 100. A mid-range phone is about 2-3x
    // slower, so a 30-part creature gets 32 before stutter and 64 before slow motion.
    public const int ReferenceParts = 30;
    public const int ReferenceSmoothLimit = 32;
    public const int ReferenceSevereLimit = 64;

    private ShadowsBudget(int smoothLimit, int severeLimit)
    {
        SmoothLimit = smoothLimit;
        SevereLimit = severeLimit;
    }

    /// <summary>The most shadows that should run smoothly.</summary>
    public int SmoothLimit { get; }

    /// <summary>The most shadows before training is likely to run in slow motion.</summary>
    public int SevereLimit { get; }

    public static ShadowsBudget For(CreatureDef creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        var parts = Math.Max(creature.PartCount, 1);
        return new ShadowsBudget(Scaled(ReferenceSmoothLimit, parts), Scaled(ReferenceSevereLimit, parts));
    }

    public ShadowsLoad LoadOf(int shadows) =>
        shadows > SevereLimit ? ShadowsLoad.TooMany
        : shadows > SmoothLimit ? ShadowsLoad.Caution
        : ShadowsLoad.Smooth;

    private static int Scaled(int referenceLimit, int parts) =>
        Math.Clamp(referenceLimit * ReferenceParts / parts, TrainSettingsDef.MinShadows, TrainSettingsDef.MaxShadows);
}
