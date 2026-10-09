using NodeRunner.Domain;

namespace NodeRunner.App.ViewModels;

/// <summary>
/// Build's undo and redo steps for one visit (#689): the body as it was around each finished edit.
/// A change outside an open edit is a step of its own; an open edit, such as a drag, makes all its
/// changes one step when it ends. A step only counts if the body changed. Holds at most
/// <see cref="MaxSteps"/> undo steps, dropping the oldest; a new step clears Redo. A step may name
/// the parts to select again when it is undone (#878), and keeps them through Redo and Undo.
/// </summary>
/// <remarks>
/// Godot's <c>UndoRedo</c> keeps the same two stacks, but it would put Build's step rules in the
/// Godot host, outside the App tests that cover the rest of Build's edits. Here a step is just a
/// body snapshot, so the stacks stay a few lines of plain C#.
/// </remarks>
public sealed class BuildHistory
{
    public const int MaxSteps = 100;

    private readonly Func<CreatureDef> _body;
    private readonly LinkedList<Step> _undo = new();
    private readonly Stack<Step> _redo = new();
    private (object Owner, CreatureDef Before)? _open;

    /// <param name="body">Reads the body as it is now.</param>
    public BuildHistory(Func<CreatureDef> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        _body = body;
    }

    /// <summary>Raised when <see cref="CanUndo"/> or <see cref="CanRedo"/> may have changed.</summary>
    public event EventHandler? Changed;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>
    /// Opens an edit for <paramref name="owner"/> that lasts until its <see cref="End"/>. An edit
    /// another owner left open ends first; opening the owner's own open edit again does nothing.
    /// </summary>
    public void Begin(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (_open is { } open)
        {
            if (ReferenceEquals(open.Owner, owner))
            {
                return;
            }

            End(open.Owner);
        }

        _open = (owner, _body());
    }

    /// <summary>Ends <paramref name="owner"/>'s open edit: one step if the body changed.</summary>
    public void End(object owner)
    {
        if (_open is { } open && ReferenceEquals(open.Owner, owner))
        {
            _open = null;
            Record(open.Before, null);
        }
    }

    /// <summary>Drops <paramref name="owner"/>'s open edit without a step, once the owner has put the body back.</summary>
    public void Cancel(object owner)
    {
        if (_open is { } open && ReferenceEquals(open.Owner, owner))
        {
            _open = null;
        }
    }

    /// <summary>Makes <paramref name="change"/> a step, or part of the open edit.</summary>
    public void Change(Action change)
    {
        ArgumentNullException.ThrowIfNull(change);
        Change(() =>
        {
            change();
            return true;
        });
    }

    /// <summary>
    /// Makes <paramref name="change"/> a step that selects <paramref name="reselect"/> again when it
    /// is undone; inside an open edit it is only part of that edit.
    /// </summary>
    public void Change(Action change, PartSet reselect)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(reselect);
        Change(() =>
        {
            change();
            return true;
        }, reselect);
    }

    /// <inheritdoc cref="Change(Action)"/>
    public T Change<T>(Func<T> change) => Change(change, null);

    private T Change<T>(Func<T> change, PartSet? reselect)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (_open is not null)
        {
            return change();
        }

        var before = _body();
        var result = change();
        Record(before, reselect);
        return result;
    }

    /// <summary>
    /// The body to go back to and the parts to select again, or null when there is none; the body
    /// now becomes the redo step. Does nothing while an edit is open: a second finger can tap Undo
    /// while the first still drags, and the drag would carry on against a body that was swapped
    /// under it.
    /// </summary>
    public Step? Undo()
    {
        if (_open is not null || _undo.Last is not { } last)
        {
            return null;
        }

        _undo.RemoveLast();
        _redo.Push(last.Value with { Body = _body() });
        Changed?.Invoke(this, EventArgs.Empty);
        return last.Value;
    }

    /// <summary>The body to go forward to, or null when there is none; the body now becomes the undo step. Does nothing while an edit is open, as for <see cref="Undo"/>.</summary>
    public CreatureDef? Redo()
    {
        if (_open is not null || !_redo.TryPop(out var after))
        {
            return null;
        }

        Push(after with { Body = _body() });
        Changed?.Invoke(this, EventArgs.Empty);
        return after.Body;
    }

    public void Clear()
    {
        _open = null;
        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Parts only: NextPartId is not part of the body.
    private static bool SameBody(CreatureDef a, CreatureDef b) =>
        a.Nodes.SequenceEqual(b.Nodes)
        && a.Beams.SequenceEqual(b.Beams)
        && a.Sensors.SequenceEqual(b.Sensors)
        && a.Servos.SequenceEqual(b.Servos)
        && a.Pistons.SequenceEqual(b.Pistons)
        && a.Springs.SequenceEqual(b.Springs)
        && a.Wheels.SequenceEqual(b.Wheels);

    private void Record(CreatureDef before, PartSet? reselect)
    {
        if (SameBody(before, _body()))
        {
            return;
        }

        Push(new Step(before, reselect));
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Push(Step step)
    {
        _undo.AddLast(step);
        if (_undo.Count > MaxSteps)
        {
            _undo.RemoveFirst();
        }
    }

    /// <summary>A body to go back to, and the parts to select again there, if any.</summary>
    public sealed record Step(CreatureDef Body, PartSet? Reselect);
}
