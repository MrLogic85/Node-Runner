using Godot;

namespace NodeRunner.Ui.Screens;

public static class UiGalleryScroll
{
    public const float TouchDeadzone = 8f;

    public static int ApplyVerticalDrag(int currentScroll, float relativeY) =>
        currentScroll - Mathf.RoundToInt(relativeY);
}
