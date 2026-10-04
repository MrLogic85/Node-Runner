using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The Training shadow strip (#387, reference <c>GenerationStrip</c>): one <see cref="UiBarCell"/>
/// per shadow, its bar the distance so far and the followed shadow selected. Tapping a cell follows
/// that shadow. It has as many places as fit its width, cells keeping their size (#791). With more
/// shadows than places it pages: a "worse" cell first and a sort or "better" cell last
/// (<see cref="ShadowStripPresentation"/>). The scene authors the row and the two buttons; this adds
/// a cell per place whenever the width changes, and refreshes them every frame, because distances
/// change every physics tick. Its root is a plain <see cref="Control"/>, whose width is only what
/// the tray gives it, so a strip full of cells can still shrink.
/// </summary>
public partial class ShadowStrip : Control
{
    private readonly List<UiBarCell> _cells = [];
    private TrainingPresentationViewModel? _training;
    private ShadowStripView _shown = ShadowStripView.Empty;
    private HBoxContainer? _row;

    private HBoxContainer Row => _row ??= GetNode<HBoxContainer>("%Row");

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
                Fit();
            }
        }
    }

    public override void _Ready()
    {
        Worse.Pressed += () => Act(training => training.ShowWorseShadows());
        Trailing.Pressed += OnTrailingPressed;
        Resized += Fit;
        Row.MinimumSizeChanged += UpdateMinimumSize;
        Fit();
    }

    public override void _Process(double delta) => Refresh();

    // As tall as the row and no wider than nothing, so the width stays the tray's.
    public override Vector2 _GetMinimumSize() =>
        IsInsideTree() ? new Vector2(0, Row.GetCombinedMinimumSize().Y) : Vector2.Zero;

    // Keeps a cell per place that fits the width, every place as wide as a button. Until the strip
    // is laid out it has no width, and the training keeps its default places.
    private void Fit()
    {
        if (Size.X <= 0)
        {
            return;
        }

        var separation = Row.GetThemeConstant("separation");
        var pitch = Worse.GetCombinedMinimumSize().X + separation;
        var places = Math.Max((int)((Size.X + separation) / pitch), ShadowStripPresentation.FewestPlaces);
        while (_cells.Count < places)
        {
            AddCell();
        }

        while (_cells.Count > places)
        {
            var cell = _cells[^1];
            _cells.RemoveAt(_cells.Count - 1);
            Row.RemoveChild(cell);
            cell.QueueFree();
        }

        if (_training is not null)
        {
            _training.StripPlaces = places;
        }

        Refresh();
    }

    private void AddCell()
    {
        var place = _cells.Count;
        var cell = new UiBarCell { Name = $"Cell{place + 1}" };
        cell.Pressed += () => FollowAt(place);
        Row.AddChild(cell);
        Row.MoveChild(cell, Trailing.GetIndex());
        _cells.Add(cell);
    }

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
        for (var index = 0; index < _cells.Count; index++)
        {
            var cell = _cells[index];
            cell.Visible = index < _shown.Cells.Count;
            if (!cell.Visible)
            {
                continue;
            }

            var shadow = _shown.Cells[index];
            cell.Fill = (float)shadow.Fill;
            cell.Selected = shadow.IsFollowed;
        }

        Trailing.Visible = _shown.Trailing != ShadowStripTrailing.None;
        Trailing.IconId = _shown.Trailing == ShadowStripTrailing.Better ? UiIconId.ChevronRight : UiIconId.Sort;
        Trailing.TooltipText = _shown.Trailing == ShadowStripTrailing.Better ? "Better shadows" : "Sort by distance";
    }
}
