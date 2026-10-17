using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Copies text and confirms it with a notification at once. Android 13 and later also show
/// their own toast for the copy; ours does not wait for it.
/// </summary>
public static class UiClipboard
{
    public static void Copy(Node from, string text, UiNotificationSpec notice)
    {
        ArgumentNullException.ThrowIfNull(from);
        DisplayServer.ClipboardSet(text);
        UiNotificationLayer.Enqueue(from, notice);
    }
}
