using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Managers;
using NodeRunner.Ui.Lib;
using NodeRunner.Ui.Widgets;

namespace NodeRunner.Hosts;

/// <summary>What the scenes that change saved creations share.</summary>
internal static class CreationActions
{
    /// <summary>Copies a creation; a failure shows the shared "Copy failed" notification and returns null.</summary>
    public static CreationDef? TryCopy(Node from, string name, Func<CreationDef> copy) =>
        TryAdd(from, name, copy, $"Copying Creation '{name}'", "Copy failed", UiIconId.Copy, $"creation.copy.failed.{name}",
            UiTextTranslation.Source(UiText.Format("Could not copy {0}. Try again.", name)));

    /// <summary>Adds a shared build; a failure shows "Import failed" and returns null.</summary>
    public static CreationDef? TryImport(Node from, string name, Func<CreationDef> import) =>
        TryAdd(from, name, import, $"Importing Creation '{name}'", "Import failed", UiIconId.Paste, $"creation.import.failed.{name}",
            UiTextTranslation.Source(UiText.Format("Could not add {0}. Try again.", name)));

    private static CreationDef? TryAdd(
        Node from, string name, Func<CreationDef> add, string description, string failedTitle, UiIconId icon, string id, Func<string>? failedText)
    {
        CreationDef? added = null;
        if (FileOperations.TryRun(() => added = add(), description) && added is not null)
        {
            return added;
        }

        UiNotificationLayer.Enqueue(from, new UiNotificationSpec(UiPopupType.Danger, failedTitle, string.Empty, Icon: new(icon))
        {
            Id = id,
            MessageSource = failedText,
        });
        return null;
    }

    /// <summary>Deletes a saved creation; a missing one counts as a failure.</summary>
    public static bool TryDelete(SaveManager saves, Guid id, string name) =>
        FileOperations.TryRun(
            () =>
            {
                if (!saves.Delete(id))
                {
                    throw new FileNotFoundException($"Creation '{id}' was not found.");
                }
            },
            $"Deleting Creation '{name}'");

    /// <summary>The reference's Delete dialog: danger, no Undo, confirmed with a tap.</summary>
    public static UiDialogSpec DeleteDialog(string name, Func<bool> delete) =>
        new(
            UiPopupType.Danger,
            string.Empty,
            "The creation and its trained network are removed for good. Copy it first if you might want it back.",
            "Delete",
            () => Task.FromResult(delete()
                ? UiDialogResult.Success
                : UiDialogResult.Failure(UiTextTranslation.Source(UiText.Format("Could not delete {0}. Try again.", name)))))
        {
            Icon = new(UiIconId.Trash),
            TitleSource = UiTextTranslation.Source(UiText.Format("Delete {0}?", name)),
        };

    /// <summary>The reference's "One reset" dialog: losing training is danger, confirmed with a tap.</summary>
    public static UiDialogSpec ResetTrainingDialog(Func<string> warning, Func<bool> reset) =>
        new(
            UiPopupType.Danger,
            "Reset training?",
            string.Empty,
            "Reset",
            () => Task.FromResult(reset()
                ? UiDialogResult.Success
                : UiDialogResult.Failure("Could not reset training. Try again.")))
        {
            Icon = new(UiIconId.Restart),
            ContentSource = warning,
        };

    /// <summary>
    /// Unlocking keeps the training: it only opens the body for this Build visit, so a plain
    /// confirm is enough.
    /// </summary>
    public static UiDialogSpec UnlockDialog(string name, Action unlock) =>
        new(
            UiPopupType.Default,
            string.Empty,
            "The creation is locked so you do not change the size or values of its network by accident. Changes to how its parts behave are still allowed. Unlock to use every tool that changes the network. Adding or removing a part keeps the training, but adds or removes that part's inputs and outputs in the network. Removing a part and adding the same kind back does not bring its training back; use Undo instead.",
            "Unlock",
            () =>
            {
                unlock();
                return Task.FromResult(UiDialogResult.Success);
            })
        {
            Icon = new(UiIconId.Unlock),
            TitleSource = UiTextTranslation.Source(UiText.Format("Unlock {0}?", name)),
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
