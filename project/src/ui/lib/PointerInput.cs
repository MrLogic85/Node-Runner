using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Normalizes native pointer events into press/drag/release positions.
/// </summary>
public static class PointerInput
{
    public static bool TryGetPressPosition(InputEvent inputEvent, out Vector2 position)
    {
        switch (inputEvent)
        {
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
            case InputEventMouseMotion { ButtonMask: MouseButtonMask.Left } mouseMotion:
                position = mouseMotion.Position;
                return true;
            default:
                position = Vector2.Zero;
                return false;
        }
    }
}
