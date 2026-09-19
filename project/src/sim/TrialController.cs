using Godot;

namespace NodeRunner.Sim;

/// <summary>
/// Runs a single creature through fixed-duration trials, resetting its pose
/// between runs and scoring each trial with an <see cref="Evaluator"/>.
///
/// This node does not own the creature's lifecycle (creation/destruction) or
/// brain assignment — callers (Main.cs today, Population/Evolver later) are
/// responsible for that. TrialController only knows how to time a trial and
/// measure how far the creature got.
/// </summary>
public partial class TrialController : Node
{
    private readonly Evaluator _evaluator = new();
    private Creature.Creature? _creature;
    private int _elapsedTicks;

    /// <summary>Trial length in physics ticks. Default is 10s at 60Hz.</summary>
    public int TrialDurationTicks { get; set; } = 600;

    public bool IsRunning { get; private set; }

    public int ElapsedTicks => _elapsedTicks;

    public float Fitness => _evaluator.Fitness;

    /// <summary>Raised when a trial finishes, with the final fitness score.</summary>
    public event Action<float>? TrialCompleted;

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
    /// Resets the given creature to its built pose and starts a fresh trial
    /// for it. Replaces any trial already in progress.
    /// </summary>
    public void StartTrial(Creature.Creature creature)
    {
        ArgumentNullException.ThrowIfNull(creature);

        _creature = creature;
        _creature.ResetPose();
        _evaluator.Reset(_creature.CenterOfMass.X);
        _elapsedTicks = 0;
        IsRunning = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsRunning || _creature is null)
        {
            return;
        }

        _elapsedTicks++;
        _evaluator.Record(_creature.CenterOfMass.X);

        if (_elapsedTicks >= TrialDurationTicks)
        {
            IsRunning = false;
            TrialCompleted?.Invoke(_evaluator.Fitness);
        }
    }
}
