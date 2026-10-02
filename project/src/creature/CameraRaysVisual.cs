using Godot;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// The creature's camera rays in Training (#623): every ray that hits the ground, drawn up to the
/// hit with a ring there, whether or not the camera is selected. The creature adds it after its
/// joints, so tree order keeps the rays on top.
/// </summary>
public partial class CameraRaysVisual : Node2D
{
    public required VisualTheme Theme { get; init; }

    public required IReadOnlyList<CameraSensor> Cameras { get; init; }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        foreach (var camera in Cameras)
        {
            SensorDrawing.DrawRayHits(this, Transform2D.Identity, Theme, ToLocal(camera.GlobalOrigin), camera.GlobalHits.Select(ToLocal));
        }
    }
}
