using Godot;
using NodeRunner.App.Repositories;

namespace NodeRunner.Hosts;

/// <summary>Save-file changes the hosts make, for creations and settings alike.</summary>
internal static class FileOperations
{
    /// <summary>Runs a save-file change; a recoverable file error is logged and returns false.</summary>
    public static bool TryRun(Action action, string description)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex) when (FilePersistenceExceptions.IsRecoverable(ex))
        {
            GD.PrintErr($"{description} failed: {ex}");
            return false;
        }
    }
}
