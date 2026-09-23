using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Mouse-filter setup that lets Godot ScrollContainer own native drag scrolling.</summary>
internal static class UiNativeScroll
{
    public static void AllowGesturesToBubble(Control root)
    {
        Configure(root);
        foreach (var child in root.GetChildren().OfType<Control>())
        {
            AllowGesturesToBubble(child);
        }
    }

    private static void Configure(Control control)
    {
        switch (control)
        {
            case ScrollContainer:
                return;
            case Label or TextureRect or ColorRect:
                control.MouseFilter = Control.MouseFilterEnum.Ignore;
                return;
            case Container or Button or LineEdit:
                control.MouseFilter = Control.MouseFilterEnum.Pass;
                return;
        }
    }
}
