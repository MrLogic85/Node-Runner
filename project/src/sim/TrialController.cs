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
    private readonly TrialMeasurement _measurement = new(Engine.PhysicsTicksPerSecond);
    private Creature.Creature? _creature;
    private int _elapsedTicks;
    private bool _startsNextTick;

    /// <summary>Trial length in physics ticks. Default is 10s at 60Hz.</summary>
    public int TrialDurationTicks { get; set; } = 600;

    public bool IsRunning { get; private set; }

    public int ElapsedTicks => _elapsedTicks;

    /// <summary>
    /// Physics ticks left in the trial in progress (#715): the full length until it has begun, 0 once
    /// it is over. Meaningless with an endless <see cref="TrialDurationTicks"/>.
    /// </summary>
    public int TicksLeft => !IsRunning ? 0 : _startsNextTick ? TrialDurationTicks : Math.Max(TrialDurationTicks - _elapsedTicks, 0);

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

    /// <summary>Raised when a trial has begun, with the creature in its start pose.</summary>
    public event Action? TrialStarted;

    /// <summary>Raised when a trial finishes, with what it measured; its centre's distance is the fitness.</summary>
    public event Action<TrialResult>? TrialCompleted;

    public override void _Ready()
    {
        // Must run its _PhysicsProcess before any Creature's (default
        // priority 0), so a trial always begins before its creature drives
        // that tick, whether it started inside a tick or between ticks
        // (docs/TRAINING_LOOP.md, "The same brain runs the same trial").
        ProcessPhysicsPriority = -100;
    }

    /// <summary>
    /// Starts a fresh trial for the given creature, reset to its built pose
    /// <see cref="StartClearance"/> above the ground. Replaces any trial already in progress.
    /// Called inside a physics tick, as <see cref="Evolver"/> starts the next trial, it begins at
    /// once; called between ticks, as a scene starts training, it begins at the start of the next
    /// tick (#798). Either way it begins before any creature drives that tick, so the same brain
    /// runs the same trial every time. <see cref="TrialStarted"/> says when it has begun.
    /// </summary>
    public void StartTrial(Creature.Creature creature)
    {
        ArgumentNullException.ThrowIfNull(creature);

        _creature = creature;
        IsRunning = true;
        _startsNextTick = !Engine.IsInPhysicsFrame();
        if (!_startsNextTick)
        {
            Begin();
        }
    }

    private void Begin()
    {
        _creature!.ResetPose(GroundTopY - StartClearance);
        _measurement.Reset(_creature.CenterOfMass.X, _creature.Bounds.End.X);
        _elapsedTicks = 0;
        TrialStarted?.Invoke();
    }

    /// <summary>Stops the current trial without raising <see cref="TrialCompleted"/>.</summary>
    public void Stop()
    {
        IsRunning = false;
        _startsNextTick = false;
        _creature = null;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsRunning || _creature is null)
        {
            return;
        }

        if (_startsNextTick)
        {
            _startsNextTick = false;
            Begin();
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
