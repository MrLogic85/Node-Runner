using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// A list of rows whose picked row has an info line right under it (#705, #805), as the Build Links
/// list and Parts tray do. <see cref="ShowInfoUnder"/> moves the scene's <see cref="Info"/> below
/// that row and fades it in when it appears or moves to another row; the owner sets its text.
/// </summary>
[Tool]
[GlobalClass]
public partial class UiPickList : VBoxContainer
{
    private const double _fadeInSeconds = 0.12;

    private Tween? _tween;
    private Control? _shownUnder;

    /// <summary>The info line: a child of this list, hidden while no row is picked.</summary>
    [Export]
    public Control? Info { get; set; }

    /// <summary>Shows <see cref="Info"/> right under <paramref name="row"/>, a child of this list, or hides it for null.</summary>
    public void ShowInfoUnder(Control? row)
    {
        if (Info is null || Info.GetParent() != this)
        {
            GD.PushError($"{Name}: Info must be a child of this list.");
            return;
        }

        if (row is not null && row.GetParent() != this)
        {
            GD.PushError($"{Name}: {row.Name} is not a row of this list; hiding the info line.");
            row = null;
        }

        if (row is null)
        {
            _tween?.Kill();
            _shownUnder = null;
            Info.Visible = false;
            Info.Modulate = Colors.White;
            return;
        }

        var below = row.GetIndex();
        MoveChild(Info, Info.GetIndex() < below ? below : below + 1);
        if (Info.Visible && _shownUnder == row)
        {
            return;
        }

        _shownUnder = row;
        _tween?.Kill();
        Info.Modulate = Colors.Transparent;
        Info.Visible = true;
        _tween = CreateTween();
        _tween.TweenProperty(Info, CanvasItem.PropertyName.Modulate.ToString(), Colors.White, _fadeInSeconds);
    }
}
