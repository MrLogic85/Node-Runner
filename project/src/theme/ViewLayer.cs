using Godot;

namespace NodeRunner.Theme;

/// <summary>
/// A view's own drawing on one of a creature's named layers (#769), such as Build's placing
/// feedback under the links or its markers and Select frame over the whole creature. The view
/// draws on it from its <see cref="CanvasItem.Draw"/> signal, so its marks take their place
/// among the parts without the view setting a <c>ZIndex</c>.
/// </summary>
public partial class ViewLayer : Node2D
{
    /// <summary>A layer under the links, over the rigid hatch (<see cref="CreatureLayers.Underlays"/>).</summary>
    public static ViewLayer Underlay() => new() { ZIndex = CreatureLayers.Underlays };

    /// <summary>A layer over the whole creature (<see cref="CreatureLayers.Overlays"/>).</summary>
    public static ViewLayer Overlay() => new() { ZIndex = CreatureLayers.Overlays };
}
