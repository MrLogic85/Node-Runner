using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Runs a theme-change refresh at most once per node at a time.
/// </summary>
/// <remarks>
/// Applying a theme override to a node re-emits <c>NotificationThemeChanged</c> on that same
/// node. A handler that refreshes itself by writing overrides therefore re-enters itself and
/// recurses until the stack overflows, which on Android surfaces as a silent freeze rather than
/// a crash. Every <c>NotificationThemeChanged</c> handler that writes its own overrides must run
/// through this guard.
/// </remarks>
public static class UiThemeRefresh
{
    private static readonly HashSet<ulong> _running = [];

    public static void Guarded(GodotObject node, Action refresh)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(refresh);

        var id = node.GetInstanceId();
        if (!_running.Add(id))
        {
            return;
        }

        try
        {
            refresh();
        }
        finally
        {
            _running.Remove(id);
        }
    }
}
