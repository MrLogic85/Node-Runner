using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The Training shadow strip (#387, reference <c>GenerationStrip</c>): one <see cref="UiBarCell"/>
/// per shadow, its bar the distance so far, the leader's bar <c>accent</c> and the followed shadow
/// ringed. Tapping a cell follows that shadow. Past eight shadows the strip pages: a "worse" cell
/// first and a sort or "better" cell last (<see cref="ShadowStripPresentation"/>). The scene
/// authors the cells; this binds them to the training and refreshes them every frame, because
/// distances change every physics tick.
/// </summary>
public partial class ShadowStrip : HBoxContainer
{
    private TrainingPresentationViewModel? _training;
    private UiBarCell[] _cells = [];
    private ShadowStripView _shown = ShadowStripView.Empty;

    private UiBarCell Worse => GetNode<UiBarCell>("%Worse");

    private UiBarCell Trailing => GetNode<UiBarCell>("%Trailing");

    /// <summary>The training whose shadows the strip shows and follows.</summary>
    public TrainingPresentationViewModel? Presentation
    {
        get => _training;
        set
        {
            _training = value;
            if (IsNodeReady())
            {
                Refresh();
            }
        }
    }

    /// <summary>
    /// The ring on a cell: <c>accent</c> when the followed shadow also leads, as following the
    /// leader needs nothing else marked, and <c>halo</c> when the player follows another one.
    /// </summary>
    public static UiBarCellRing RingFor(ShadowStripCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        return !cell.IsFollowed ? UiBarCellRing.None
            : cell.IsLeader ? UiBarCellRing.Selected
            : UiBarCellRing.Picked;
    }

    public override void _Ready()
    {
        _cells = Enumerable.Range(1, ShadowStripPresentation.Places)
            .Select(number => GetNode<UiBarCell>($"%Cell{number}"))
            .ToArray();
        for (var index = 0; index < _cells.Length; index++)
        {
            var place = index;
            _cells[index].Pressed += () => FollowAt(place);
        }

        Worse.Pressed += () => Act(training => training.ShowWorseShadows());
        Trailing.Pressed += OnTrailingPressed;
        Refresh();
    }

    public override void _Process(double delta) => Refresh();

    private void FollowAt(int place)
    {
        if (place < _shown.Cells.Count)
        {
            var number = _shown.Cells[place].Number;
            Act(training => training.Follow(number));
        }
    }

    private void OnTrailingPressed()
    {
        switch (_shown.Trailing)
        {
            case ShadowStripTrailing.Sort:
                Act(training => training.SortShadows());
                break;
            case ShadowStripTrailing.Better:
                Act(training => training.ShowBetterShadows());
                break;
        }
    }

    private void Act(Action<TrainingPresentationViewModel> action)
    {
        if (_training is not null)
        {
            action(_training);
            Refresh();
        }
    }

    private void Refresh()
    {
        _shown = _training?.Strip ?? ShadowStripView.Empty;
        Worse.Visible = _shown.Pages;
        Worse.Disabled = !_shown.CanPageWorse;
        for (var index = 0; index < _cells.Length; index++)
        {
            var cell = _cells[index];
            cell.Visible = index < _shown.Cells.Count;
            if (!cell.Visible)
            {
                continue;
            }

            var shadow = _shown.Cells[index];
            cell.Fill = (float)shadow.Fill;
            cell.Bright = shadow.IsLeader;
            cell.Ring = RingFor(shadow);
        }

        Trailing.Visible = _shown.Trailing != ShadowStripTrailing.None;
        Trailing.IconId = _shown.Trailing == ShadowStripTrailing.Better ? UiIconId.ChevronRight : UiIconId.Sort;
        Trailing.TooltipText = _shown.Trailing == ShadowStripTrailing.Better ? "Better shadows" : "Sort by distance";
    }
}
