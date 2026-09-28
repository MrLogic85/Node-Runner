using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>A component that clips its children to its rounded shape through <see cref="UiClip"/>.</summary>
internal interface IUiClipping
{
    void RefreshClip();
}

/// <summary>
/// Godot cannot nest <c>clip_children</c>: a clipping node inside a clipping ancestor draws
/// nothing. Components that clip to their rounded shape apply it here, so only the outermost
/// one does, and a change is passed down to the clipping components below it.
/// </summary>
internal static class UiClip
{
    public static void Apply(CanvasItem item, bool clip)
    {
        var mode = clip && !IsInsideClip(item)
            ? CanvasItem.ClipChildrenMode.AndDraw
            : CanvasItem.ClipChildrenMode.Disabled;
        if (item.ClipChildren == mode)
        {
            return;
        }

        item.ClipChildren = mode;
        RefreshBelow(item);
    }

    private static void RefreshBelow(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is CanvasItem { TopLevel: true })
            {
                continue;
            }

            if (child is IUiClipping clipping)
            {
                clipping.RefreshClip();
            }

            RefreshBelow(child);
        }
    }

    // A top-level item draws outside its ancestors, so their clipping does not reach it.
    private static bool IsInsideClip(CanvasItem item)
    {
        for (var node = item; !node.TopLevel && node.GetParent() is CanvasItem parent; node = parent)
        {
            if (parent.ClipChildren != CanvasItem.ClipChildrenMode.Disabled)
            {
                return true;
            }
        }

        return false;
    }
}
