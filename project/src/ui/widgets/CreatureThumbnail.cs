using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// A creature fitted to its rectangle, as on a Creations card, drawn with the same part visuals as
/// Build (#770): beams, links, joints, sensors and the rigid hatch, with no edit marks. The parts
/// live in their own still world (<see cref="UiWorldView.RequestRender"/>), which renders again
/// only when the creature, the size or the pixel density changes, so scrolling a list of cards
/// moves finished pictures. Its top corners round to the card's, because it sits at the top of a flush card.
/// </summary>
[Tool]
[GlobalClass]
public partial class CreatureThumbnail : Control
{
    private const float _inset = UiSize.Space.S3;

    private CreatureDef? _creature;
    private bool _refreshQueued;

    // Looked up, not exported: this is a tool script, and in the editor neither is its own type.
    private UiWorldView View => GetNode<UiWorldView>("View");

    private CreatureParts Parts => GetNode<CreatureParts>("View/WorldViewport/Parts");

    public CreatureDef? Creature
    {
        get => _creature;
        set
        {
            _creature = value;
            QueueRefresh();
        }
    }

    // A new fit can change the pixel density alone, which the parts' strokes are drawn for.
    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            return;
        }

        View.Fitted += QueueRefresh;
        QueueRefresh();
    }

    public override void _Draw() =>
        UiCorners.Top(UiSize.Radius.Large).Fill(this, new Rect2(Vector2.Zero, Size), UiThemeLookup.Color(this, UiTokens.Color.Background));

    // Once a frame at most, after layout, so the world has its new size before it renders. The
    // editor only draws the background: the parts are not a tool script.
    private void QueueRefresh()
    {
        if (_refreshQueued || Engine.IsEditorHint())
        {
            return;
        }

        _refreshQueued = true;
        Callable.From(Refresh).CallDeferred();
    }

    private void Refresh()
    {
        _refreshQueued = false;
        var fit = _creature is null ? null : CreatureThumbnailFit.Of(_creature, Size.X, Size.Y, _inset);
        var parts = Parts;
        parts.Visible = fit is not null;
        if (fit is { } placed)
        {
            parts.Position = new Vector2((float)placed.Offset.X, (float)placed.Offset.Y);
            parts.Scale = Vector2.One * (float)placed.Scale;
            parts.Show(_creature!);
        }

        View.RequestRender();
    }
}
