namespace NodeRunner.Domain;

/// <summary>
/// A part on a beam: a sensor or a Memory cell. A beam holds at most one, drawn as a picture on it
/// (<see cref="SensorPicture"/>), and removing the beam removes it. See docs/CREATURE_MODEL.md.
/// </summary>
public interface IBeamPartDef : IPartDef
{
    int BeamId { get; }
}
