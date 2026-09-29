using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// A layout slot that shows a 2D world. The world is authored under the view's
/// <c>%WorldViewport</c>, a <see cref="SubViewport"/> with its own coordinates and camera, so the
/// layout never moves or scales a physics scene. Unlike a stretched
/// <see cref="SubViewportContainer"/>, it renders at the screen's pixel density rather than the
/// canvas size, so lines stay sharp on a phone. A press reports the world position under it.
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

        EmitSignal(SignalName.WorldPressed, WorldViewport.CanvasTransform.AffineInverse() * position);
        AcceptEvent();
    }

    // The world lays out in the slot's canvas units and renders at the pixels the slot covers.
    private void Fit()
    {
        var canvasSize = (Vector2I)Size.Round();
        if (canvasSize.X <= 0 || canvasSize.Y <= 0)
        {
            return;
        }

        var density = (GetViewport().GetFinalTransform().Scale * GetGlobalTransformWithCanvas().Scale).Abs();
        var viewport = WorldViewport;
        viewport.Size = (Vector2I)(Size * density).Ceil();
        viewport.Size2DOverride = canvasSize;
        viewport.Size2DOverrideStretch = true;
        QueueRedraw();
    }
}
