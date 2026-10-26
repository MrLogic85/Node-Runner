using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// The app's palette, set as the root window's <see cref="Window.Theme"/> so every screen and
/// dialog inherits it. Registered as the <see cref="AutoloadName"/> autoload. With
/// <see cref="UiThemeChoice.Phone"/> it follows the phone's dark mode (Neon) or light mode
/// (Paper), also while the app runs.
/// </summary>
public sealed partial class UiAppTheme : Node
{
    public const string AutoloadName = "AppTheme";

    private UiThemeChoice _chosen = UiThemeChoice.Neon;

    // The palette set on the root window; null until the first one is.
    private UiTokenType? _current;

    public static UiAppTheme Of(Node from)
    {
        ArgumentNullException.ThrowIfNull(from);
        return from.GetNode<UiAppTheme>("/root/" + AutoloadName);
    }

    public override void _Ready()
    {
        DisplayServer.SetSystemThemeChangeCallback(Callable.From(Refresh));
        Refresh();
    }

    public override void _ExitTree() => DisplayServer.SetSystemThemeChangeCallback(default);

    // A phone may change its mode while the app is in the background, where the callback may not
    // reach it. The window may gain focus before the autoload is in the tree, which _Ready covers.
    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusIn && IsInsideTree())
        {
            Refresh();
        }
    }

    public void Choose(UiThemeChoice choice)
    {
        if (choice == _chosen)
        {
            return;
        }

        _chosen = choice;
        Refresh();
    }

    /// <summary>
    /// The palette <paramref name="choice"/> gives with the phone in dark mode or not.
    /// <paramref name="phoneDark"/> is null where the phone has no dark or light mode; Phone then gives Neon.
    /// </summary>
    public static UiTokenType Resolve(UiThemeChoice choice, bool? phoneDark) => choice switch
    {
        UiThemeChoice.Neon => UiTokenType.Neon,
        UiThemeChoice.Paper => UiTokenType.Paper,
        UiThemeChoice.Phone => phoneDark == false ? UiTokenType.Paper : UiTokenType.Neon,
        _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, null),
    };

    private void Refresh()
    {
        bool? phoneDark = DisplayServer.IsDarkModeSupported() ? DisplayServer.IsDarkMode() : null;
        var current = Resolve(_chosen, phoneDark);
        if (current == _current)
        {
            return;
        }

        _current = current;
        GetTree().Root.Theme = UiThemes.For(current);
    }
}
