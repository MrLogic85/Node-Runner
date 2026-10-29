using NodeRunner.App.Builders;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The selection's Delete: the selected parts and what goes with them, refused on a locked Creation
/// when it would change the network.
/// </summary>
public sealed partial class BuildViewModel
{
    /// <summary>
    /// Why Delete cannot remove the selection now, or null when it can. A locked Creation
    /// refuses a delete that would change its network: one that takes a part with network ports, also by
    /// cascade (a joint's Piston, motor, Touch sensor or Angle sensor, a beam's sensor), or clears a motor's or an
    /// Angle sensor's Fixed or Target link,
    /// after which it could no longer train (<c>docs/SAVE_FORMAT.md</c> → "<c>creation.json</c>"). A Wheel,
    /// a Rubber and a Brake have no ports, so they can go, and so can a Brake's side.
    /// </summary>
    public UiText? DeleteLockedReason => DeleteRefusalNotes().Count > 0 ? LockedReason : null;

    /// <summary>
    /// Deletes the selection and what goes with it, as one undo step. While <see cref="DeleteLockedReason"/>
    /// says why not, it deletes nothing and instead shows a note on each part that blocks it in
    /// <see cref="CanvasNotes"/>.
    /// </summary>
    public void DeleteSelectedParts()
    {
        if (SelectedPartCount == 0)
        {
            return;
        }

        var refusalNotes = DeleteRefusalNotes();
        if (refusalNotes.Count > 0)
        {
            ShowRefusalNotes(refusalNotes);
            return;
        }

        // A joint part's joint goes too when the delete takes its links and its parts and leaves none:
        // to the player its parts are that joint, so clearing an area must not leave a
        // bare joint behind. A joint that had no links stays.
        var partJoints = SelectedJointPartJoints().ToDictionary(joint => joint, joint => _builder.LinksAt(joint).Count);
        _history.Change(() =>
        {
            RemoveParts(_builder, Selection);

            foreach (var (joint, linksBefore) in partJoints)
            {
                if (linksBefore > 0 && Exists(new CreatureElementSelection(CreatureElementKind.Node, joint)) && _builder.LinksAt(joint).Count == 0
                    && !CreatureDef.HasJointPart(joint, _builder.JointParts))
                {
                    _builder.RemoveNode(joint);
                }
            }

            // Within the step: its Undo row refresh must find no deleted part still selected.
            _selected.Clear();
        }, Selection);
        NotifySelectionChanged();
        RaiseAnatomyChanged();
    }

    // On a locked Creation, a note on each part that makes the delete change the network, in id order:
    // each part whose network ports it removes, and each kept part with ports whose Fixed or Target
    // side it clears. A Brake has no ports, so losing a side changes no network and gets no note. It
    // tries the delete on a copy of the body, so every cascade counts exactly as the delete does it.
    // Empty when the delete may go ahead.
    private List<CanvasNote> DeleteRefusalNotes()
    {
        List<CanvasNote> notes = [];
        if (!_locked || SelectedPartCount == 0)
        {
            return notes;
        }

        var before = _builder.Build();
        var trial = new CreatureBuilder(before);
        RemoveParts(trial, Selection);
        var after = trial.Build();
        var keptPorts = after.Parts.SelectMany(part => NetworkPorts.Of(part)).ToHashSet();

        foreach (var part in before.Parts.OrderBy(part => part.Id))
        {
            var ports = NetworkPorts.Of(part).ToList();
            var portsLost = ports.Any(port => !keptPorts.Contains(port));
            var sidesLost = ports.Count > 0 && part is IHingePartDef hinge && after.FindPart(part.Id) is IHingePartDef kept
                && (kept.FixedLinkId, kept.TargetLinkId) != (hinge.FixedLinkId, hinge.TargetLinkId);
            if (portsLost || sidesLost)
            {
                notes.Add(new(CanvasNoteKind.Danger, CreatureElementSelection.Of(part), _wouldChangeNetworkNote));
            }
        }

        return notes;
    }

    // Each part goes before what it sits on, so none is already gone with a deleted link or joint
    // when its turn comes.
    private static void RemoveParts(CreatureBuilder builder, PartSet parts)
    {
        var removed = parts.Parts.Select(part => builder.FindPart(part.Id)).OfType<IPartDef>()
            .OrderBy(RemoveRank)
            .ToList();
        foreach (var part in removed)
        {
            builder.RemovePart(part.Id);
        }

        // Joint parts and sensors first, then the links they may sit on, then the joints.
        static int RemoveRank(IPartDef part) => part is NodeDef ? 2 : PartKinds.IsLink(part) ? 1 : 0;
    }
}
