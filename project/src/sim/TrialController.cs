using Godot;
using NodeRunner.ML.Ga;

namespace NodeRunner.Sim;

/// <summary>
/// Runs a single creature through fixed-duration trials, resetting its pose
/// between runs and measuring each trial with a <see cref="TrialMeasurement"/>.
/// A <see cref="TrialDurationTicks"/> of <see cref="int.MaxValue"/> makes one
/// trial that never ends, as Simulate (#702) plays.
///
/// This node does not own the creature's lifecycle (creation/destruction) or
/// brain assignment — its caller (the <see cref="Evolver"/>, or TrainingHost
/// in Simulate) is responsible for both. TrialController only knows how to
/// time a trial and measure how far, how fast and how high the creature got.
/// </summary>
public partial class TrialController : Node
{
    private readonly TrialMeasurement _measurement = new(SimSpeed.TicksPerSecond);
    private Creature.Creature? _creature;
    private int _elapsedTicks;

    /// <summary>Trial length in physics ticks. Default is 10s at 60Hz.</summary>
    public int TrialDurationTicks { get; set; } = 600;

    public bool IsRunning { get; private set; }

    public int ElapsedTicks => _elapsedTicks;

    /// <summary>What the trial in progress (or the last one) has measured so far.</summary>
    public TrialResult Measured => _measurement.Result;

    /// <summary>
    /// The gap between the creature's lowest point and the ground when every trial starts, in
    /// creature units. A small fixed drop gives every trial the same start (#649); the fall counts
    /// as trial time, and distance is measured from the start X, so it doesn't change fitness.
    /// </summary>
    public const float StartClearance = 6f;

    /// <summary>The Y of the ground's top edge, which elevation is measured from.</summary>
    public float GroundTopY { get; set; }

    /// <summary>Raised when a trial finishes, with what it measured; its centre's distance is the fitness.</summary>
    public event Action<TrialResult>? TrialCompleted;

    public override void _Ready()
    {
        // Must run its _PhysicsProcess before any Creature's (default
        // priority 0): TrialCompleted fires synchronously on the boundary
        // tick, and a subscriber typically calls StartTrial (ResetPose)
        // right away. If Creature ran first, it would already have driven
        // motors from the outgoing trial's final pose that tick, and that
        // torque would then be integrated against the freshly-reset pose —
        // contaminating the new trial with leftover motion from the old one.
        ProcessPhysicsPriority = -100;
    }

    /// <summary>
    /// Resets the given creature to its built pose, <see cref="StartClearance"/> above the ground,
    /// and starts a fresh trial for it. Replaces any trial already in progress.
    /// </summary>
    public void StartTrial(Creature.Creature creature)
    {
        ArgumentNullException.ThrowIfNull(creature);

        _creature = creature;
        _creature.ResetPose(GroundTopY - StartClearance);
        _measurement.Reset(_creature.CenterOfMass.X, _creature.Bounds.End.X);
        _elapsedTicks = 0;
        IsRunning = true;
    }

    /// <summary>Stops the current trial without raising <see cref="TrialCompleted"/>.</summary>
    public void Stop()
    {
        IsRunning = false;
        _creature = null;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsRunning || _creature is null)
        {
            return;
        }

        _elapsedTicks++;
        _measurement.Record(_creature.CenterOfMass.X, _creature.Bounds.End.X, GroundTopY - _creature.LowestPointY);

        if (_elapsedTicks >= TrialDurationTicks)
        {
            IsRunning = false;
            TrialCompleted?.Invoke(_measurement.Result);
        }
    }
}
