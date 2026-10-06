using NodeRunner.App.ViewModels;
using NodeRunner.Domain;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// What Build marks on the creature <see cref="CreatureParts"/> shows while it is edited: the
/// <paramref name="Selected"/> parts in <c>halo</c>, the joints <paramref name="ShowsAsLoose"/>
/// picks and too short links (<paramref name="ShowsTooShort"/>) in <c>danger</c>, each
/// Accelerometer's weight where <paramref name="WeightOffset"/> swings it (null at rest), the sensor
/// a tray drag would place (<paramref name="PreviewSensor"/>), and a selected Piston's stroke
/// (<paramref name="ShowsStroke"/>). A shown creature that is not edited has <see cref="None"/>.
/// </summary>
public sealed record CreatureMarks(
    PartSet Selected,
    Func<int, bool> ShowsAsLoose,
    Func<int, Vector2D?> WeightOffset,
    (BeamDef Beam, SensorKind Kind)? PreviewSensor,
    int? PreviewServoNode,
    bool ShowsTooShort,
    bool ShowsStroke)
{
    public static CreatureMarks None { get; } = new(PartSet.None, _ => false, _ => null, null, null, ShowsTooShort: false, ShowsStroke: false);
}
