namespace NodeRunner.Sim;

/// <summary>
/// Accumulates a creature's fitness for one trial: the furthest forward
/// horizontal distance it reaches from its starting position.
///
/// Using the running maximum (rather than the final position) rewards peak
/// forward progress without penalizing a creature that surges forward and
/// then settles or wobbles back slightly by the time the trial ends — a
/// simpler and more forgiving signal than net or cumulative distance.
/// </summary>
public sealed class Evaluator
{
    private float _startX;
    private float _bestForwardDistance;

    public float Fitness => _bestForwardDistance;

    /// <summary>Begins a new trial from the given starting X position.</summary>
    public void Reset(float startX)
    {
        _startX = startX;
        _bestForwardDistance = 0f;
    }

    /// <summary>Records the creature's current X position for this tick.</summary>
    public void Record(float currentX)
    {
        var forwardDistance = currentX - _startX;
        if (forwardDistance > _bestForwardDistance)
        {
            _bestForwardDistance = forwardDistance;
        }
    }
}
