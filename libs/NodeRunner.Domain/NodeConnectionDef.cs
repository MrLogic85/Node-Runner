namespace NodeRunner.Domain;

/// <summary>
/// One pin between two beams that share a node. <see cref="IsMotorized"/> is
/// true only for the one connection per rigid cluster (see
/// <see cref="MotorTopology"/>) that represents a genuine, independent
/// rotational degree of freedom; all other connections at a node — either
/// beams inside the same rigid cluster as <see cref="ReferenceBeamIndex"/>,
/// or non-leader members chained to their own cluster's leader — still get a
/// physical pin (so the structure stays connected) but carry no sensor or
/// brain output. <see cref="ReferenceBeamIndex"/> is therefore this specific
/// connection's zero-direction beam, not necessarily one shared identity
/// across every connection at the node. See docs/CREATURE_MODEL.md.
/// </summary>
public sealed record NodeConnectionDef(int NodeIndex, int ReferenceBeamIndex, int OtherBeamIndex, bool IsMotorized);
