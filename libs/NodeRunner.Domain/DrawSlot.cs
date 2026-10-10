namespace NodeRunner.Domain;

/// <summary>Where a part draws within its link's draw group (#1107), back to front.</summary>
public enum DrawSlot
{
    /// <summary>A Wheel, under every link to its joint.</summary>
    Under,

    /// <summary>The link itself.</summary>
    Link,

    /// <summary>The sensor on a beam.</summary>
    Sensor,

    /// <summary>A Servo or plain joint ring, over every link end at its joint.</summary>
    Over,
}

/// <summary>How many <see cref="DrawSlot"/>s one draw group has.</summary>
public static class DrawSlots
{
    public const int Count = (int)DrawSlot.Over + 1;
}
