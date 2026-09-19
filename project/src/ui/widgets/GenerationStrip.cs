using Godot;
using NodeRunner.Theme;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// Displays one cell per candidate in the active generation. This is the
/// first visual Watch slice; the surrounding Watch shell and live trial
/// treatment remain future work.
/// </summary>
public partial class GenerationStrip : Control
{
    private double[] _fitness = [];
    private int _currentCandidate;
    private int _populationSize;
    private bool _active;

    public VisualTheme Palette { get; set; } = VisualTheme.Neon;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0, 52);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void SetProgress(
        int generation,
        int currentCandidate,
        int populationSize,
        int completedCandidateCount,
        double[] completedFitness,
        bool active)
    {
        _currentCandidate = currentCandidate;
        _populationSize = populationSize;
        _fitness = completedFitness.Take(Math.Min(completedCandidateCount, completedFitness.Length)).ToArray();
        _active = active;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_populationSize < 1)
        {
            return;
        }

        var gap = 6f;
        var cellWidth = Mathf.Max(24f, (Size.X - (gap * (_populationSize - 1))) / _populationSize);
        var cellHeight = Mathf.Min(32f, Size.Y - 16f);
        var top = (Size.Y - cellHeight) * 0.5f;

        for (var index = 0; index < _populationSize; index++)
        {
            var x = index * (cellWidth + gap);
            var rect = new Rect2(x, top, cellWidth, cellHeight);
            var isCurrent = _active && index + 1 == _currentCandidate;
            var isDone = index < _fitness.Length;
            var beam = Palette.Beam;
            var fill = isDone ? new Color(beam.R, beam.G, beam.B, 0.45f) : Palette.ArenaGrid;
            var border = isCurrent ? Palette.SelectionGlow : Palette.Beam;
            var borderWidth = isCurrent ? 3f : 1f;

            DrawRect(rect, fill, filled: true);
            DrawRect(rect, border, filled: false, width: borderWidth);

            if (isCurrent)
            {
                DrawCircle(rect.GetCenter(), 5, Palette.SelectionGlow);
            }
            else if (isDone)
            {
                // Relative within this generation; this is not the global
                // progression/unlock scale.
                var bestFitness = _fitness.Max();
                var relativeFitness = bestFitness > 0
                    ? Mathf.Clamp((float)(_fitness[index] / bestFitness), 0f, 1f)
                    : 0f;
                DrawRect(
                    new Rect2(rect.Position + new Vector2(4, rect.Size.Y - 8), new Vector2((rect.Size.X - 8) * relativeFitness, 4)),
                    Palette.CoreMarker,
                    filled: true);
            }
            else
            {
                DrawLine(
                    rect.Position + new Vector2(5, rect.Size.Y - 6),
                    rect.End - new Vector2(5, 6),
                    Palette.ArenaGrid,
                    2);
            }
        }
    }
}
