using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Keeps properties a component derives in code out of saved scenes (#309).
/// </summary>
/// <remarks>
/// Godot saves every theme override and custom minimum size a component writes, and
/// <c>_ValidateProperty</c> never sees the dynamic <c>theme_override_*</c> properties. Instead,
/// the editor notifies every node right before and after it packs a scene: the component clears
/// its derived properties for the pack, gets them back straight after, and then re-runs its
/// refresh: a parent clears state on child components before they see the notification, so a
/// restored snapshot alone can be stale. Call <see cref="Handle"/> first in <c>_Notification</c>
/// and return when it returns true, so the theme refresh that clearing an override triggers does
/// not write the values back mid-save.
/// </remarks>
public sealed class UiUnsavedState
{
    private readonly (NodePath Node, StringName Property)[] _entries;
    private readonly List<(Node Node, StringName Property, Variant Value)> _held = [];
    private bool _holding;

    /// <summary>Derived properties on the component itself.</summary>
    public UiUnsavedState(params StringName[] properties)
        : this(properties.Select(property => (new NodePath("."), property)))
    {
    }

    /// <summary>Derived properties on the component or on nodes of its scene, by path.</summary>
    public UiUnsavedState(IEnumerable<(NodePath Node, StringName Property)> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = [.. entries];
    }

    /// <summary>All theme stylebox overrides a Button draws its states with.</summary>
    public static StringName[] ButtonStyles { get; } =
    [
        "theme_override_styles/normal",
        "theme_override_styles/hover",
        "theme_override_styles/pressed",
        "theme_override_styles/focus",
        "theme_override_styles/disabled",
    ];

    /// <summary>
    /// Clears the derived properties on editor pre-save; restores them and queues
    /// <paramref name="refresh"/> on post-save. Returns true while the notification belongs to
    /// the save and must not refresh anything.
    /// </summary>
    public bool Handle(Node owner, int what, Action? refresh = null)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (what == Node.NotificationEditorPreSave)
        {
            Hold(owner);

            // The editor skips post-save when packing fails; nothing between the two notifications
            // flushes deferred calls, so this only releases a hold whose post-save never came.
            Callable.From(() => Release(owner, refresh)).CallDeferred();
            return true;
        }

        if (what == Node.NotificationEditorPostSave)
        {
            Release(owner, refresh);
            return true;
        }

        return _holding;
    }

    private void Release(Node owner, Action? refresh)
    {
        if (!_holding)
        {
            return;
        }

        Restore();
        if (refresh is not null)
        {
            // Deferred: the editor blocks child changes while it propagates the notification.
            // A scene saved outside the tree restyles on its theme change when it re-enters.
            Callable.From(() =>
            {
                if (GodotObject.IsInstanceValid(owner) && owner.IsInsideTree())
                {
                    refresh();
                }
            }).CallDeferred();
        }
    }

    private void Hold(Node owner)
    {
        Restore();
        _holding = true;
        foreach (var (path, property) in _entries)
        {
            var node = owner.GetNodeOrNull(path);
            if (node is null)
            {
                continue;
            }

            _held.Add((node, property, node.Get(property)));
            node.Set(property, DefaultValue(node, property));
        }
    }

    // A saved scene omits a property that equals its default, so that is the value to pack.
    private static Variant DefaultValue(Node node, StringName property)
    {
        if (node.GetScript().Obj is Script script)
        {
            var value = script.GetPropertyDefaultValue(property);
            if (value.VariantType != Variant.Type.Nil)
            {
                return value;
            }
        }

        return ClassDB.ClassGetPropertyDefaultValue(node.GetClass(), property);
    }

    private void Restore()
    {
        foreach (var (node, property, value) in _held)
        {
            if (GodotObject.IsInstanceValid(node))
            {
                node.Set(property, value);
            }
        }

        _held.Clear();
        _holding = false;
    }
}
