using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Callouts over a figure, each joined by a leader line to the spot it is about. The owner of the
/// figure says where each spot is, which way is clear of it, and how far its own drawing reaches
/// there; <see cref="UiCalloutLayout"/> decides where each callout goes, and the layer shows them
/// and draws the leaders behind them. Callouts keep their screen size at any zoom. Fill the
/// parent; the layer ignores the mouse.
/// </summary>
[GlobalClass]
public partial class UiCalloutLayer : Control
{
    private readonly List<UiCallout> _callouts = [];
    private readonly List<(Vector2 From, Vector2 To, UiCallout.CalloutKind Kind)> _leaders = [];

    public UiCalloutLayer()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>Shows exactly these callouts, in this layer's coordinates, and hides the rest.</summary>
    /// <remarks>The leaders are drawn by the layer itself, so they pass behind every callout.</remarks>
    public void SetCallouts(IReadOnlyList<UiCalloutLayout.Placement> placements)
    {
        ArgumentNullException.ThrowIfNull(placements);
        while (_callouts.Count < placements.Count)
        {
            var callout = new UiCallout();
            AddChild(callout);
            _callouts.Add(callout);
        }

        var sizes = new Vector2[placements.Count];
        for (var i = 0; i < placements.Count; i++)
        {
            var callout = _callouts[i];
            callout.Kind = placements[i].Kind;
            callout.IconId = placements[i].IconId;
            callout.Text = placements[i].Text;
            sizes[i] = callout.GetCombinedMinimumSize();
        }

        var arranged = UiCalloutLayout.Arrange(placements, sizes, new Rect2(Vector2.Zero, Size));
        _leaders.Clear();
        for (var i = 0; i < _callouts.Count; i++)
        {
            var callout = _callouts[i];
            callout.Visible = i < arranged.Count && !arranged[i].Joined && !arranged[i].Omitted;
            if (i >= arranged.Count)
            {
                continue;
            }

            var at = arranged[i];
            if (!at.Joined)
            {
                callout.Size = at.Rect.Size;
                callout.Position = at.Rect.Position;
            }

            if (placements[i].Leader && !at.Omitted)
            {
                _leaders.Add((placements[i].Anchor, at.LeaderEnd, placements[i].Kind));
            }
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_leaders.Count == 0)
        {
            return;
        }

        // Drawn in window pixels, so the antialiased edge stays one pixel wide at any UI size.
        var toPixels = UiPixelSpace.Enter(this, Transform2D.Identity);
        var width = UiSize.Stroke.Signal * UiPixelSpace.ScaleOf(toPixels);
        foreach (var (from, to, kind) in _leaders)
        {
            var color = UiThemeLookup.Color(this, UiCallout.BorderFor(kind));
            DrawLine(toPixels * from, toPixels * to, color, width, antialiased: true);
        }

        DrawSetTransformMatrix(Transform2D.Identity);
    }
}
