using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Theme;

/// <summary>
/// One layer of a part's drawing on a node of its own (#454), so it turns by its own transform
/// and is drawn again only when its look or the pixel scale changes, never as the creature moves.
/// </summary>
public partial class PartLayer : Node2D
{
    public Action<UiPixelPen>? Drawing { get; set; }

    public override void _Draw()
    {
        if (Drawing is null)
        {
            return;
        }

        using var pen = UiPixelPen.Begin(this);
        Drawing(pen);
    }
}
