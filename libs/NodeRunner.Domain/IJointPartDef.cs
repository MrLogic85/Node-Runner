namespace NodeRunner.Domain;

/// <summary>
/// A part that sits on a joint (#1044) and fills one of its slots, the one <see cref="PartKinds"/> gives its kind:
/// a hinge part (<see cref="IHingePartDef"/>), a Wheel, a Rubber or a Touch sensor.
/// </summary>
public interface IJointPartDef : IPartDef
{
    int NodeId { get; }
}
