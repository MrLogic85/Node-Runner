namespace NodeRunner.Domain;

public sealed record ProgressionDef
{
    public ProgressionDef(bool extraCoreUnlocked = false, int? extraCoreUnlockedAtGeneration = null)
    {
        if (extraCoreUnlocked && extraCoreUnlockedAtGeneration is null)
        {
            throw new ArgumentException("An unlocked extra core needs a generation.", nameof(extraCoreUnlockedAtGeneration));
        }

        if (extraCoreUnlockedAtGeneration is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(extraCoreUnlockedAtGeneration));
        }

        ExtraCoreUnlocked = extraCoreUnlocked;
        ExtraCoreUnlockedAtGeneration = extraCoreUnlockedAtGeneration;
    }

    public bool ExtraCoreUnlocked { get; }

    public int? ExtraCoreUnlockedAtGeneration { get; }
}
