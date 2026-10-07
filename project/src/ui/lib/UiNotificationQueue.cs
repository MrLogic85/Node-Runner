namespace NodeRunner.Ui.Lib;

/// <summary>What <see cref="UiNotificationQueue.Offer"/> did with a notification.</summary>
public enum UiNotificationOffer
{
    /// <summary>It waits for its turn.</summary>
    Queued,

    /// <summary>It waits, and the showing notification is replaceable, so it should close now.</summary>
    ReplacesCurrent,

    /// <summary>Its <see cref="UiNotificationSpec.Id"/> is showing; the showing one should start its lifetime again.</summary>
    RepeatsCurrent,

    /// <summary>Its <see cref="UiNotificationSpec.Id"/> is already waiting; it was dropped.</summary>
    AlreadyWaiting,
}

/// <summary>
/// The order <see cref="UiNotification"/> shows notifications in, kept free of the engine so the
/// rules are testable: one Id at a time, and a replaceable notification gives way to any newer one.
/// </summary>
public sealed class UiNotificationQueue
{
    private readonly List<UiNotificationSpec> _waiting = [];

    /// <summary>The notification on screen, set by <see cref="TryAdvance"/>.</summary>
    public UiNotificationSpec? Current { get; private set; }

    public IReadOnlyList<UiNotificationSpec> Waiting => _waiting;

    public UiNotificationOffer Offer(UiNotificationSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (spec.Id is { } id)
        {
            if (Current?.Id == id)
            {
                return UiNotificationOffer.RepeatsCurrent;
            }
            if (_waiting.Exists(waiting => waiting.Id == id))
            {
                return UiNotificationOffer.AlreadyWaiting;
            }
        }
        _waiting.RemoveAll(waiting => waiting.Replaceable);
        _waiting.Add(spec);
        return Current?.Replaceable == true ? UiNotificationOffer.ReplacesCurrent : UiNotificationOffer.Queued;
    }

    /// <summary>Puts <paramref name="spec"/> first in line, ahead of every rule; used for "Action failed".</summary>
    public void PushFront(UiNotificationSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        _waiting.Insert(0, spec);
    }

    /// <summary>Makes the next waiting notification <see cref="Current"/>; false when none waits.</summary>
    public bool TryAdvance([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out UiNotificationSpec? spec)
    {
        if (_waiting.Count == 0)
        {
            spec = null;
            return false;
        }
        spec = _waiting[0];
        _waiting.RemoveAt(0);
        Current = spec;
        return true;
    }

    /// <summary>The showing notification closed.</summary>
    public void CloseCurrent() => Current = null;

    public void Clear()
    {
        _waiting.Clear();
        Current = null;
    }
}
