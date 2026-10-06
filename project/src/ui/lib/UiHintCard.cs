using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// A card that comes and goes with a touch (#867): <see cref="ShowNow"/> fades it in, or keeps it
/// if it already shows; <see cref="HideAfterLinger"/> keeps it <see cref="LingerSeconds"/> and
/// then fades it out; <see cref="HideNow"/> drops it at once. It never takes input.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiHintCard : UiCard
{
    private const double _fadeInSeconds = 0.12;
    private const double _fadeOutSeconds = 0.2;

    private Tween? _tween;

    [Export]
    public double LingerSeconds { get; set; } = 2;

    public override void _Ready()
    {
        base._Ready();
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void ShowNow()
    {
        _tween?.Kill();
        if (!Visible)
        {
            Modulate = Colors.Transparent;
            Visible = true;
        }

        _tween = CreateTween();
        _tween.TweenProperty(this, CanvasItem.PropertyName.Modulate.ToString(), Colors.White, _fadeInSeconds);
    }

    public void HideAfterLinger()
    {
        if (!Visible)
        {
            return;
        }

        _tween?.Kill();
        _tween = CreateTween();
        _tween.TweenInterval(LingerSeconds);
        _tween.TweenProperty(this, CanvasItem.PropertyName.Modulate.ToString(), Colors.Transparent, _fadeOutSeconds);
        _tween.TweenCallback(Callable.From(HideNow));
    }

    public void HideNow()
    {
        _tween?.Kill();
        _tween = null;
        Visible = false;
        Modulate = Colors.White;
    }
}
