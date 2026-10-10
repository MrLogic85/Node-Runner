using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Callouts over a figure, each joined by a leader line to the spot it is about. The owner of the
/// figure says where each spot is, which way is clear of it, and how far its own drawing reaches
/// there; <see cref="UiCalloutLayout"/> decides where each callout goes, and the layer shows them
/// and draws the leaders behind them. Callouts keep their screen size at any zoom. Fill the
/// parent; the layer ignores the mouse. Callout text is set in code, already in the player's
/// language or as the player wrote it, so the layer turns auto-translation off for its callouts.
/// </summary>
[GlobalClass]
public partial class UiCalloutLayer : Control
{
    private readonly List<UiCallout> _callouts = [];
    private readonly List<(Vector2 From, Vector2 To, UiCallout.CalloutKind Kind)> _leaders = [];

    public UiCalloutLayer()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AutoTranslateMode = AutoTranslateModeEnum.Disabled;
    }

    /// <summary>Shows exactly these callouts, in this layer's coordinates, and hides the rest. Their text is shown as given.</summary>
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
            callout.Lines = placements[i].Lines;
            sizes[i] = callout.GetCombinedMinimumSize();
        }

        var arranged = UiCalloutLayout.Arrange(placements, sizes, new Rect2(Vector2.Zero, Size));
        _leaders.Clear();
        for (var i = 0; i < _callouts.Count; i++)
        {
            var callout = _callouts[i];
            callout.Visible = i < arranged.Count && !arranged[i].Joined;
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

            _leaders.Add((placements[i].Anchor, at.LeaderEnd, placements[i].Kind));
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
