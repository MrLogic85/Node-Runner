using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// What every shared part visual has (#767): its theme, whether it is selected, and its place on
/// a <see cref="CreatureLayers"/> layer. A part draws its selection mark in its own drawing, and
/// while selected it moves to its kind's selected layer. The parts take plain visual state only,
/// so Build, Training and thumbnails can all feed them.
/// </summary>
public partial class PartVisual : Node2D
{
    private readonly int _layer;
    private readonly int _selectedLayer;
    private VisualTheme _theme = VisualTheme.Neon;
    private bool _selected;

    public PartVisual()
    {
    }

    /// <param name="layer">The part's <see cref="CreatureLayers"/> layer.</param>
    /// <param name="selectedLayer">Its layer while selected.</param>
    protected PartVisual(int layer, int selectedLayer)
    {
        _layer = layer;
        _selectedLayer = selectedLayer;
        ZIndex = layer;
    }

    public VisualTheme Theme
    {
        get => _theme;
        set => Change(ref _theme, value);
    }

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }

            _selected = value;
            ZIndex = value ? _selectedLayer : _layer;
            QueueRedraw();
        }
    }

    /// <summary>Sets <paramref name="field"/> and redraws when the value changes.</summary>
    protected void Change<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        Changed();
    }

    /// <summary>Called when a property changes; redraws.</summary>
    protected virtual void Changed() => QueueRedraw();

    /// <summary>
    /// How far, as a share, the window pixel scale may move from the one the parts were drawn at
    /// before <see cref="RedrawOnNewPixelScale"/> draws them again.
    /// </summary>
    public const float PixelScaleTolerance = 0.02f;

    /// <summary>
    /// A part draws its antialiased edges in window pixels at the scale of its last draw
    /// (<c>UiPixelPen</c>), so a zoom, UI size or screen change must draw it again. Every view
    /// that holds parts calls this as it may have zoomed, with the scale its parts under
    /// <paramref name="root"/> were drawn at; when the scale has moved past
    /// <see cref="PixelScaleTolerance"/>, it redraws them all and updates <paramref name="drawnAt"/>.
    /// </summary>
    public static void RedrawOnNewPixelScale(CanvasItem root, ref float drawnAt)
    {
        var pixelScale = UiPixelSpace.ScaleOf(UiPixelSpace.ItemToPixels(root));
        if (Mathf.Abs(pixelScale - drawnAt) <= drawnAt * PixelScaleTolerance)
        {
            return;
        }

        drawnAt = pixelScale;
        RedrawParts(root);
    }

    private static void RedrawParts(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is PartVisual part)
            {
                part.QueueRedraw();
            }

            RedrawParts(child);
        }
    }
}
