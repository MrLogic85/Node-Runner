using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// The app's notification queue, registered as the <see cref="AutoloadName"/> autoload so it
/// outlives scene changes: a notification raised just before a scene change still shows after it.
/// It draws above every screen's layers and keeps running while the tree is paused.
/// </summary>
public sealed partial class UiNotificationLayer : CanvasLayer
{
    public const string AutoloadName = "Notifications";

    /// <summary>Above every layer a screen uses; a dialog's embedded window still draws above it.</summary>
    private const int _layer = 50;

    private readonly UiNotification _notifications = new() { ProcessMode = ProcessModeEnum.Always };

    public override void _Ready()
    {
        Layer = _layer;
        ProcessMode = ProcessModeEnum.Always;
        AddChild(_notifications);
    }

    /// <summary>
    /// Queues a notification on the app's layer, found from any node in the tree. The queue outlives
    /// the calling scene, so an <c>OnClick</c> callback must not capture that scene.
    /// </summary>
    public static void Enqueue(Node from, UiNotificationSpec spec)
    {
        ArgumentNullException.ThrowIfNull(from);
        from.GetNode<UiNotificationLayer>("/root/" + AutoloadName)._notifications.Enqueue(spec);
    }
}
