using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>Where a creature's picture sits in a thumbnail: scaled by <see cref="Scale"/>, then moved by <see cref="Offset"/>.</summary>
public readonly record struct CreatureThumbnailFit(double Scale, Vector2D Offset)
{
    /// <summary>
    /// The largest scale a card's thumbnail draws at, so a lone joint or a small creature is not
    /// blown up past half its size in Build at 1:1.
    /// </summary>
    public const double LargestScale = 0.5;

    /// <summary>
    /// Fits <paramref name="creature"/>'s whole picture, every joint ring and every sensor picture at
    /// its beam's middle (#770), centred in a <paramref name="width"/> × <paramref name="height"/>
    /// thumbnail <paramref name="inset"/> inside each edge, never larger than <paramref name="largestScale"/>.
    /// Null when there is nothing to show or no room.
    /// </summary>
    public static CreatureThumbnailFit? Of(CreatureDef creature, double width, double height, double inset, double largestScale = LargestScale)
    {
        ArgumentNullException.ThrowIfNull(creature);
        var room = new Vector2D(width - (2 * inset), height - (2 * inset));
        if (Bounds(creature) is not { } bounds || room.X <= 0 || room.Y <= 0)
        {
            return null;
        }

        var scale = Math.Min(largestScale, Math.Min(room.X / bounds.Width, room.Y / bounds.Height));
        var center = bounds.Center;
        return new CreatureThumbnailFit(scale, new Vector2D((width / 2) - (center.X * scale), (height / 2) - (center.Y * scale)));
    }

    /// <summary>The creature's picture in creature units, or null when it has no joints.</summary>
    public static CanvasRect? Bounds(CreatureDef creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        if (creature.Nodes.Count == 0)
        {
            return null;
        }

        var extents = creature.Nodes.Select(node => (node.Position, Reach: creature.NodeRadius(node.Id))).ToList();
        foreach (var sensor in creature.Sensors)
        {
            var beam = creature.Beams[creature.BeamIndexOf(sensor.BeamId)];
            var a = creature.Nodes[creature.NodeIndexOf(beam.NodeA)].Position;
            var b = creature.Nodes[creature.NodeIndexOf(beam.NodeB)].Position;
            // The picture is a square turned with its beam, so its corners reach half its diagonal.
            extents.Add((new Vector2D((a.X + b.X) / 2, (a.Y + b.Y) / 2), SensorPicture.SizeOf(sensor.Kind) * Math.Sqrt(2) / 2));
        }

        return new CanvasRect(
            new Vector2D(extents.Min(extent => extent.Position.X - extent.Reach), extents.Min(extent => extent.Position.Y - extent.Reach)),
            new Vector2D(extents.Max(extent => extent.Position.X + extent.Reach), extents.Max(extent => extent.Position.Y + extent.Reach)));
    }
}
