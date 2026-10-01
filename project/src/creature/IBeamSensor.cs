namespace NodeRunner.Creature;

/// <summary>A sensor part on a beam as the sim runs it: writes its readings into the brain's input buffer.</summary>
public interface IBeamSensor
{
    /// <summary>The group label shown in the mapping display, e.g. "Accelerometer".</summary>
    string GroupKind { get; }

    IReadOnlyList<string> ValueNames { get; }

    void Read(double[] values, int startIndex, double dt);

    /// <summary>Returns any internal state to rest for the beam's current pose.</summary>
    void Reset();
}
