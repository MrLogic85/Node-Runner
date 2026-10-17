using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// A component's own mouse filter as a default that a scene's <c>mouse_filter</c> overrides.
/// Godot applies a scene's native properties before its <c>script</c>, so a C# constructor runs after
/// the scene's value, and <c>_Ready</c> after all of them: assigning <c>MouseFilter</c> there would
/// silently undo the scene. A scene stores only a value that differs from the native class's default,
/// so a control still at that default has none of its own. The flip side: a scene cannot pick the
/// native default (Stop on a plain Control or a BaseButton, Pass on a Container) for a component
/// that prefers another; that needs code.
/// </summary>
public static class UiMouseFilter
{
    public static void Prefer(Control control, Control.MouseFilterEnum filter)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (control.MouseFilter == NativeDefault(control.GetClass()))
        {
            control.MouseFilter = filter;
        }
    }

    // A virtual class such as BaseButton has no defaults of its own; its nearest instantiable base does.
    private static Control.MouseFilterEnum NativeDefault(StringName type)
    {
        var value = ClassDB.ClassGetPropertyDefaultValue(type, Control.PropertyName.MouseFilter);
        return value.VariantType == Variant.Type.Nil
            ? NativeDefault(ClassDB.GetParentClass(type))
            : (Control.MouseFilterEnum)value.AsInt32();
    }
}
