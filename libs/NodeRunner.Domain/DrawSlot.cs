namespace NodeRunner.Domain;

/// <summary>Where a part draws within its link's draw group, back to front.</summary>
public enum DrawSlot
{
    /// <summary>A Wheel, under every link to its joint.</summary>
    Under,

    Link,

    /// <summary>A beam's sensor or Memory cell, over its beam.</summary>
    Sensor,

    /// <summary>
    /// A joint's bands, its Touch sensor's cushion and Rubber's skin, with the marks round the joint:
    /// over every link end at the joint, under its ring and the hinge part on it.
    /// </summary>
    Band,

    /// <summary>A motor or plain joint ring, over every link end at its joint.</summary>
    Over,
}

/// <summary>How many <see cref="DrawSlot"/>s one draw group has.</summary>
public static class DrawSlots
{
    public const int Count = (int)DrawSlot.Over + 1;
}
