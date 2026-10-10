using NodeRunner.Domain;

namespace NodeRunner.Theme;

/// <summary>
/// The named draw layers inside one drawn creature (#767, #1107), as ZIndex values relative to the
/// creature's root. The parts draw on two surfaces, the unselected and above it the selected, each
/// in draw groups: one per link, back to front as <see cref="DrawGroups"/> orders them, each with
/// its <see cref="DrawSlot"/>s. Joints joined to no link draw over every group of their surface.
/// A part draws its selection mark with itself and rises whole to the selected surface (#766).
/// </summary>
public static class CreatureLayers
{
    /// <summary>The followed creature's knock-out outline (#818), under everything else.</summary>
    public const int Knockout = 0;

    /// <summary>The rigid hatch inside closed triangles.</summary>
    public const int Hatch = 1;

    /// <summary>The first draw group of the unselected surface.</summary>
    public const int Parts = Hatch + 1;

    /// <summary>Joints joined to no link, over every unselected part.</summary>
    public const int LooseJoints = Parts + DrawGroups.SurfaceRanks - 1;

    /// <summary>What a view highlights under the selected parts, such as the selected Servo's links.</summary>
    public const int SelectedUnderlays = LooseJoints + 1;

    /// <summary>The first draw group of the selected surface, over the whole unselected surface.</summary>
    public const int SelectedParts = SelectedUnderlays + 1;

    /// <summary>Selected joints joined to no link.</summary>
    public const int SelectedLooseJoints = SelectedParts + DrawGroups.SurfaceRanks - 1;

    /// <summary>What a view draws over the whole creature, such as Training's camera rays and Build's selection frame.</summary>
    public const int Overlays = SelectedLooseJoints + 1;

    /// <summary>How many ZIndex steps one creature takes, so drawn creatures can be stacked without mixing.</summary>
    public const int Count = Overlays + 1;

    /// <summary>The layer of a part in <paramref name="slot"/> of draw <paramref name="group"/>, by its <see cref="DrawGroups.Rank"/>.</summary>
    public static int Of(DrawSlot slot, int? group, bool selected) =>
        Parts + DrawGroups.Rank(slot, group, selected) + (selected ? SelectedParts - SelectedUnderlays : 0);
}
