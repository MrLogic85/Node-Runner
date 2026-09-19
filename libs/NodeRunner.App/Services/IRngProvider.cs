namespace NodeRunner.App.Services;

/// <summary>
/// The single seeded RNG source for the current simulation run. Simulation
/// and ML code should thread randomness through this instead of creating ad
/// hoc <see cref="Random"/> instances, so a run's outcome is reproducible
/// from its seed (see docs/CODE_DESIGN_PRINCIPLES.md's determinism rule).
/// </summary>
public interface IRngProvider
{
    /// <summary>The seed the current <see cref="Random"/> was created from.</summary>
    int Seed { get; }

    /// <summary>The current seeded RNG instance.</summary>
    Random Random { get; }

    /// <summary>Replaces the current RNG with a freshly seeded one.</summary>
    void Reseed(int seed);
}
