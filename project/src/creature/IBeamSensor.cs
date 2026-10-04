namespace NodeRunner.Creature;

/// <summary>A sensor part on a beam as the sim runs it: writes its readings into the brain's input buffer.</summary>
public interface IBeamSensor
{
    /// <summary>How many brain inputs it writes, one per channel (<see cref="Domain.BrainPorts"/>).</summary>
    int ValueCount { get; }

    void Read(double[] values, int startIndex, double dt);

    /// <summary>Returns any internal state to rest for the beam's current pose.</summary>
    void Reset();
}
