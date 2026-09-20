using NodeRunner.App.Repositories;
using NodeRunner.Domain;

namespace NodeRunner.App.Services;

public sealed class DefaultCreationSeeder
{
    private readonly ICreationRepository _creations;
    private readonly IProgressionRepository _progression;

    public DefaultCreationSeeder(ICreationRepository creations, IProgressionRepository progression)
    {
        ArgumentNullException.ThrowIfNull(creations);
        ArgumentNullException.ThrowIfNull(progression);

        _creations = creations;
        _progression = progression;
    }

    public bool SeedIfNeeded()
    {
        var progression = _progression.Load();
        if (progression.DefaultCreationsSeeded)
        {
            return false;
        }

        if (_creations.List().Count > 0)
        {
            MarkSeeded(progression);
            return false;
        }

        _creations.Save(DefaultCreationTemplates.CreateStarterWorm());
        MarkSeeded(progression);
        return true;
    }

    private void MarkSeeded(ProgressionDef progression)
    {
        _progression.Save(new ProgressionDef(
            progression.ExtraCoreUnlocked,
            progression.ExtraCoreUnlockedAtGeneration,
            progression.ExtraCoreUnlockedByCreationId,
            defaultCreationsSeeded: true));
    }
}
