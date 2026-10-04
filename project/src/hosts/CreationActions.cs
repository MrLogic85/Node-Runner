using Godot;
using NodeRunner.App.Repositories;
using NodeRunner.Managers;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Hosts;

/// <summary>What the scenes that change saved creations (Creations, Examples, Build) share.</summary>
internal static class CreationActions
{
    /// <summary>Runs a save-file change; a recoverable file error is logged and returns false.</summary>
    public static bool TryRunFileOperation(Action action, string description)
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

    /// <summary>Deletes a saved creation; a missing one counts as a failure.</summary>
    public static bool TryDelete(SaveManager saves, Guid id, string name) =>
        TryRunFileOperation(
            () =>
            {
                if (!saves.Delete(id))
                {
                    throw new FileNotFoundException($"Creation '{id}' was not found.");
                }
            },
            $"Deleting Creation '{name}'");

    /// <summary>The reference's Delete dialog: danger, press-and-hold, no Undo.</summary>
    public static UiDialogSpec DeleteDialog(string name, Func<bool> delete) =>
        new(
            UiPopupType.Danger,
            $"Delete {name}?",
            "The creation and its trained brain are removed for good. Copy it first if you might want it back.",
            "Hold to delete",
            () => Task.FromResult(delete()
                ? UiDialogResult.Success
                : UiDialogResult.Failure($"Could not delete {name}. Try again.")),
            holdToAction: true)
        {
            Icon = new(UiIconId.Trash),
        };

    /// <summary>The reference's "One reset" dialog (#687): losing training is danger and a hold.</summary>
    public static UiDialogSpec ResetTrainingDialog(Func<string> warning, Func<bool> reset) =>
        new(
            UiPopupType.Danger,
            "Reset training?",
            string.Empty,
            "Hold to reset",
            () => Task.FromResult(reset()
                ? UiDialogResult.Success
                : UiDialogResult.Failure("Could not reset training. Try again.")),
            holdToAction: true)
        {
            Icon = new(UiIconId.Restart),
            ContentSource = warning,
        };

    /// <summary>
    /// Unlocking keeps the training (#371): it only opens the body for this Build visit, so a plain
    /// confirm is enough, not a hold.
    /// </summary>
    public static UiDialogSpec UnlockDialog(string name, Action unlock) =>
        new(
            UiPopupType.Default,
            $"Unlock {name}?",
            "You can change the body until you leave Build. Training is kept: the brain remembers the parts you keep, and new parts start almost unused.",
            "Unlock",
            () =>
            {
                unlock();
                return Task.FromResult(UiDialogResult.Success);
            })
        {
            Icon = new(UiIconId.Unlock),
        };

    public static bool TryParseId(string key, string name, string action, out Guid id)
    {
        if (Guid.TryParse(key, out id))
        {
            return true;
        }

        GD.PrintErr($"Could not {action} '{name}': invalid id '{key}'.");
        return false;
    }
}
