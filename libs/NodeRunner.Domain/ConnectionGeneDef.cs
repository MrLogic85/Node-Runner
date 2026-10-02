namespace NodeRunner.Domain;

/// <summary>
/// One weighted connection in a saved brain (#536), keyed by <see cref="From"/> and <see cref="To"/>
/// neuron ids. Neuron ids are never reused, so the key works like a NEAT innovation number. A
/// disabled gene keeps its weight but carries no signal, and can be enabled again.
/// </summary>
public sealed record ConnectionGeneDef
{
    public ConnectionGeneDef(int from, int to, double weight, bool enabled)
    {
        if (from <= 0 || to <= 0)
        {
            throw new ArgumentOutOfRangeException(from <= 0 ? nameof(from) : nameof(to), "Neuron ids must be positive.");
        }

        if (!double.IsFinite(weight))
        {
            throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be finite.");
        }

        From = from;
        To = to;
        Weight = weight;
        Enabled = enabled;
    }

    public int From { get; }

    public int To { get; }

    public double Weight { get; }

    public bool Enabled { get; }
}
