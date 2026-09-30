namespace NodeRunner.Ui.Lib;

/// <summary>
/// Sorts the Back signals of Android presses into each press's first signal and its repeats (#506).
/// Godot sends a go-back signal on the Back key-down, on every key repeat while it is held, and
/// again from the system back callback on release. The order varies: a quick tap can send its first
/// go-back before its key-down, and a held key sends its release go-back just before or just after
/// its key-up. Pure, so the sequences recorded on a device replay in tests.
/// </summary>
public sealed class UiBackPressTracker
{
    /// <summary>A release go-back follows its key-up within a few milliseconds.</summary>
    public const ulong ReleaseMsec = 50;

    /// <summary>Without a key-up, a pause this long means the next go-back belongs to a new press.</summary>
    public const ulong PauseMsec = 200;

    private bool _inPress;
    private bool _pressReleased;
    private ulong _releasedMsec;
    private ulong _lastSignalMsec;

    /// <summary>
    /// Records a go-back signal, arriving while the Back key is held or not. True when it is the
    /// first of a new press, false for a repeat.
    /// </summary>
    public bool GoBack(ulong nowMsec, bool backHeld)
    {
        if (!backHeld && _inPress)
        {
            _inPress = _pressReleased
                ? nowMsec - _releasedMsec < ReleaseMsec
                : nowMsec - _lastSignalMsec < PauseMsec;
        }

        var first = !_inPress;
        if (first)
        {
            _inPress = true;
            _pressReleased = false;
        }

        _lastSignalMsec = nowMsec;
        return first;
    }

    /// <summary>Records a Back key event; an echo is a key repeat while the key is held.</summary>
    public void Key(ulong nowMsec, bool pressed, bool echo)
    {
        if (pressed && !echo && _pressReleased)
        {
            // A new press whose key-down comes before its go-back.
            _inPress = false;
        }
        else if (!pressed && _inPress)
        {
            _pressReleased = true;
            _releasedMsec = nowMsec;
        }

        _lastSignalMsec = nowMsec;
    }
}
