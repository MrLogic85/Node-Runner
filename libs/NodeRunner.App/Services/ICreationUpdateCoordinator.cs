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
    /// Replaces a Creation's <see cref="CreatureDef"/>, preserving its
    /// current training state, and invalidates any pending training
    /// snapshot (an edit can change brain topology, making an
    /// in-flight genome incompatible). Returns <c>null</c> if no such
    /// Creation exists.
    /// </summary>
    CreationDef? ApplyCreatureEdit(Guid id, CreatureDef editedCreature);

    /// <summary>Deletes a Creation, invalidating any pending training snapshot.</summary>
    bool Delete(Guid id);

    /// <summary>
    /// Atomically captures and deletes a Creation, invalidating any pending
    /// training snapshot. Returns <c>null</c> if no such Creation exists.
    /// </summary>
    CreationDef? DeleteAndCapture(Guid id);
}
