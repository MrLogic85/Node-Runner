using NodeRunner.Domain;

namespace NodeRunner.App.Lifecycle;

/// <summary>
/// A Creation is locked once it has trained at least one generation (#369). The lock protects the
/// trained model: anatomy and brain shape stay as that model needs them until the player unlocks,
/// which resets the training. It follows from the saved training and is never stored.
/// </summary>
public static class CreationLock
{
    public static bool IsLocked(CreationDef creation)
    {
        ArgumentNullException.ThrowIfNull(creation);
        return creation.Training is { Generation: > 0 };
    }
}
