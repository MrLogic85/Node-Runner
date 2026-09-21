namespace NodeRunner.ML.Ga;

/// <summary>
/// Assigns a fixed population to reusable parallel evaluation slots.
/// Physics and fitness remain the caller's responsibility.
/// </summary>
public sealed class ParallelEvaluationSchedule
{
    private readonly int[] _activeCandidates;
    private int _nextCandidate;

    public ParallelEvaluationSchedule(int populationSize, int slotCount)
    {
        if (populationSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(populationSize));
        }

        if (slotCount < 1 || slotCount > populationSize)
        {
            throw new ArgumentOutOfRangeException(nameof(slotCount));
        }

        PopulationSize = populationSize;
        _activeCandidates = new int[slotCount];
        Reset();
    }

    public int PopulationSize { get; }

    public int SlotCount => _activeCandidates.Length;

    public int CompletedCount { get; private set; }

    public bool IsComplete => CompletedCount == PopulationSize;

    public int LowestActiveCandidate
    {
        get
        {
            var lowest = int.MaxValue;
            foreach (var candidate in _activeCandidates)
            {
                if (candidate >= 0)
                {
                    lowest = Math.Min(lowest, candidate);
                }
            }

            return lowest == int.MaxValue ? -1 : lowest;
        }
    }

    public int ActiveCandidate(int slot)
    {
        ValidateSlot(slot);
        return _activeCandidates[slot];
    }

    public bool TryAssignNext(int slot, out int candidate)
    {
        ValidateSlot(slot);
        if (_activeCandidates[slot] >= 0)
        {
            throw new InvalidOperationException($"Slot {slot} already has an active candidate.");
        }

        if (_nextCandidate >= PopulationSize)
        {
            candidate = -1;
            return false;
        }

        candidate = _nextCandidate++;
        _activeCandidates[slot] = candidate;
        return true;
    }

    public void Complete(int slot)
    {
        ValidateSlot(slot);
        if (_activeCandidates[slot] < 0)
        {
            throw new InvalidOperationException($"Slot {slot} has no active candidate.");
        }

        _activeCandidates[slot] = -1;
        CompletedCount++;
    }

    public void Reset()
    {
        Array.Fill(_activeCandidates, -1);
        _nextCandidate = 0;
        CompletedCount = 0;
    }

    private void ValidateSlot(int slot)
    {
        if (slot < 0 || slot >= _activeCandidates.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }
}
