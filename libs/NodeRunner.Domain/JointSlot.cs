namespace NodeRunner.Domain;

/// <summary>
/// A joint's part slots, each holding at most one part, declared in the order they stack on the joint
/// (docs/BUILD_MODE.md → Interactions). See docs/CREATURE_MODEL.md → Node → One part per slot.
/// </summary>
public enum JointSlot
{
    HingePart,
    Wheel,
    Rubber,
    TouchSensor,
}
