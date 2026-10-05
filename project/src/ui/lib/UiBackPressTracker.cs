namespace NodeRunner.Ui.Lib;

/// <summary>
/// Lets one go-back signal through per Android Back press (#506, #838). Godot sends one when the Back
/// key goes down, one on every key repeat, and one from the system back callback on release
/// (godotengine/godot#123454). So each Back key-down gets one go-back. A go-back with no Back key near
/// it comes from a gesture, which sends no key events, and counts on its own. Godot sends the
/// key-down's go-back before the key event, so a key-down just after a taken go-back belongs to it.
/// Pure, so sequences recorded on a device replay in tests.
/// </summary>
public sealed class UiBackPressTracker
{
    /// <summary>A go-back and the Back key event it belongs to arrive within this.</summary>
    public const ulong PairMsec = 200;

    private ulong? _lastKeyMsec;
    private ulong? _keylessTakenMsec;
    private bool _pressTaken = true;

    /// <summary>
    /// Records a go-back signal, arriving while the Back key is held or not. True when it is the
    /// first of a new press, false for a repeat.
    /// </summary>
    public bool GoBack(ulong nowMsec, bool backHeld)
    {
        if (backHeld || Near(_lastKeyMsec, nowMsec))
        {
            var first = !_pressTaken;
            _pressTaken = true;
            return first;
        }

        _keylessTakenMsec = nowMsec;
        return true;
    }

    /// <summary>Records a Back key event; an echo is a key repeat while the key is held.</summary>
    public void Key(ulong nowMsec, bool pressed, bool echo)
    {
        if (pressed && !echo)
        {
            _pressTaken = Near(_keylessTakenMsec, nowMsec);
            _keylessTakenMsec = null;
        }

        _lastKeyMsec = nowMsec;
    }

    private static bool Near(ulong? thenMsec, ulong nowMsec) => thenMsec is { } then && nowMsec - then < PairMsec;
}
