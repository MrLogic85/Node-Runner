using System.Diagnostics.CodeAnalysis;
using NodeRunner.App.Builders;
using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// Placing parts from the tray (#376, #805) and moving a sensor to another beam (#806): one rule
/// for where a sensor goes, and a good drop is one Undo step that selects the part.
/// </summary>
public sealed partial class BuildViewModel
{
    /// <summary>
    /// Whether a tray part dropped on <paramref name="target"/> would be placed there (#376); if
    /// not, <paramref name="reason"/> says why. Sensors go on a beam that has none yet.
    /// </summary>
    public bool CanPlacePart(BuildPart part, CreatureElementSelection target, [NotNullWhen(false)] out UiText? reason)
    {
        ArgumentNullException.ThrowIfNull(target);
        // Every tray part has brain ports (#896).
        if (_locked)
        {
            reason = LockedReason;
            return false;
        }

        if (!PartTray.IsAvailable(part) || (PartTray.SensorKindOf(part) is null && part != BuildPart.Servo))
        {
            reason = PartTray.ComingLater;
            return false;
        }

        if (part == BuildPart.Servo)
        {
            if (target.Kind != CreatureElementKind.Node)
            {
                reason = CreatureBuilder.ServosGoOnAJointReason;
                return false;
            }

            return _builder.CanAddServo(target.Id, out reason);
        }

        return TakesSensor(PartTray.SensorKindOf(part)!.Value, target, movingSensorId: null, out reason);
    }

    /// <summary>
    /// Places a tray part dropped or tapped on <paramref name="target"/> and returns its new id
    /// (#376, #805). A drop on empty canvas (<paramref name="target"/> null) changes nothing and says
    /// nothing; a refused drop changes nothing and shows why as <see cref="PlacementNote"/> at that
    /// part. A dropped part is selected; a tapped one is not, so the tray stays for the next.
    /// </summary>
    public int? PlacePart(BuildPart part, CreatureElementSelection? target, bool select = true)
    {
        PlacementNote = null;
        if (target is null)
        {
            return null;
        }

        if (!CanPlacePart(part, target, out var reason))
        {
            if (!_locked)
            {
                PlacementNote = new CanvasNote(CanvasNoteKind.Danger, target, reason);
            }

            return null;
        }

        if (part == BuildPart.Servo)
        {
            var servoId = _history.Change(() => _builder.AddServo(target.Id));
            SelectPlaced(PartSet.None with { Servos = new HashSet<int> { servoId } }, select);
            return servoId;
        }

        var sensorId = _history.Change(() =>
        {
            _builder.AddSensor(target.Id, PartTray.SensorKindOf(part)!.Value, out var id, out _);
            return id;
        });
        SelectPlaced(PartSet.None with { Sensors = new HashSet<int> { sensorId } }, select);
        return sensorId;
    }

    private void SelectPlaced(PartSet placed, bool select)
    {
        if (select)
        {
            ReplaceSelection(placed);
        }
        else
        {
            RaiseAnatomyChanged();
        }
    }

    /// <summary>
    /// Whether the sensor dragged onto <paramref name="target"/> would move there (#806): onto a
    /// beam no other sensor sits on, its own included. If not, <paramref name="reason"/> says why.
    /// A move keeps the sensor's brain ports, so a locked Creation moves sensors too. The sensor
    /// must exist.
    /// </summary>
    public bool CanMoveSensor(int sensorId, [NotNullWhen(true)] CreatureElementSelection? target, [NotNullWhen(false)] out UiText? reason) =>
        TakesSensor(_builder.Sensors[_builder.SensorIndexOf(sensorId)].Kind, target, sensorId, out reason);

    /// <summary>
    /// Whether the beam would take the sensor a drag moves (<see cref="CanMoveSensor"/>), for the
    /// canvas to mark while it draws; null once the sensor is gone mid-drag, which moves nowhere.
    /// </summary>
    public bool? BeamTakesMovingSensor(int sensorId, int beamId) =>
        SensorExists(sensorId) ? CanMoveSensor(sensorId, new CreatureElementSelection(CreatureElementKind.Beam, beamId), out _) : null;

    /// <summary>
    /// The sensor as a drop on <paramref name="target"/> would leave it (#806), for its preview:
    /// null when the drop would be refused or keep it where it is, or the sensor or the beam under
    /// it is gone mid-drag.
    /// </summary>
    public SensorDef? SensorMovePreview(int sensorId, CreatureElementSelection? target) =>
        SensorExists(sensorId) && target is not null && Exists(target)
        && CanMoveSensor(sensorId, target, out _) && SensorBeam(sensorId) != target.Id
            ? _builder.SensorMovedTo(sensorId, target.Id)
            : null;

    /// <summary>
    /// Moves the sensor dragged onto <paramref name="target"/> there (#806), keeping its id, ports
    /// and settings (<see cref="CreatureBuilder.SensorMovedTo"/>), and selects it. A drop on empty
    /// canvas (<paramref name="target"/> null) leaves it and says nothing (#1026), as <see cref="PlacePart"/>
    /// does; a refused drop leaves it and shows why as <see cref="PlacementNote"/> at the part it was
    /// dropped on. A sensor gone mid-drag moves nowhere.
    /// </summary>
    public bool MoveSensor(int sensorId, CreatureElementSelection? target)
    {
        PlacementNote = null;
        if (!SensorExists(sensorId) || target is null)
        {
            return false;
        }

        if (!CanMoveSensor(sensorId, target, out var reason))
        {
            PlacementNote = new CanvasNote(CanvasNoteKind.Danger, target, reason);
            return false;
        }

        if (SensorBeam(sensorId) != target.Id)
        {
            _history.Change(() => _builder.MoveSensor(sensorId, target.Id, out _));
        }

        SelectPlaced(PartSet.None with { Sensors = new HashSet<int> { sensorId } }, select: true);
        return true;
    }

    /// <summary>
    /// The one rule for where a sensor goes, placed or moved: on a beam (<see
    /// cref="GoesOnABeamReason"/>) the builder lets it mount on (<see cref="CreatureBuilder.CanMountSensor"/>).
    /// </summary>
    private bool TakesSensor(SensorKind kind, [NotNullWhen(true)] CreatureElementSelection? target, int? movingSensorId, [NotNullWhen(false)] out UiText? reason)
    {
        if (target is not { Kind: CreatureElementKind.Beam })
        {
            reason = GoesOnABeamReason(kind);
            return false;
        }

        return _builder.CanMountSensor(target.Id, movingSensorId, out reason);
    }

    private bool SensorExists(int sensorId) => _builder.Sensors.Any(sensor => sensor.Id == sensorId);

    private int SensorBeam(int sensorId) => _builder.Sensors[_builder.SensorIndexOf(sensorId)].BeamId;
}
