using Godot;
using NodeRunner.App.ViewModels;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The Training arena's camera (#668): it frames the followed creature through
/// <see cref="ArenaFraming"/>, and while a part callout shows keeps a <see cref="Shelf"/> for it
/// (#1137). The framing does all the smoothing, so the camera's own is off. See
/// <c>docs/TRAINING_LOOP.md</c> → Camera.
/// </summary>
public partial class ArenaCamera : Camera2D
{
    private readonly ArenaFraming _framing = new();
    private Func<FramedCreature>? _creature;
    private Vector2 _framedView;

    /// <summary>The Y of the ground's top, in world coordinates; the framing never shows it above its line on screen.</summary>
    public double GroundY { get; set; }

    /// <summary>
    /// The share of the view's height to keep clear at the top for a part callout; 0 with none.
    /// The camera opens and gives back the room smoothly, also while paused.
    /// </summary>
    public double Shelf
    {
        get => _framing.Shelf;
        set
        {
            _framing.Shelf = value;
            Apply();
        }
    }

    /// <summary>
    /// The share of the view's height the distance scale takes along the bottom (#1170): making
    /// room for the <see cref="Shelf"/> lowers the ground no further than its top.
    /// </summary>
    public double ScaleBand
    {
        get => _framing.ScaleBand;
        set
        {
            _framing.ScaleBand = value;
            Apply();
        }
    }

    /// <summary>
    /// Frames the creature <paramref name="creature"/> describes, read every physics tick in world
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
        }
    }

    // The camera moves on physics ticks, as the creatures do (#909): stepped every rendered frame,
    // it moved alone on the frames between ticks of a screen faster than physics, and the world
    // juddered against it.
    public override void _PhysicsProcess(double delta)
    {
        if (_creature is null)
        {
            return;
        }

        // The host runs while paused, but a paused scene keeps its framing: only the shelf for a
        // callout read then opens or closes.
        if (GetTree().Paused)
        {
            _framing.StepShelf(_creature(), _framedView.X, _framedView.Y, delta);
        }
        else
        {
            _framing.Step(_creature(), GroundY, _framedView.X, _framedView.Y, delta);
        }

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
