using Godot;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// The creature's camera rays in Training (#623): every ray that hits the ground, drawn up to the
/// hit with a ring there, whether or not the camera is selected, on the overlay layer over the
/// whole creature.
/// </summary>
public partial class CameraRaysVisual : Node2D, IShadowVisual
{
    public CameraRaysVisual()
    {
        ZIndex = CreatureLayers.Overlays;
    }

    /// <summary>Only the followed creature shows its camera rays.</summary>
    public static ShadowDrawing AsShadow => ShadowDrawing.Hidden;

    public bool IsShadow
    {
        get => !Visible;
        set
        {
            Visible = !value;
            SetProcess(!value);
        }
    }

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
