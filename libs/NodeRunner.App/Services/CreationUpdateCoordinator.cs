using System.Collections.Concurrent;
using NodeRunner.App.Repositories;
using NodeRunner.Domain;

namespace NodeRunner.App.Services;

/// <inheritdoc cref="ICreationUpdateCoordinator"/>
public sealed class CreationUpdateCoordinator : ICreationUpdateCoordinator
{
    private readonly ICreationRepository _repository;
    private readonly ConcurrentDictionary<Guid, object> _creationLocks = new();
    private readonly ConcurrentDictionary<Guid, long> _trainingEpochs = new();

    public CreationUpdateCoordinator(ICreationRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    public long CurrentTrainingEpoch(Guid id) => _trainingEpochs.GetValueOrDefault(id);

    public CreationDef? UpdateIfPresent(Guid id, Func<CreationDef, CreationDef> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        var creationLock = _creationLocks.GetOrAdd(id, static _ => new object());
        lock (creationLock)
        {
            var source = _repository.Get(id);
            if (source is null)
            {
                return null;
            }

            var updated = update(source);
            if (!ReferenceEquals(updated, source))
            {
                _repository.Save(updated);
            }

            return updated;
        }
    }

    public bool TryPersistTraining(Guid id, long expectedEpoch, TrainingStateDef training)
    {
        ArgumentNullException.ThrowIfNull(training);
        var wrote = false;
        UpdateIfPresent(id, source =>
        {
            if (_trainingEpochs.GetValueOrDefault(id) != expectedEpoch)
            {
                return source;
            }

            if (source.Training is { } current && current.Generation >= training.Generation)
            {
                return source;
            }

            wrote = true;
            return new CreationDef(source.Id, source.Name, source.Creature, training);
        });

        return wrote;
    }

    public void ResetTraining(Guid id)
    {
        var updated = UpdateIfPresent(id, source =>
        {
            BumpTrainingEpoch(id);
            return new CreationDef(source.Id, source.Name, source.Creature);
        });

        if (updated is null)
        {
            // KeyNotFoundException (not InvalidOperationException) so callers
            // treating "the Creation this id points to doesn't exist" as
            // recoverable (#114) can't accidentally also swallow a genuine
            // InvalidOperationException lifecycle bug elsewhere in the call
            // chain (e.g. SaveManager's "not ready" composition-root guard).
            throw new KeyNotFoundException($"Creation '{id}' was not found.");
        }
    }

    public CreationDef? ApplyCreatureEdit(Guid id, CreatureDef editedCreature)
    {
        ArgumentNullException.ThrowIfNull(editedCreature);
        return UpdateIfPresent(id, source =>
        {
            BumpTrainingEpoch(id);
            return new CreationDef(source.Id, source.Name, editedCreature, source.Training);
        });
    }

    public bool Delete(Guid id)
    {
        return DeleteAndCapture(id) is not null;
    }

    public CreationDef? DeleteAndCapture(Guid id)
    {
        // Serialized with UpdateIfPresent (same per-id lock) so a queued
        // training snapshot can never read the Creation, lose the race with
        // a delete, and write it back afterward.
        var creationLock = _creationLocks.GetOrAdd(id, static _ => new object());
        lock (creationLock)
        {
            var deleted = _repository.Get(id);
            if (deleted is null)
            {
                return null;
            }

            BumpTrainingEpoch(id);
            _repository.Delete(id);
            return deleted;
        }
    }

    private void BumpTrainingEpoch(Guid id) => _trainingEpochs.AddOrUpdate(id, 1, static (_, epoch) => epoch + 1);
}
