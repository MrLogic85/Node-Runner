using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Where callouts over a figure go. Every callout is shown; none is left out.
/// <list type="bullet">
/// <item>A callout sits on its direction past its clearance plus <see cref="LeaderLength"/>, with
/// its nearest side just there, so it does not cover its own spot.</item>
/// <item>It stays inside the bounds: near an edge it is pushed along it, never to the other side
/// of its part.</item>
/// <item>Callouts that would overlap form a stack: one column of callouts, in the order they are
/// listed (so list the most important first; it sits nearest its part), growing away from the
/// first one's part. A callout with the same kind, icon and text as one already in the stack,
/// and no lines under its caption, adds only its leader to that one. A stack that grows into
/// another takes it in, until no two overlap.</item>
/// </list>
/// Leaders run from each spot to the centre of its callout and are drawn behind all callouts.
/// </summary>
public static class UiCalloutLayout
{
    /// <summary>The gap between what is drawn at the spot and the callout, spanned by the leader.</summary>
    public const float LeaderLength = 28;

    /// <summary>
    /// One callout: its spot, the clear direction from it, and how far the figure reaches that
    /// way; <paramref name="Lines"/> are its live values under the caption, if any (#1064).
    /// </summary>
    public readonly record struct Placement(
        Vector2 Anchor,
        Vector2 Direction,
        float Clearance,
        UiCallout.CalloutKind Kind,
        UiIconId IconId,
        string Text,
        IReadOnlyList<UiCalloutLine>? Lines = null);

    /// <summary>
    /// Where a callout went and where its leader from the spot meets it; when it
    /// <paramref name="Joined"/> an earlier callout with the same text, the rect is that one's and
    /// it shows no callout of its own.
    /// </summary>
    public readonly record struct Arranged(Rect2 Rect, Vector2 LeaderEnd, bool Joined = false);

    /// <summary>Where each callout goes, inside <paramref name="bounds"/> less a <see cref="UiSize.Space.S1"/> margin.</summary>
    public static IReadOnlyList<Arranged> Arrange(IReadOnlyList<Placement> placements, IReadOnlyList<Vector2> sizes, Rect2 bounds)
    {
        ArgumentNullException.ThrowIfNull(placements);
        ArgumentNullException.ThrowIfNull(sizes);
        var inner = bounds.Grow(-UiSize.Space.S1);
        var stacks = Enumerable.Range(0, placements.Count).Select(i => new List<int> { i }).ToList();
        var laidOut = stacks.Select(stack => LayOut(stack, placements, sizes, inner)).ToList();
        while (FirstOverlap(laidOut) is var (first, second))
        {
            stacks[first].AddRange(stacks[second]);
            stacks[first].Sort();
            stacks.RemoveAt(second);
            laidOut.RemoveAt(second);
            laidOut[first] = LayOut(stacks[first], placements, sizes, inner);
        }

        var result = new Arranged[placements.Count];
        foreach (var stack in laidOut)
        {
            foreach (var (index, rect, joined) in stack.Members)
            {
                result[index] = new Arranged(rect, Entry(rect, placements[index].Anchor), joined);
            }
        }

        return result;
    }

    private sealed record Stack(Rect2 Bounds, IReadOnlyList<(int Index, Rect2 Rect, bool Joined)> Members);

    private static (int First, int Second)? FirstOverlap(IReadOnlyList<Stack> stacks)
    {
        for (var first = 0; first < stacks.Count; first++)
        {
            for (var second = first + 1; second < stacks.Count; second++)
            {
                if (stacks[first].Bounds.Grow(UiSize.Space.S1).Intersects(stacks[second].Bounds))
                {
                    return (first, second);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The stack's callouts in one column, centred on where its first callout would sit alone and
    /// growing away from that one's part, then pushed inside the bounds as a whole.
    /// </summary>
    private static Stack LayOut(IReadOnlyList<int> members, IReadOnlyList<Placement> placements, IReadOnlyList<Vector2> sizes, Rect2 bounds)
    {
        var lead = placements[members[0]];
        var direction = DirectionOf(lead);
        var start = Out(lead, direction, sizes[members[0]]);
        var centreX = start.GetCenter().X;
        var upward = direction.Y <= 0;
        var edge = upward ? start.End.Y : start.Position.Y;
        var boxes = new List<(int Index, Rect2 Rect)>();
        var assigned = new List<(int Index, int Box)>();
        foreach (var index in members)
        {
            var placement = placements[index];
            var same = boxes.FindIndex(box =>
                placements[box.Index].Kind == placement.Kind
                && placements[box.Index].IconId == placement.IconId
                && placements[box.Index].Text == placement.Text
                && placements[box.Index].Lines is null
                && placement.Lines is null);
            if (same >= 0)
            {
                assigned.Add((index, same));
                continue;
            }

            var size = sizes[index];
            var top = upward ? edge - size.Y : edge;
            boxes.Add((index, new Rect2(centreX - (size.X / 2), top, size)));
            edge = upward ? top - UiSize.Space.S1 : top + size.Y + UiSize.Space.S1;
            assigned.Add((index, boxes.Count - 1));
        }

        var all = boxes.Skip(1).Aggregate(boxes[0].Rect, (union, box) => union.Merge(box.Rect));
        var shift = PushInside(all, bounds).Position - all.Position;
        return new Stack(
            new Rect2(all.Position + shift, all.Size),
            assigned.Select(member =>
            {
                var (owner, rect) = boxes[member.Box];
                return (member.Index, new Rect2(rect.Position + shift, rect.Size), owner != member.Index);
            }).ToList());
    }

    private static Vector2 DirectionOf(Placement placement) =>
        placement.Direction.IsZeroApprox() ? Vector2.Up : placement.Direction.Normalized();

    private static Rect2 Out(Placement placement, Vector2 direction, Vector2 size)
    {
        var reach = (Mathf.Abs(direction.X) * size.X / 2) + (Mathf.Abs(direction.Y) * size.Y / 2);
        var centre = placement.Anchor + (direction * (placement.Clearance + LeaderLength + reach));
        return new Rect2(centre - (size / 2), size);
    }

    private static Rect2 PushInside(Rect2 rect, Rect2 bounds) => new(
        new Vector2(
            Mathf.Clamp(rect.Position.X, bounds.Position.X, Math.Max(bounds.Position.X, bounds.End.X - rect.Size.X)),
            Mathf.Clamp(rect.Position.Y, bounds.Position.Y, Math.Max(bounds.Position.Y, bounds.End.Y - rect.Size.Y))),
        rect.Size);

    /// <summary>Where the line from the spot to the callout's centre meets its edge.</summary>
    private static Vector2 Entry(Rect2 rect, Vector2 anchor)
    {
        var centre = rect.GetCenter();
        return SegmentEntry(anchor, centre, rect) ?? centre;
    }

    /// <summary>The first point of the segment inside the rect, or null when it misses.</summary>
    private static Vector2? SegmentEntry(Vector2 from, Vector2 to, Rect2 rect)
    {
        var delta = to - from;
        float enter = 0, leave = 1;
        for (var axis = 0; axis < 2; axis++)
        {
            var start = axis == 0 ? from.X : from.Y;
            var step = axis == 0 ? delta.X : delta.Y;
            var low = axis == 0 ? rect.Position.X : rect.Position.Y;
            var high = axis == 0 ? rect.End.X : rect.End.Y;
            if (Mathf.IsZeroApprox(step))
            {
                if (start < low || start > high)
                {
                    return null;
                }

                continue;
            }

            var a = (low - start) / step;
            var b = (high - start) / step;
            enter = Math.Max(enter, Math.Min(a, b));
            leave = Math.Min(leave, Math.Max(a, b));
        }

        return enter <= leave ? from + (delta * enter) : null;
    }
}
