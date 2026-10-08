using System.Globalization;
using System.Text.RegularExpressions;
using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Copies text and confirms it with a notification. Android 13 (SDK 33) and later confirm every
/// copy with their own toast at the bottom for about two seconds, so there the notification
/// follows that toast instead of showing under it (#899).
/// </summary>
public static partial class UiClipboard
{
    private const int _firstSdkWithCopyToast = 33;
    private const double _systemToastSeconds = 2.0;

    public static void Copy(Node from, string text, UiNotificationSpec notice)
    {
        ArgumentNullException.ThrowIfNull(from);
        DisplayServer.ClipboardSet(text);
        if (!SystemConfirmsCopy(OS.GetName(), OS.GetVersionAlias()))
        {
            UiNotificationLayer.Enqueue(from, notice);
            return;
        }

        // A tree timer from the root, so leaving the screen meanwhile does not drop the notification.
        var root = from.GetTree().Root;
        root.GetTree().CreateTimer(_systemToastSeconds).Timeout += () => UiNotificationLayer.Enqueue(root, notice);
    }

    /// <summary>
    /// Whether the system shows its own toast for a copy: Android at SDK 33 or later. Godot's
    /// Android version alias reads "14 (SDK 34 build …)".
    /// </summary>
    public static bool SystemConfirmsCopy(string osName, string versionAlias) =>
        osName == "Android"
        && SdkPattern().Match(versionAlias) is { Success: true } match
        && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var sdk)
        && sdk >= _firstSdkWithCopyToast;

    [GeneratedRegex(@"\(SDK (\d+)")]
    private static partial Regex SdkPattern();
}
