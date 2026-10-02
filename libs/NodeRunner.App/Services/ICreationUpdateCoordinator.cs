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

    /// <summary>Clears a Creation's training state, invalidating any pending training snapshot.</summary>
    void ResetTraining(Guid id);

    /// <summary>
    /// Saves a Build edit and invalidates any pending training snapshot. A move-only edit (Build
    /// opened the Creation locked, <see cref="Lifecycle.CreationLock"/>) takes only the creature and
    /// keeps the training, which still fits. A full edit takes the creature and drops the training,
    /// since the old genome may not fit the new anatomy; Build only allows it on a Creation with no
    /// finished generation. The caller's mode decides, not the lock
    /// at save time, so a generation saved while Build was open cannot keep a genome that no longer
    /// fits. Returns <c>null</c> if no such Creation exists.
    /// </summary>
    CreationDef? ApplyEdit(Guid id, CreatureDef editedCreature, bool moveOnly);

    /// <summary>Deletes a Creation, invalidating any pending training snapshot.</summary>
    bool Delete(Guid id);

    /// <summary>
    /// Atomically captures and deletes a Creation, invalidating any pending
    /// training snapshot. Returns <c>null</c> if no such Creation exists.
    /// </summary>
    CreationDef? DeleteAndCapture(Guid id);
}
