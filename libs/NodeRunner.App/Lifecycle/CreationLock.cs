using NodeRunner.Domain;

namespace NodeRunner.App.Lifecycle;

/// <summary>
/// A Creation is locked once it has trained at least one generation (#369), which is when it has
/// saved training. The lock only prevents accidental changes to the body: unlocking
/// (<see cref="ViewModels.BuildViewModel.Unlock"/>, #371) keeps the training and lasts for one
/// Build visit. It follows from the saved training and is never stored.
/// </summary>
public static class CreationLock
{
    public static bool IsLocked(CreationDef creation)
    {
        ArgumentNullException.ThrowIfNull(creation);
        return creation.Training is not null;
    }
}
