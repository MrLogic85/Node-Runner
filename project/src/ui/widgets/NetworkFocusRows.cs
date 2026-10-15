using Godot;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The scrolled part of NetworkFocus (#908): as tall as its rows, so the scene's ScrollContainer scrolls
/// it. <see cref="NetworkFocusView"/> draws into it and reads its taps.
/// </summary>
public partial class NetworkFocusRows : Control
{
    private float _contentHeight;

    public float ContentHeight
    {
        get => _contentHeight;
        set
        {
            if (value != _contentHeight)
            {
                _contentHeight = value;
                UpdateMinimumSize();
            }
        }
    }

    public override Vector2 _GetMinimumSize() => new(0, _contentHeight);
}
