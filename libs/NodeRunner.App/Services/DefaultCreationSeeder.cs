using NodeRunner.App.Repositories;

namespace NodeRunner.App.Services;

/// <summary>
/// Gives the player a copy of the Worm example on the app's first start, and never again: a saved
/// marker, not an empty list, says whether this is the first start.
/// </summary>
public sealed class DefaultCreationSeeder
{
    private readonly IExampleCopyWorkflow _examples;
    private readonly IProgressionRepository _progression;

    public DefaultCreationSeeder(IExampleCopyWorkflow examples, IProgressionRepository progression)
    {
        ArgumentNullException.ThrowIfNull(examples);
        ArgumentNullException.ThrowIfNull(progression);

        _examples = examples;
        _progression = progression;
    }

    public bool SeedIfNeeded()
    {
        var progression = _progression.Load();
        if (progression.DefaultCreationsSeeded)
        {
            return false;
        }

        _examples.Copy(CreationExamples.WormId);
        _progression.Save(progression with { DefaultCreationsSeeded = true });
        return true;
    }
}
