using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// A layout slot that shows a 2D world. The world is authored under the view's
/// <c>%WorldViewport</c>, a <see cref="SubViewport"/> with its own coordinates and camera, so the
/// layout never moves or scales a physics scene. Unlike a stretched
/// <see cref="SubViewportContainer"/>, it renders at the screen's pixel density rather than the
/// canvas size, so lines stay sharp on a phone. A press reports the world position under it.
/// The world keeps its size on screen whatever the UI size (<see cref="UiScale"/>): the slot shows
/// more or less of it as the UI around it shrinks or grows.
/// </summary>
public partial class UiWorldView : Control
{
    [Signal]
    public delegate void WorldPressedEventHandler(Vector2 worldPosition);

    private SubViewport WorldViewport => GetNode<SubViewport>("%WorldViewport");

    public override void _Ready()
    {
        Resized += Fit;
        GetViewport().SizeChanged += Fit;
        Fit();
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= Fit;
    }

    public override void _Draw() =>
        DrawTextureRect(WorldViewport.GetTexture(), new Rect2(Vector2.Zero, Size), tile: false);

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (!PointerInput.TryGetPressPosition(inputEvent, out var position))
        {
            return;
        }

        var toWorld = (Vector2)WorldViewport.Size2DOverride / Size;
        EmitSignal(SignalName.WorldPressed, WorldViewport.CanvasTransform.AffineInverse() * (position * toWorld));
        AcceptEvent();
    }

    /// <summary>Where <paramref name="worldPosition"/> shows in this view's coordinates.</summary>
    public Vector2 FromWorld(Vector2 worldPosition) =>
        WorldViewport.CanvasTransform * worldPosition * (Size / WorldViewport.Size2DOverride);

    // The world lays out in canvas units without the UI size's root factor, which undoes the UI
    // size for it alone, and renders at the pixels the slot covers.
    private void Fit()
    {
        var worldSize = (Vector2I)(Size * UiScale.FactorOf(this)).Round();
        if (worldSize.X <= 0 || worldSize.Y <= 0)
        {
            return;
        }

        var density = (GetViewport().GetFinalTransform().Scale * GetGlobalTransformWithCanvas().Scale).Abs();
        var viewport = WorldViewport;
        viewport.Size = (Vector2I)(Size * density).Ceil();
        viewport.Size2DOverride = worldSize;
        viewport.Size2DOverrideStretch = true;
        QueueRedraw();
    }
}
