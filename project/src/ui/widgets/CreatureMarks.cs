using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// What Build marks on the creature <see cref="CreatureParts"/> shows while it is edited: the
/// <paramref name="Selected"/> parts in <c>halo</c>, the joints <paramref name="ShowsAsLoose"/>
/// picks and too short links (<paramref name="ShowsTooShort"/>) in <c>danger</c>, each
/// Accelerometer's weight where <paramref name="WeightOffset"/> swings it (null at rest), the sensor
/// a tray drag would place or a sensor drag would move (<paramref name="PreviewSensor"/>, with a Camera's aim), or the joint a dragged
/// joint part, a Servo or a Wheel (#129), would take (<paramref name="PreviewJointPart"/>), the beams and joints a placed part
/// or moved sensor would take or refuse (<paramref name="Placing"/>), and what is drawn on the selected surface
/// (<paramref name="Raised"/>, #1107), which Build's touches hit there too. A shown creature that is not edited
/// has <see cref="None"/>.
/// </summary>
public sealed record CreatureMarks(
    PartSet Selected,
    Func<int, bool> ShowsAsLoose,
    Func<int, Vector2D?> WeightOffset,
    (BeamDef Beam, SensorKind Kind, double? Aim)? PreviewSensor,
    (BuildPart Part, int NodeId)? PreviewJointPart,
    bool ShowsTooShort,
    PlacingTargets Placing,
    RaisedParts Raised)
{
    public static CreatureMarks None { get; } = new(PartSet.None, _ => false, _ => null, null, null, ShowsTooShort: false, PlacingTargets.None, RaisedParts.None);

    /// <summary>The joint a dragged <paramref name="part"/> would take, or null while no such part is dragged there.</summary>
    public int? PreviewNodeOf(BuildPart part) => PreviewJointPart is { } preview && preview.Part == part ? preview.NodeId : null;
}
