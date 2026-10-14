using NodeRunner.Domain;

namespace NodeRunner.Mechanics;

/// <summary>
/// When a beam end lets go of its joint (#962). Godot's joints have no break force and do not report
/// what they push, so the rule reads how far the beam's pinned end has drifted from its joint: an end
/// lets go once its drift has been at least <see cref="DriftLimit"/> for <see cref="Ticks"/> ticks in a row.
/// The #1255 spike set both: ordinary creatures, even dropped from 3 m, drifted at most 11.3 units over
/// four ticks, and every truss that went on to flip crossed 20 first.
/// </summary>
public static class Tear
{
    /// <summary>The drift at which an end starts to let go, in world units: 20, 0.2 m.</summary>
    public const double DriftLimit = 20;

    /// <summary>How many ticks in a row an end's drift must stay at or over <see cref="DriftLimit"/>: 4.</summary>
    public const int Ticks = 4;

    /// <summary>
    /// How many ticks in a row an end has now drifted at or over the limit, from the count before this tick
    /// and this tick's drift. A drift under the limit starts over at 0; one that is not a number counts as over.
    /// </summary>
    public static int NextTicksOver(int ticksOver, double drift) =>
        drift < DriftLimit ? 0 : Math.Min(ticksOver + 1, Ticks);

    /// <summary>Whether an end that has drifted over the limit for <paramref name="ticksOver"/> ticks in a row lets go.</summary>
    public static bool LetsGo(int ticksOver) => ticksOver >= Ticks;

    /// <summary>
    /// The joints of the piece a creature's centre and bounds follow after a tear, by id. The one-piece
    /// check (<see cref="Pieces.Of"/>, #930) runs over the links still joining their joints: every Piston
    /// and Spring, and every beam with neither end torn. Of the pieces it lists (by their first joint in
    /// joint order), the one with the most joints wins, the first listed on a tie. When no link joins
    /// anything, the first joint stands for the creature.
    /// </summary>
    /// <param name="creature">The creature as built.</param>
    /// <param name="endTorn">Whether each beam end has torn: end A of beam i at 2i, end B at 2i + 1.</param>
    public static IReadOnlyList<int> MainPiece(CreatureDef creature, IReadOnlyList<bool> endTorn)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(endTorn);
        if (endTorn.Count != 2 * creature.Beams.Count)
        {
            throw new ArgumentException("Every beam has two ends.", nameof(endTorn));
        }

        var holding = creature.Beams.Where((_, beam) => !endTorn[2 * beam] && !endTorn[(2 * beam) + 1]);
        IReadOnlyList<int> main = [];
        foreach (var piece in Pieces.Of(creature.Nodes, LinkRef.All(holding, creature.Pistons, creature.Springs)))
        {
            if (piece.Count > main.Count)
            {
                main = piece;
            }
        }

        return main.Count > 0 ? main : [creature.Nodes[0].Id];
    }
}
