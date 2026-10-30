namespace NodeRunner.Domain;

/// <summary>How Train setup sets each generation's Run length.</summary>
public enum RunLengthMode
{
    /// <summary>Every generation runs <see cref="TrainSettingsDef.RunLengthSeconds"/>.</summary>
    Fixed,

    /// <summary>Each generation draws one length from <see cref="TrainSettingsDef.RunLengthRange"/>.</summary>
    Ranged,
}
