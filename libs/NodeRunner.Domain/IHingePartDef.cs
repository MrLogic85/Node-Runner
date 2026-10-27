namespace NodeRunner.Domain;

/// <summary>
/// What fills a joint's one "hinge part" slot: a joint motor (<see cref="IJointMotorDef"/>),
/// a <see cref="BrakeDef"/> or an <see cref="AngleSensorDef"/>. It has a Fixed and a Target side, each a
/// link at the joint or the joint's Wheel, or null once that part is deleted.
/// </summary>
public interface IHingePartDef : IJointPartDef
{
    int? FixedLinkId { get; }

    int? TargetLinkId { get; }

    /// <summary>The part with new sides. It keeps its id, and so its ports and training.</summary>
    IHingePartDef WithLinks(int? fixedLinkId, int? targetLinkId);

    /// <summary>Which kind of part this is, as it is selected.</summary>
    CreatureElementKind Kind => PartKinds.Of(PartKind).Element;
}
