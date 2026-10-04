using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// A CanvasLayer on one of the named <see cref="UiLayers"/> levels (#768), <see cref="UiLayers.Overlay"/>
/// unless set, in the Viewport or Window it is in. It lifts its controls, such as an open
/// <see cref="UiMenu"/>, over the screen or dialog that opened them without a ZIndex. It stays in
/// its opener's subtree, so its controls keep the opener's lifetime, scene and unique names.
/// A CanvasLayer cuts Theme and visibility inheritance, so the layer hands its controls the
/// opener's theme (they take none of their own) and hides when its opener does.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiLevelLayer : CanvasLayer
{
    private Control? _opener;

    public UiLevelLayer()
    {
        Layer = UiLayers.Overlay;
    }

    // In the editor the project theme applies, and a theme or visibility written here would be saved.
    public override void _EnterTree()
    {
        if (Engine.IsEditorHint())
        {
            return;
        }

        _opener = GetParent() as Control;
        _opener?.ThemeChanged += PassTheme;
        _opener?.VisibilityChanged += FollowVisibility;
        ChildEnteredTree += OnChildEnteredTree;
        PassTheme();
        FollowVisibility();
    }

    public override void _ExitTree()
    {
        if (Engine.IsEditorHint())
        {
            return;
        }

        _opener?.ThemeChanged -= PassTheme;
        _opener?.VisibilityChanged -= FollowVisibility;
        _opener = null;
        ChildEnteredTree -= OnChildEnteredTree;
    }

    private void OnChildEnteredTree(Node child)
    {
        if (child is Control control)
        {
            control.Theme = OpenerTheme();
        }
    }

    // A menu left showing over a hidden opener would also keep Back and outside taps.
    private void FollowVisibility() => Visible = _opener?.IsVisibleInTree() ?? true;

    private void PassTheme()
    {
        var theme = OpenerTheme();
        foreach (var control in GetChildren().OfType<Control>())
        {
            control.Theme = theme;
        }
    }

    // The nearest Control or Window with a theme, stopping at any other node. Godot would fall
    // through to outer themes for an item the nearest one lacks; a menu's opener theme is always a
    // full palette (the app's or a gallery's), so the nearest one is enough. None means the
    // project theme, which applies anyway.
    private Godot.Theme? OpenerTheme()
    {
        for (var node = GetParent(); node is Control or Window; node = node.GetParent())
        {
            var theme = node is Control control ? control.Theme : ((Window)node).Theme;
            if (theme is not null)
            {
                return theme;
            }
        }

        return null;
    }
}
