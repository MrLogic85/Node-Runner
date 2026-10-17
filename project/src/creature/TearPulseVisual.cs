using Godot;
using NodeRunner.Theme;

namespace NodeRunner.Creature;

/// <summary>
/// The moment a beam end lets go of its joint, on the followed creature: a <c>danger</c> ring
/// that grows out from the joint and fades, run by a physics-tick <see cref="Tween"/> so it keeps
/// pace with the creature and stops on Pause. A child of the joint's body, whose rotation is locked,
/// over the whole creature; it frees itself when done. <see cref="TearPulseDrawing"/> draws it.
/// </summary>
public partial class TearPulseVisual : Node2D, IShadowVisual
{
    /// <summary>How long the pulse lasts: 24 ticks at 60 Hz.</summary>
    public const double Seconds = 0.4;

    private float _grown;

    /// <summary>Only the followed creature pulses: with many shadows tearing at once, pulses would be noise.</summary>
    public static ShadowDrawing AsShadow => ShadowDrawing.Hidden;

    public bool IsShadow
    {
        get => !Visible;
        set => Visible = !value;
    }

    public VisualTheme Theme { get; init; } = VisualTheme.Neon;

    /// <summary>The joint's outer drawn radius.</summary>
    public float Radius { get; init; }

    public override void _Ready()
    {
        ZIndex = CreatureLayers.Overlays;
        var tween = CreateTween().SetProcessMode(Tween.TweenProcessMode.Physics);
        tween.TweenMethod(Callable.From<float>(Grow), 0f, 1f, Seconds).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    public override void _Draw()
    {
        TearPulseDrawing.Draw(this, Theme, Radius, _grown);
    }

    private void Grow(float grown)
    {
        _grown = grown;
        QueueRedraw();
    }
}
