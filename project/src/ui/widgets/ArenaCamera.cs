using Godot;
using NodeRunner.App.ViewModels;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The Training arena's camera (#668): it follows a centre point along the ground, eased by
/// <see cref="ArenaFollow"/> and then by Godot's own position smoothing.
/// The two stages together keep a wobbling gait from shaking the view and make a change of target
/// glide in and settle. Only the horizontal position follows; the scene sets the height.
/// </summary>
public partial class ArenaCamera : Camera2D
{
    private readonly ArenaFollow _follow = new();
    private Func<Vector2>? _centre;

    /// <summary>
    /// Follows the point <paramref name="centre"/> returns, read every frame in world coordinates.
    /// The camera starts on it at once and glides after it as it moves; see <see cref="Retarget"/>
    /// and <see cref="Cut"/> for when it changes creature or trial.
    /// </summary>
    public void Follow(Func<Vector2> centre)
    {
        ArgumentNullException.ThrowIfNull(centre);
        _centre = centre;
        Cut();
    }

    /// <summary>Shows the followed point at once, dropping any glide: a new trial is a new scene.</summary>
    public void Cut()
    {
        if (_centre is null)
        {
            return;
        }

        _follow.SnapTo(_centre().X);
        Aim();
        ResetSmoothing();
    }

    /// <summary>
    /// Tells the camera the followed point now belongs to another creature, so it glides there
    /// without taking the jump for speed. Call it as soon as the point changes owner.
    /// </summary>
    public void Retarget()
    {
        if (_centre is not null)
        {
            _follow.Retarget(_centre().X);
        }
    }

    public override void _Ready()
    {
        PositionSmoothingEnabled = true;
        PositionSmoothingSpeed = (float)ArenaFollow.CameraSmoothingSpeed;
    }

    public override void _Process(double delta)
    {
        if (_centre is null)
        {
            return;
        }

        _follow.Step(_centre().X, delta);
        Aim();
    }

    private void Aim()
    {
        if (!_follow.HasFocus)
        {
            return;
        }

        var viewWidth = GetViewportRect().Size.X / Zoom.X;
        GlobalPosition = GlobalPosition with { X = (float)ArenaFollow.CameraX(_follow.AimX, viewWidth) };
    }
}
