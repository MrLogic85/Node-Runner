using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// A layout slot that shows a 2D world. The world is authored under the view's
/// <c>%WorldViewport</c>, a <see cref="SubViewport"/> with its own coordinates and camera, so the
/// layout never moves or scales a physics scene, and the world's draw layers (<c>ZIndex</c>) order
/// only the world, never the controls over it (#769). Unlike a stretched
/// <see cref="SubViewportContainer"/>, it renders at the screen's pixel density rather than the
/// canvas size, so lines stay sharp on a phone. Like one, it hands the world every pointer event
/// over it, in the world's coordinates, and a press also reports the world position under it.
/// The world's background is transparent, so the slot shows through where the world draws nothing.
/// By default the world keeps its size on screen whatever the UI size (<see cref="UiScale"/>): the
/// slot shows more or less of it as the UI around it shrinks or grows.
/// </summary>
public partial class UiWorldView : Control
{
    [Signal]
    public delegate void WorldPressedEventHandler(Vector2 worldPosition);

    /// <summary>The world was laid out again for a new size or pixel density.</summary>
    [Signal]
    public delegate void FittedEventHandler();

    /// <summary>
    /// Whether the world's units are the UI's, so the world grows and shrinks with the UI size like
    /// the controls over it. Build sets it: its view zoom makes up for the UI size itself
    /// (<c>CanvasView.UiScale</c>), and its handles and notes are controls in the UI's units.
    /// </summary>
    [Export]
    public bool ScalesWithUi { get; set; }

    private SubViewport WorldViewport => GetNode<SubViewport>("%WorldViewport");

    /// <summary>The map from world coordinates to this view's.</summary>
    public Transform2D LocalFromWorld
    {
        get
        {
            var viewport = WorldViewport;
            var worldSize = (Vector2)viewport.Size2DOverride;
            return worldSize.X <= 0 || worldSize.Y <= 0
                ? viewport.CanvasTransform
                : new Transform2D(0, Size / worldSize, 0, Vector2.Zero) * viewport.CanvasTransform;
        }
    }

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
        if (inputEvent is not (InputEventMouse or InputEventScreenTouch or InputEventScreenDrag or InputEventGesture))
        {
            return;
        }

        if (PointerInput.TryGetPressPosition(inputEvent, out var position))
        {
            EmitSignal(SignalName.WorldPressed, LocalFromWorld.AffineInverse() * position);
        }

        // As a SubViewportContainer does: in the viewport's pixels, which it maps to world coordinates.
        var viewport = WorldViewport;
        viewport.PushInput(inputEvent.XformedBy(new Transform2D(0, (Vector2)viewport.Size / Size, 0, Vector2.Zero)));
        AcceptEvent();
    }

    /// <summary>
    /// Renders a still world once more, after its owner changed it. A still world is one whose
    /// <c>WorldViewport</c> is authored with <c>render_target_update_mode</c> Disabled, as a list
    /// of thumbnails does so scrolling moves finished pictures (#770); a live world ignores it.
    /// </summary>
    public void RequestRender()
    {
        var viewport = WorldViewport;
        if (viewport.RenderTargetUpdateMode is SubViewport.UpdateMode.Disabled or SubViewport.UpdateMode.Once)
        {
            viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
        }
    }

    /// <summary>Where <paramref name="worldPosition"/> shows in this view's coordinates.</summary>
    public Vector2 FromWorld(Vector2 worldPosition) => LocalFromWorld * worldPosition;

    // Unless the world scales with the UI, it lays out in canvas units without the UI size's root
    // factor, which undoes the UI size for it alone. Either way it renders at the pixels the slot covers.
    private void Fit()
    {
        var factor = ScalesWithUi ? 1 : UiScale.FactorOf(this);
        var worldSize = (Vector2I)(Size * factor).Round();
        if (worldSize.X <= 0 || worldSize.Y <= 0)
        {
            return;
        }

        var density = (GetViewport().GetFinalTransform().Scale * GetGlobalTransformWithCanvas().Scale).Abs();
        var viewport = WorldViewport;
        viewport.Size = (Vector2I)(Size * density).Ceil();
        viewport.Size2DOverride = worldSize;
        viewport.Size2DOverrideStretch = true;
        RequestRender();
        QueueRedraw();
        EmitSignal(SignalName.Fitted);
    }
}
