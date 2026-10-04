using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// The app's notification queue, registered as the <see cref="AutoloadName"/> autoload so it
/// outlives scene changes: a notification raised just before a scene change still shows after it.
/// It draws on <see cref="UiLayers.Notification"/>, over screens and menus but under a dialog's
/// embedded Window, and keeps running while the tree is paused.
/// </summary>
public sealed partial class UiNotificationLayer : CanvasLayer
{
    public const string AutoloadName = "Notifications";

    private readonly UiNotification _notifications = new() { ProcessMode = ProcessModeEnum.Always };

    public override void _Ready()
    {
        Layer = UiLayers.Notification;
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
