using Godot;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The scrolled part of BrainFocus (#908): as tall as its rows, so the scene's ScrollContainer scrolls
/// it. <see cref="BrainFocusNetworkView"/> draws into it and reads its taps.
/// </summary>
public partial class BrainFocusRows : Control
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
