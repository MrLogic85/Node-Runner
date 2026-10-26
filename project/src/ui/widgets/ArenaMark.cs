using Godot;
using NodeRunner.Theme;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// A mark that stands on the Training arena's ground at a distance on the ruler: the best marker,
/// which keeps its screen size at any zoom, and the start sign, which is half a metre tall in the
/// world. It is a child of the ground, and it fades back like a shadow while a part's name is
/// shown, which may cover it.
/// </summary>
public abstract partial class ArenaMark : Node2D
{
    private double _startX;
    private bool _faded;

    /// <summary>The theme of the world it is in (<see cref="VisualTheme.For"/>).</summary>
    public VisualTheme Theme { get; private set; } = VisualTheme.Project;

    /// <summary>Where 0 m is, in the ground's coordinates: the ruler's start.</summary>
    public double StartX
    {
        get => _startX;
        set
        {
            _startX = value;
            OnChanged();
        }
    }

    /// <summary>Drawn at the shadows' opacity, behind what is in focus.</summary>
    public bool Faded
    {
        get => _faded;
        set
        {
            _faded = value;
            ApplyFaded();
        }
    }

    public override void _EnterTree() => Theme = VisualTheme.For(this);

    public override void _Ready() => Theme.Bind(this, OnThemeChanged);

    /// <summary>Called when the theme or the start changes.</summary>
    protected abstract void OnChanged();

    private void OnThemeChanged()
    {
        ApplyFaded();
        OnChanged();
    }

    private void ApplyFaded() => Modulate = Colors.White with { A = _faded ? Theme.ShadowAlpha : 1f };

    /// <summary>What the camera shows, in the ground's coordinates.</summary>
    protected Rect2 VisibleArea() =>
        GetParent<CanvasItem>().GetGlobalTransform().AffineInverse() * GetCanvasTransform().AffineInverse() * GetViewportRect();

    /// <summary>
    /// Ground units per UI unit in <paramref name="view"/>: how much to scale a mark back up by as
    /// the camera zooms out. The arena view undoes the UI size, so this applies it again.
    /// </summary>
    protected float ScreenScale(Rect2 view) => view.Size.X / GetViewportRect().Size.X * UiScale.FactorOf(this);
}
