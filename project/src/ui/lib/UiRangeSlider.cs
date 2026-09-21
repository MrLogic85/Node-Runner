using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Two-thumb range slider whose limits always contain its current-value mark.</summary>
public partial class UiRangeSlider : Control
{
    [Signal]
    public delegate void RangeChangedEventHandler(double low, double high);

    private const float _trackY = 34;
    private const float _thumbRadius = 9;

    private UiTokens _tokens = UiTokens.Neon;
    private double _minimum = -90;
    private double _maximum = 90;
    private double _low = -35;
    private double _high = 60;
    private double _mark = 10;
    private bool _disabled;
    private DragTarget _dragTarget;

    private enum DragTarget
    {
        _None,
        _Low,
        _High,
    }

    [Export]
    public string LabelText { get; set; } = "Angle limits";

    [Export]
    public double Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            NormalizeRange();
        }
    }

    [Export]
    public double Maximum
    {
        get => _maximum;
        set
        {
            _maximum = value;
            NormalizeRange();
        }
    }

    [Export]
    public double Low
    {
        get => _low;
        set => SetRange(value, _high, emit: false);
    }

    [Export]
    public double High
    {
        get => _high;
        set => SetRange(_low, value, emit: false);
    }

    [Export]
    public double Mark
    {
        get => _mark;
        set
        {
            _mark = value;
            NormalizeRange();
        }
    }

    [Export]
    public string MarkLabel { get; set; } = "now";

    [Export]
    public bool Disabled
    {
        get => _disabled;
        set
        {
            _disabled = value;
            QueueRedraw();
        }
    }

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(0, _tokens.TouchTarget + _tokens.Space4);
        MouseFilter = MouseFilterEnum.Pass;
        NormalizeRange();
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (Disabled)
        {
            return;
        }

        if (PointerInput.TryGetPressPosition(inputEvent, out var pressPosition))
        {
            _dragTarget = TargetAt(pressPosition);
            if (_dragTarget != DragTarget._None)
            {
                AcceptEvent();
            }

            return;
        }

        if (_dragTarget != DragTarget._None && PointerInput.TryGetDragPosition(inputEvent, out var dragPosition))
        {
            SetDraggedValue(dragPosition.X);
            AcceptEvent();
            return;
        }

        if (PointerInput.TryGetReleasePosition(inputEvent, out _))
        {
            _dragTarget = DragTarget._None;
        }
    }

    public override void _Draw()
    {
        var range = UiComponentContracts.ClampRange(Low, High, Minimum, Maximum, Mark);
        var mark = UiComponentContracts.ClampValue(Mark, Minimum, Maximum);
        var ink = Disabled ? _tokens.Muted : _tokens.Ink;
        var accent = Disabled ? _tokens.LineStrong : _tokens.Accent;
        var fill = Disabled ? _tokens.Line : _tokens.AccentSoft;
        var y = _trackY;

        DrawString(ThemeDB.FallbackFont, new Vector2(0, 13), LabelText, HorizontalAlignment.Left, Size.X, (int)_tokens.LabelText.FontSize, _tokens.Muted);
        DrawRect(new Rect2(0, y - 3, Size.X, 6), _tokens.Line, filled: true);

        var lowX = PositionFor(range.Low);
        var highX = PositionFor(range.High);
        var markX = PositionFor(mark);
        DrawRect(new Rect2(lowX, y - 3, highX - lowX, 6), fill, filled: true);
        DrawCircle(new Vector2(lowX, y), _thumbRadius, accent);
        DrawCircle(new Vector2(highX, y), _thumbRadius, accent);
        DrawColoredPolygon([new Vector2(markX, y - 15), new Vector2(markX + 6, y - 9), new Vector2(markX, y - 3), new Vector2(markX - 6, y - 9)], _tokens.Halo);
        DrawString(ThemeDB.FallbackFont, new Vector2(markX - 28, y + 24), $"{MarkLabel} {mark:0}°", HorizontalAlignment.Center, 56, (int)_tokens.ReadoutSmallText.FontSize, Disabled ? _tokens.Muted : _tokens.Halo);
        DrawString(ThemeDB.FallbackFont, new Vector2(0, y + 24), $"{range.Low:0}°", HorizontalAlignment.Left, 40, (int)_tokens.ReadoutSmallText.FontSize, ink);
        DrawString(ThemeDB.FallbackFont, new Vector2(Size.X - 40, y + 24), $"{range.High:0}°", HorizontalAlignment.Right, 40, (int)_tokens.ReadoutSmallText.FontSize, ink);
    }

    private DragTarget TargetAt(Vector2 position)
    {
        if (Mathf.Abs(position.Y - _trackY) > _tokens.TouchTarget * 0.5f)
        {
            return DragTarget._None;
        }

        var lowDistance = Mathf.Abs(position.X - PositionFor(Low));
        var highDistance = Mathf.Abs(position.X - PositionFor(High));
        return lowDistance <= highDistance ? DragTarget._Low : DragTarget._High;
    }

    private void SetDraggedValue(float position)
    {
        var value = ValueFor(position);
        SetRange(
            _dragTarget == DragTarget._Low ? value : Low,
            _dragTarget == DragTarget._High ? value : High,
            emit: true);
    }

    private void SetRange(double low, double high, bool emit)
    {
        var range = UiComponentContracts.ClampRange(low, high, Minimum, Maximum, Mark);
        var changed = !Mathf.IsEqualApprox((float)_low, (float)range.Low) ||
                      !Mathf.IsEqualApprox((float)_high, (float)range.High);
        _low = range.Low;
        _high = range.High;
        QueueRedraw();
        if (emit && changed)
        {
            EmitSignal(SignalName.RangeChanged, _low, _high);
        }
    }

    private void NormalizeRange()
    {
        if (_minimum > _maximum)
        {
            (_minimum, _maximum) = (_maximum, _minimum);
        }

        SetRange(_low, _high, emit: false);
    }

    private float PositionFor(double value)
    {
        var span = Maximum - Minimum;
        if (Math.Abs(span) < double.Epsilon)
        {
            return Size.X * 0.5f;
        }

        var percent = (float)((UiComponentContracts.ClampValue(value, Minimum, Maximum) - Minimum) / span);
        return percent * Size.X;
    }

    private double ValueFor(float position)
    {
        if (Size.X <= 0 || Math.Abs(Maximum - Minimum) < double.Epsilon)
        {
            return Minimum;
        }

        return UiComponentContracts.ClampValue(Minimum + ((position / Size.X) * (Maximum - Minimum)), Minimum, Maximum);
    }
}
