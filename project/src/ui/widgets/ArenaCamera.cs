using Godot;
using NodeRunner.App.ViewModels;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The Training arena's camera (#668, #675): it frames the followed creature through
/// <see cref="ArenaFraming"/>, which follows its centre sideways, zooms out to fit it and its
/// speed, and keeps the ground at the same height on screen until the creature rises above the
/// top margin. The framing does all the smoothing, so the camera's own is off: a gait's wobble
/// never shakes the view and a change of target glides in and settles.
/// </summary>
public partial class ArenaCamera : Camera2D
{
    private readonly ArenaFraming _framing = new();
    private Func<FramedCreature>? _creature;
    private Vector2 _framedView;

    /// <summary>The Y of the ground's top, in world coordinates; it stays at the same height on screen.</summary>
    public double GroundY { get; set; }

    /// <summary>
    /// Frames the creature <paramref name="creature"/> describes, read every frame in world
    /// coordinates. The camera starts on it at once and glides after it as it moves; see
    /// <see cref="Retarget"/> and <see cref="Cut"/> for when it changes creature or trial.
    /// </summary>
    public void Follow(Func<FramedCreature> creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        _creature = creature;
        Cut();
    }

    /// <summary>Frames the followed creature at once, dropping any glide: a new trial is a new scene.</summary>
    public void Cut()
    {
        if (_creature is null)
        {
            return;
        }

        _framedView = GetViewportRect().Size;
        _framing.Cut(_creature(), GroundY, _framedView.X, _framedView.Y);
        Apply();
    }

    /// <summary>
    /// Tells the camera the followed creature is now another, so it glides there without taking
    /// the jump for speed. Call it as soon as the creature changes.
    /// </summary>
    public void Retarget()
    {
        if (_creature is not null)
        {
            _framing.Retarget(_creature());
        }
    }

    public override void _Ready() => PositionSmoothingEnabled = false;

    public override void _Process(double delta)
    {
        if (_creature is null)
        {
            return;
        }

        // A view that changes size, as when the arena is first laid out, is framed anew rather than
        // glided into from a framing made for another size.
        if (GetViewportRect().Size != _framedView)
        {
            Cut();
            return;
        }

        // The host runs while paused, but a paused scene keeps its framing: nothing moves to follow.
        if (GetTree().Paused)
        {
            return;
        }

        _framing.Step(_creature(), GroundY, _framedView.X, _framedView.Y, delta);
        Apply();
    }

    private void Apply()
    {
        if (!_framing.HasFrame)
        {
            return;
        }

        var zoom = (float)_framing.Zoom;
        Zoom = new Vector2(zoom, zoom);
        GlobalPosition = new Vector2((float)_framing.CameraX, (float)_framing.CameraY);
    }
}
