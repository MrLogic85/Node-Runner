using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Normalizes touch and mouse pointer events into press/drag/release
/// positions, so screens and widgets do not each re-implement the
/// touch-vs-mouse branching. App-agnostic: only Godot input/primitive types.
/// </summary>
public static class PointerInput
{
    public static bool TryGetPressPosition(InputEvent inputEvent, out Vector2 position)
    {
        switch (inputEvent)
        {
            case InputEventScreenTouch { Pressed: true } touch:
                position = touch.Position;
                return true;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouseButton:
                position = mouseButton.Position;
                return true;
            default:
                position = Vector2.Zero;
                return false;
        }
    }

    public static bool TryGetReleasePosition(InputEvent inputEvent, out Vector2 position)
    {
        switch (inputEvent)
        {
            case InputEventScreenTouch { Pressed: false } touch:
                position = touch.Position;
                return true;
            case InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } mouseButton:
                position = mouseButton.Position;
                return true;
            default:
                position = Vector2.Zero;
                return false;
        }
    }

    public static bool TryGetDragPosition(InputEvent inputEvent, out Vector2 position)
    {
        switch (inputEvent)
        {
            case InputEventScreenDrag drag:
                position = drag.Position;
                return true;
            case InputEventMouseMotion { ButtonMask: MouseButtonMask.Left } mouseMotion:
                position = mouseMotion.Position;
                return true;
            default:
                position = Vector2.Zero;
                return false;
        }
    }
}
