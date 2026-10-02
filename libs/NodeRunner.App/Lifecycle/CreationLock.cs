using NodeRunner.Domain;

namespace NodeRunner.App.Lifecycle;

/// <summary>
/// A Creation is locked once it has trained at least one generation (#369), which is when it has
/// saved training. The lock protects the
/// trained model: the anatomy stays as that model needs it until the player unlocks,
/// which resets the training. It follows from the saved training and is never stored.
/// </summary>
public static class CreationLock
{
    public static bool IsLocked(CreationDef creation)
    {
        ArgumentNullException.ThrowIfNull(creation);
        return creation.Training is not null;
    }
}
