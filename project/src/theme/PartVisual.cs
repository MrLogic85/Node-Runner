using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// What every shared part visual has: its theme, whether it is selected, and its place on
/// a <see cref="CreatureLayers"/> layer. A part in a draw group takes its layer from its
/// <see cref="DrawSlot"/>, the group <see cref="Place"/> finds for it and its surface: while
/// selected or raised it moves to the selected surface. A part draws its selection mark in its
/// own drawing. The parts take plain visual state only,
/// so Build, Training and thumbnails can all feed them.
/// </summary>
public partial class PartVisual : Node2D
{
    private readonly int _layer;
    private readonly DrawSlot? _slot;
    private VisualTheme _theme = VisualTheme.Project;
    private bool _selected;
    private bool _raised;
    private int? _group;
    private readonly List<(Node2D Layer, DrawSlot Slot, int? Group)> _slotLayers = [];

    public PartVisual()
    {
    }

    /// <param name="layer">The part's <see cref="CreatureLayers"/> layer, selected or not.</param>
    protected PartVisual(int layer)
    {
        _layer = layer;
        ZIndex = layer;
    }

    /// <param name="slot">Where the part draws within its draw group.</param>
    protected PartVisual(DrawSlot slot)
    {
        _slot = slot;
        ZIndex = CreatureLayers.Of(slot, null, selected: false);
    }

    /// <summary>
    /// Puts the part in its draw group in <paramref name="groups"/>: a link's or sensor's
    /// by the link id <paramref name="ownerId"/>, a joint part's by its joint id, in its own
    /// <see cref="DrawSlot"/>. A <paramref name="raised"/> part draws on the selected surface
    /// without being selected (<see cref="DrawGroups.Raised"/>).
    /// </summary>
    public void Place(DrawGroups groups, int ownerId, bool raised)
    {
        ArgumentNullException.ThrowIfNull(groups);
        _group = _slot is { } own ? GroupOf(groups, ownerId, own) : null;
        for (var i = 0; i < _slotLayers.Count; i++)
        {
            _slotLayers[i] = _slotLayers[i] with { Group = GroupOf(groups, ownerId, _slotLayers[i].Slot) };
        }

        _raised = raised;
        ApplyLayer();
    }

    /// <summary>
    /// Adds a child for a part that draws in a second <see cref="DrawSlot"/> as well as its own, such
    /// as a joint's bands under the hinge part on it. The child keeps to
    /// <paramref name="slot"/> of the part's draw group, and rises with the part to the selected
    /// surface. The part draws on it from the child's <c>Draw</c> signal.
    /// </summary>
    protected Node2D AddSlotLayer(DrawSlot slot, string name)
    {
        if (_slot is null)
        {
            throw new InvalidOperationException("Only a part in a draw group has slots to draw in.");
        }

        var layer = new Node2D { Name = name };
        _slotLayers.Add((layer, slot, null));
        AddChild(layer);
        ApplyLayer();
        return layer;
    }

    private static int? GroupOf(DrawGroups groups, int ownerId, DrawSlot slot) => slot switch
    {
        DrawSlot.Link or DrawSlot.Sensor => groups.Link(ownerId),
        _ => groups.Joint(ownerId, slot),
    };

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
            ApplyLayer();
            Changed();
        }
    }

    private void ApplyLayer()
    {
        var surface = _selected || _raised;
        ZIndex = _slot is { } own ? CreatureLayers.Of(own, _group, surface) : _layer;
        // A child's ZIndex adds to its part's.
        foreach (var (layer, slot, group) in _slotLayers)
        {
            layer.ZIndex = CreatureLayers.Of(slot, group, surface) - ZIndex;
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

    // A part may draw on child canvas items of its own (VelocityMotorPart's turning layers), so
    // everything under a part is redrawn with it.
    private static void RedrawParts(Node node, bool insidePart = false)
    {
        foreach (var child in node.GetChildren())
        {
            var isPart = insidePart || child is PartVisual;
            if (isPart && child is CanvasItem item)
            {
                item.QueueRedraw();
            }

            RedrawParts(child, isPart);
        }
    }
}
