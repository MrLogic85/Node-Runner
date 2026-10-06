using NodeRunner.Domain;

namespace NodeRunner.App.Services;

/// <summary>
/// Serializes read-modify-write mutations to a <see cref="CreationDef"/> per
/// id, and lets a training snapshot captured before a superseding mutation
/// (reset, creature edit, delete) detect that and discard itself instead of
/// resurrecting stale or incompatible training state. See #113.
/// </summary>
public interface ICreationUpdateCoordinator
{
    /// <summary>
    /// Current training epoch for a Creation. Callers about to capture a
    /// training snapshot for later, out-of-line persistence should read this
    /// first and pass it to <see cref="TryPersistTraining"/>.
    /// </summary>
    long CurrentTrainingEpoch(Guid id);

    /// <summary>
    /// Atomically reads the Creation identified by <paramref name="id"/>,
    /// applies <paramref name="update"/>, and saves the result if it differs
    /// (by reference) from what was read. Returns <c>null</c> without
    /// writing if no such Creation exists.
    /// </summary>
    CreationDef? UpdateIfPresent(Guid id, Func<CreationDef, CreationDef> update);

    /// <summary>
    /// Applies a training snapshot captured under <paramref name="expectedEpoch"/>,
    /// unless the Creation was reset/edited/deleted since (epoch changed) or
    /// a newer-or-equal generation was already persisted by another
    /// snapshot. Returns <c>true</c> only if the write actually happened.
    /// </summary>
    bool TryPersistTraining(Guid id, long expectedEpoch, TrainingStateDef training);

    /// <summary>
    /// Runs <see cref="TryPersistTraining"/> on the thread pool, after any snapshot already queued
    /// for the same Creation, so a generation boundary never waits for file IO (#113). The task
    /// faults if the write throws; whoever queued it reports that.
    /// </summary>
    Task PersistTrainingInBackground(Guid id, long expectedEpoch, TrainingStateDef training);

    /// <summary>
    /// Reads a Creation once every training snapshot queued for it has landed, so a screen that
    /// trusts its lock or training values never reads them early (#370). Returns <c>null</c> if no
    /// such Creation exists.
    /// </summary>
    CreationDef? Get(Guid id);

    /// <summary>Clears a Creation's training state, invalidating any pending training snapshot.</summary>
    void ResetTraining(Guid id);

    /// <summary>
    /// Saves a Build edit and invalidates any pending training snapshot. The training is kept with
    /// its brain refitted to the edited creature's ports (#516, <c>DirectBrain.Refit</c>); generation,
    /// latest and best are kept. When given, <paramref name="openedBrain"/> is refitted rather than
    /// the saved brain (#689, docs/CREATURE_MODEL.md → "Build refits the brain it opened with").
    /// Returns <c>null</c> if no such Creation exists.
    /// </summary>
    CreationDef? ApplyEdit(Guid id, CreatureDef editedCreature, BrainDef? openedBrain = null);

    /// <summary>Deletes a Creation, invalidating any pending training snapshot.</summary>
    bool Delete(Guid id);

    /// <summary>
    /// Atomically captures and deletes a Creation, invalidating any pending
    /// training snapshot. Returns <c>null</c> if no such Creation exists.
    /// </summary>
    CreationDef? DeleteAndCapture(Guid id);
}
