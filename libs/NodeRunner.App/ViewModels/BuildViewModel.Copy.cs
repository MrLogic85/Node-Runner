using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// The selection panel's Copy: several selected parts duplicated beside themselves,
/// inside the same Creation. A part copies only with what it sits on: a link with both its joints
/// and a sensor with its beam. A joint part is its joint to the player, so it brings that joint
/// along. A locked Creation copies no part with network ports, since a copy would change its
/// network; a Wheel, a Rubber and a Brake have none.
/// </summary>
public sealed partial class BuildViewModel
{
    /// <summary>
    /// How far a copy goes aside, each way: the fewest whole <see cref="GridStep"/>s that make at
    /// least <see cref="BuildAreaCell"/>, so a copy never covers its original and lands on the grid.
    /// </summary>
    public double CopyStep => Math.Ceiling(BuildAreaCell / GridStep) * GridStep;

    /// <summary>Whether enough parts are selected to copy: two or more, locked or not.</summary>
    public bool CanOfferCopy => SelectedPartCount >= 2;

    /// <summary>Whether Copy would duplicate the selection now; when not, the button is dimmed.</summary>
    public bool CanCopySelection => CanOfferCopy && CopyBlockers().Count == 0;

    /// <summary>
    /// A danger note at each selected part a copy cannot take: on a locked Creation first each part
    /// with network ports, then a beam, Piston or Spring without both joints among
    /// <see cref="SelectedNodeIds"/>, or a sensor without its beam; in id order within each.
    /// </summary>
    public IReadOnlyList<CanvasNote> CopyBlockers()
    {
        var joints = SelectedNodeIds;
        return
        [
            .. SelectedParts()
                .Select(part => (Part: part, Text: CopyBlocker(part, joints)))
                .Where(blocked => blocked.Text is not null)
                .OrderBy(blocked => KeepsItsPorts(blocked.Part) ? 0 : 1)
                .ThenBy(blocked => blocked.Part.Id)
                .Select(blocked => new CanvasNote(CanvasNoteKind.Danger, CreatureElementSelection.Of(blocked.Part), blocked.Text!)),
        ];
    }

    // Why a copy cannot take part, or null when it can.
    private UiText? CopyBlocker(IPartDef part, IReadOnlyCollection<int> joints)
    {
        if (KeepsItsPorts(part))
        {
            return _wouldChangeNetworkNote;
        }

        if (PartKinds.IsLink(part))
        {
            var link = _builder.Link(part.Id);
            if (!(joints.Contains(link.NodeA) && joints.Contains(link.NodeB)))
            {
                return UiText.Plain("Select both its joints");
            }
        }

        return part is IBeamPartDef beamPart && !IsSelected(CreatureElementKind.Beam, beamPart.BeamId) ? UiText.Plain("Select its beam") : null;
    }

    /// <summary>
    /// Duplicates the selection <see cref="CopyStep"/> aside and selects the copy, as one undo step that
    /// selects the originals again. Each copy keeps its settings but not its name; a copied hinge part
    /// sits on the copy of its joint, uses the copies of its Fixed and Target links or Wheel, and
    /// leaves a role empty whose side was not copied. A copied joint part brings its joint along, but the
    /// joint's other parts only when they are selected too. The copy is selected as the
    /// original was: joint parts stand in for their copied joint. While Copy is dimmed, it instead shows <see cref="CopyBlockers"/> in
    /// <see cref="CanvasNotes"/>.
    /// </summary>
    public void CopySelectedParts()
    {
        if (!CanOfferCopy)
        {
            return;
        }

        var blockers = CopyBlockers();
        if (blockers.Count > 0)
        {
            ShowRefusalNotes(blockers);
            return;
        }

        var joints = SelectedNodeIds;
        var selectedJoints = SelectedIds(CreatureElementKind.Node).ToHashSet();
        var nodes = Nodes.Where(node => joints.Contains(node.Id)).ToList();

        var parts = SelectedParts().Where(part => part is not NodeDef)
            .OrderBy(CopyRank)
            .ThenBy(part => part.Id)
            .ToList();

        // A copied joint is as big as the joint parts that come along with it.
        var copiedRadii = nodes.ToDictionary(node => node.Id, node => CreatureDef.JointRadius(node.Id, parts.OfType<IJointMotorDef>(), parts.OfType<WheelDef>()));
        var offset = CopyOffset(nodes, copiedRadii);
        _history.Change(() =>
        {
            _selected.Clear();
            var copies = new Dictionary<int, int>();
            foreach (var node in nodes)
            {
                var position = new Vector2D(node.Position.X + offset.X, node.Position.Y + offset.Y);
                // Clamping too absorbs the rounding in a shortened offset, as in TranslateSelection.
                copies[node.Id] = _builder.AddNode(BuildArea.Clamp(position, copiedRadii[node.Id]));
                if (selectedJoints.Contains(node.Id))
                {
                    _selected.Add(new CreatureElementSelection(CreatureElementKind.Node, copies[node.Id]));
                }
            }

            foreach (var part in parts)
            {
                copies[part.Id] = CopySettings(part.Id, AddCopy(part, copies));
                _selected.Add(new CreatureElementSelection(CreatureElementSelection.Of(part).Kind, copies[part.Id]));
            }
        }, Selection);
        NotifySelectionChanged();
        RaiseAnatomyChanged();

        // Each part after what it sits on or takes as a side: links first, then sensors, Memory cells, Wheels and the
        // other joint parts, and hinge parts last, since a hinge part may take a copied link or Wheel as a side.
        static int CopyRank(IPartDef part) => PartKinds.IsLink(part) ? 0 : part is IHingePartDef ? 2 : 1;
    }

    // Whether a locked Creation keeps part from being copied, as a copy would add network ports.
    private bool KeepsItsPorts(IPartDef part) => _locked && NetworkPorts.Of(part).Any();

    // The selected parts, each as the body has it.
    private IEnumerable<IPartDef> SelectedParts() => _selected.Select(FindPart).OfType<IPartDef>();

    // Adds a part like part, on the copies of what it sits on, and returns its id. copies maps each
    // part copied so far to its copy; a hinge part's side that was not copied is left empty.
    private int AddCopy(IPartDef part, Dictionary<int, int> copies) => part switch
    {
        BeamDef beam => _builder.AddBeam(copies[beam.NodeA], copies[beam.NodeB]),
        PistonDef piston => _builder.AddPiston(copies[piston.NodeA], copies[piston.NodeB]),
        SpringDef spring => _builder.AddSpring(copies[spring.NodeA], copies[spring.NodeB]),
        // The beam is a fresh copy, so it holds no sensor yet and takes this one.
        SensorDef sensor => _builder.AddSensor(copies[sensor.BeamId], sensor.Kind, out var copy, out var reason)
            ? copy
            : throw new InvalidOperationException(reason.Message),
        MemoryCellDef memoryCell => _builder.AddMemoryCell(copies[memoryCell.BeamId]),
        IHingePartDef hinge => _builder.AddHingePart(hinge.PartKind, copies[hinge.NodeId], CopyOf(hinge.FixedLinkId, copies), CopyOf(hinge.TargetLinkId, copies)),
        WheelDef wheel => _builder.AddWheel(copies[wheel.NodeId]),
        TouchSensorDef touch => _builder.AddTouchSensor(copies[touch.NodeId]),
        RubberDef rubber => _builder.AddRubber(copies[rubber.NodeId]),
        _ => throw new ArgumentOutOfRangeException(nameof(part), part.PartKind, "A joint is copied by position, and every other part kind has a copy."),
    };

    private static int? CopyOf(int? partId, Dictionary<int, int> copies) =>
        partId is { } id && copies.TryGetValue(id, out var copy) ? copy : null;

    private int CopySettings(int fromPartId, int toPartId)
    {
        foreach (var parameter in _builder.ParametersOf(fromPartId))
        {
            _builder.SetParameter(toPartId, parameter, _builder.ParameterValue(fromPartId, parameter));
        }

        return toPartId;
    }

    // CopyStep down and right, or the first other diagonal that keeps every copied joint inside
    // the build area; if none does, the first diagonal shortened so the copy keeps its shape.
    private Vector2D CopyOffset(IReadOnlyList<NodeDef> nodes, Dictionary<int, double> radii)
    {
        var joints = nodes.Select(node => (node.Position, radii[node.Id])).ToList();
        var step = CopyStep;
        Vector2D[] offsets =
        [
            new(step, step),
            new(-step, step),
            new(step, -step),
            new(-step, -step),
        ];
        foreach (var offset in offsets)
        {
            if (ShortenedToBuildArea(joints, offset) == offset)
            {
                return offset;
            }
        }

        return ShortenedToBuildArea(joints, offsets[0]);
    }
}
