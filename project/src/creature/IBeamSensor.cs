namespace NodeRunner.Creature;

/// <summary>A sensor part on a beam as the sim runs it: writes its readings into the network's input buffer.</summary>
public interface IBeamSensor
{
    /// <summary>How many network inputs it writes, one per channel (<see cref="Domain.NetworkPorts"/>).</summary>
    int ValueCount { get; }

    void Read(double[] values, int startIndex, double dt);

    /// <summary>Returns any internal state to rest for the beam's current pose.</summary>
    void Reset();
}
