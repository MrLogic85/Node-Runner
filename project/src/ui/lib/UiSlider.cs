using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Canonical normalized slider with one or two thumbs, steps, marker, and disabled state.</summary>
public partial class UiSlider : Control
{
    [Signal]
    public delegate void ThumbChangedEventHandler(int thumbIndex, double position);

    [Signal]
    public delegate void ThumbChangeCommittedEventHandler(int thumbIndex, double position);

    [Signal]
    public delegate void RangeChangedEventHandler(double low, double high);

    private UiTokens _tokens = UiTokens.Neon;
    private string _labelText = "Value";
    private string _readoutText = "50";
    private double[] _thumbs = [0.5];
    private double _fillPosition = 0.5;
    private string[] _stepLabels = [];
    private double _markerPosition = -1;
    private string _markerText = string.Empty;
    private bool _showValueRow = true;
    private bool _enabled = true;
    private UiSliderStyle _style = UiSliderStyle.From(UiTokens.Neon);
    private Label? _label;
    private Label? _readout;
    private Label? _markerLabel;
    private StyleBoxFlat? _thumbStyle;
    private readonly List<Label> _stepLabelNodes = [];
    private int _draggedThumb = -1;
    private int _pressedThumb = -1;
    private Vector2 _pressPosition;

    [Export]
    public string LabelText
    {
        get => _labelText;
        set
        {
            _labelText = value;
            Refresh();
        }
    }

    [Export]
    public string ReadoutText
    {
        get => _readoutText;
        set
        {
            _readoutText = value;
            Refresh();
        }
    }

    [Export]
    public double[] Thumbs
    {
        get => _thumbs;
        set
        {
            _thumbs = UiComponentContracts.NormalizeSliderThumbs(value);
            Refresh();
        }
    }

    [Export(PropertyHint.Range, "0,1,0.001")]
    public double FillPosition
    {
        get => _fillPosition;
        set
        {
            _fillPosition = UiComponentContracts.ClampSliderPosition(value);
            QueueRedraw();
        }
    }

    [Export]
    public string[] StepLabels
    {
        get => _stepLabels;
        set
        {
            _stepLabels = value ?? [];
            if (_stepLabels.Length == 1)
            {
                GD.PushError("Slider steps must be empty or contain at least two labels; ignoring the single label.");
                _stepLabels = [];
            }

            RebuildStepLabels();
            Refresh();
        }
    }

    [Export(PropertyHint.Range, "-1,1,0.001")]
    public double MarkerPosition
    {
        get => _markerPosition;
        set
        {
            _markerPosition = value < 0 ? -1 : UiComponentContracts.ClampSliderPosition(value);
            Refresh();
        }
    }

    [Export]
    public string MarkerText
    {
        get => _markerText;
        set
        {
            _markerText = value;
            Refresh();
        }
    }

    [Export]
    public bool ShowValueRow
    {
        get => _showValueRow;
        set
        {
            _showValueRow = value;
            Refresh();
        }
    }

    [Export]
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value)
            {
                _draggedThumb = -1;
            }

            Refresh();
        }
    }

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            Refresh();
        }
    }

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.None;
        MouseFilter = MouseFilterEnum.Pass;
        Resized += LayoutContent;
        EnsureLabels();
        RebuildStepLabels();
        Refresh();
    }

    public override void _ExitTree()
    {
        Resized -= LayoutContent;
        _draggedThumb = -1;
        _pressedThumb = -1;
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (!Enabled || _thumbs.Length == 0)
        {
            _draggedThumb = -1;
            _pressedThumb = -1;
            return;
        }

        if (PointerInput.TryGetPressPosition(inputEvent, out var pressPosition))
        {
            _pressedThumb = NearestThumb(pressPosition.X);
            _pressPosition = pressPosition;
            return;
        }

        if (_pressedThumb >= 0 && PointerInput.TryGetDragPosition(inputEvent, out var dragPosition))
        {
            var delta = dragPosition - _pressPosition;
            if (_draggedThumb < 0)
            {
                if (Mathf.Abs(delta.Y) > Mathf.Abs(delta.X))
                {
                    _pressedThumb = -1;
                    return;
                }

                _draggedThumb = _pressedThumb;
            }

            SetDraggedThumb(dragPosition.X);
            AcceptEvent();
            return;
        }

        if (PointerInput.TryGetReleasePosition(inputEvent, out _))
        {
            if (_draggedThumb >= 0)
            {
                EmitSignal(SignalName.ThumbChangeCommitted, _draggedThumb, _thumbs[_draggedThumb]);
            }
            else if (_pressedThumb >= 0)
            {
                _draggedThumb = _pressedThumb;
                SetDraggedThumb(_pressPosition.X);
                EmitSignal(SignalName.ThumbChangeCommitted, _draggedThumb, _thumbs[_draggedThumb]);
            }

            _draggedThumb = -1;
            _pressedThumb = -1;
        }
    }

    public override void _Draw()
    {
        var trackLeft = _style.ThumbRadius;
        var trackRight = Mathf.Max(trackLeft, Size.X - _style.ThumbRadius);
        var trackY = TrackY;
        var opacity = Enabled ? 1f : UiSliderStyle.DisabledOpacity;
        var line = UiTokens.MultiplyAlpha(Enabled ? _tokens.Line : _tokens.LineStrong, opacity);
        var accent = UiTokens.MultiplyAlpha(_tokens.Accent, opacity);
        var lowX = _thumbs.Length > 0
            ? PositionFor(_thumbs[0], trackLeft, trackRight)
            : trackLeft;
        var highX = _thumbs.Length switch
        {
            0 => PositionFor(FillPosition, trackLeft, trackRight),
            2 => PositionFor(_thumbs[1], trackLeft, trackRight),
            _ => lowX,
        };
        var fillStart = _thumbs.Length == 2 ? lowX : trackLeft;

        if (Enabled)
        {
            DrawLine(new Vector2(trackLeft, trackY), new Vector2(trackRight, trackY), line, _style.TrackWidth, antialiased: true);
        }
        else
        {
            DrawDisabledTrackSegment(trackLeft, fillStart, trackY, line);
            DrawDisabledTrackSegment(highX, trackRight, trackY, line);
        }

        DrawSteps(trackLeft, trackRight, trackY, opacity);
        DrawLine(new Vector2(fillStart, trackY), new Vector2(highX, trackY), accent, _style.TrackWidth, antialiased: true);

        if (HasMarker)
        {
            var markerX = PositionFor(MarkerPosition, trackLeft, trackRight);
            DrawLine(
                new Vector2(markerX, trackY - _style.MarkerHalfHeight),
                new Vector2(markerX, trackY + _style.MarkerHalfHeight),
                UiTokens.MultiplyAlpha(_tokens.Halo, opacity),
                _tokens.StrokeSignal,
                antialiased: true);
        }

        foreach (var thumb in _thumbs)
        {
            DrawThumb(new Vector2(PositionFor(thumb, trackLeft, trackRight), trackY), accent);
        }

    }

    private float TrackY => ShowValueRow
        ? _tokens.OverlineText.LineHeight + _tokens.Space3
        : Size.Y * 0.5f;

    private void EnsureLabels()
    {
        if (_label is not null)
        {
            return;
        }

        _label = CreateLabel();
        _readout = CreateLabel();
        _markerLabel = CreateLabel();
        AddChild(_label);
        AddChild(_readout);
        AddChild(_markerLabel);
    }

    private Label CreateLabel() =>
        new()
        {
            MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.Off,
        };

    private void RebuildStepLabels()
    {
        if (!IsInsideTree())
        {
            return;
        }

        foreach (var label in _stepLabelNodes)
        {
            label.QueueFree();
        }

        _stepLabelNodes.Clear();
        foreach (var text in StepLabels)
        {
            var label = CreateLabel();
            label.Text = text;
            AddChild(label);
            _stepLabelNodes.Add(label);
        }
    }

    private void Refresh()
    {
        if (!IsInsideTree())
        {
            return;
        }

        EnsureLabels();
        _style = UiSliderStyle.From(_tokens);
        var hasSteps = StepLabels.Length >= 2;
        CustomMinimumSize = new Vector2(
            0,
            ShowValueRow
                ? hasSteps ? _style.SteppedHeight : _tokens.TouchTarget
                : _style.TrackWidth);

        _label!.Text = LabelText;
        _readout!.Text = ReadoutText;
        _markerLabel!.Text = MarkerText;
        _label.Visible = ShowValueRow;
        _readout.Visible = ShowValueRow;
        _markerLabel.Visible = ShowValueRow && HasMarker && !string.IsNullOrWhiteSpace(MarkerText);

        ApplyLabelStyle(_label, _tokens.OverlineText, _tokens.Muted);
        ApplyLabelStyle(_readout, _tokens.ReadoutMediumText, _tokens.Ink);
        ApplyLabelStyle(_markerLabel, _tokens.ReadoutSmallText, _tokens.Halo);
        _thumbStyle = _tokens.ControlStyle(
            _tokens.Accent,
            Colors.Transparent,
            borderWidth: 0,
            radius: _style.ThumbRadius);
        UiGlow.ApplyToControl(_thumbStyle, _tokens.AccentGlow, _tokens.EffectsEnabled);
        foreach (var stepLabel in _stepLabelNodes)
        {
            ApplyLabelStyle(stepLabel, _tokens.ReadoutSmallText, _tokens.Muted);
        }

        LayoutContent();
        QueueRedraw();
    }

    private void ApplyLabelStyle(Label label, UiTokens.TextStyle style, Color color)
    {
        _tokens.ApplyTextStyle(label, style);
        label.AddThemeColorOverride(
            "font_color",
            UiTokens.MultiplyAlpha(color, Enabled ? 1f : UiSliderStyle.DisabledOpacity));
    }

    private void LayoutContent()
    {
        if (_label is null || _readout is null || _markerLabel is null)
        {
            return;
        }

        _label.ResetSize();
        _readout.ResetSize();
        _markerLabel.ResetSize();
        _label.Position = Vector2.Zero;
        _readout.Position = new Vector2(Mathf.Max(0, Size.X - _readout.Size.X), 0);

        if (_markerLabel.Visible)
        {
            var desiredCenter = Mathf.Lerp(
                _style.ThumbRadius,
                Size.X - _style.ThumbRadius,
                (float)MarkerPosition);
            var minimumX = _label.Size.X + _tokens.Space1;
            var maximumX = _readout.Position.X - _tokens.Space1 - _markerLabel.Size.X;
            var markerX = maximumX >= minimumX
                ? Mathf.Clamp(desiredCenter - (_markerLabel.Size.X * 0.5f), minimumX, maximumX)
                : minimumX;
            _markerLabel.Position = new Vector2(markerX, 0);
        }

        if (_stepLabelNodes.Count < 2)
        {
            return;
        }

        var labelY = TrackY + _tokens.Space2;
        for (var index = 0; index < _stepLabelNodes.Count; index++)
        {
            var label = _stepLabelNodes[index];
            label.ResetSize();
            var position = index / (float)(_stepLabelNodes.Count - 1);
            var center = Mathf.Lerp(_style.ThumbRadius, Size.X - _style.ThumbRadius, position);
            var x = index switch
            {
                0 => _style.ThumbRadius,
                _ when index == _stepLabelNodes.Count - 1 => Size.X - _style.ThumbRadius - label.Size.X,
                _ => center - (label.Size.X * 0.5f),
            };
            label.Position = new Vector2(Mathf.Max(0, x), labelY);
        }
    }

    private void DrawSteps(float trackLeft, float trackRight, float trackY, float opacity)
    {
        if (StepLabels.Length < 2)
        {
            return;
        }

        var color = UiTokens.MultiplyAlpha(_tokens.LineStrong, opacity);
        for (var index = 0; index < StepLabels.Length; index++)
        {
            var x = Mathf.Lerp(trackLeft, trackRight, index / (float)(StepLabels.Length - 1));
            DrawLine(
                new Vector2(x, trackY - _style.StepTickHalfHeight),
                new Vector2(x, trackY + _style.StepTickHalfHeight),
                color,
                _tokens.StrokeHair);
        }
    }

    private void DrawDisabledTrackSegment(float startX, float endX, float trackY, Color color)
    {
        if (endX <= startX)
        {
            return;
        }

        DrawDashedLine(
            new Vector2(startX, trackY),
            new Vector2(endX, trackY),
            color,
            _style.TrackWidth,
            _style.DisabledDashLength,
            antialiased: true);
    }

    private void DrawThumb(Vector2 center, Color color)
    {
        if (Enabled)
        {
            var thumbRect = new Rect2(
                center - new Vector2(_style.ThumbRadius, _style.ThumbRadius),
                new Vector2(_style.ThumbRadius * 2, _style.ThumbRadius * 2));
            DrawStyleBox(_thumbStyle!, thumbRect);
            return;
        }

        DrawCircle(
            center,
            _style.ThumbRadius - _style.DisabledThumbInset,
            UiTokens.MultiplyAlpha(_tokens.PanelRaised, UiSliderStyle.DisabledOpacity));
        for (var index = 0; index < UiSliderStyle.DisabledThumbSegments; index += 2)
        {
            DrawArc(
                center,
                _style.ThumbRadius,
                Mathf.Tau * index / UiSliderStyle.DisabledThumbSegments,
                Mathf.Tau * (index + 1) / UiSliderStyle.DisabledThumbSegments,
                UiSliderStyle.DisabledThumbArcPoints,
                color,
                _tokens.StrokeHair,
                antialiased: true);
        }
    }

    private int NearestThumb(float x)
    {
        if (_thumbs.Length == 1)
        {
            return 0;
        }

        var trackLeft = _style.ThumbRadius;
        var trackRight = Mathf.Max(trackLeft, Size.X - _style.ThumbRadius);
        var position = trackRight <= trackLeft
            ? 0
            : UiComponentContracts.ClampSliderPosition((x - trackLeft) / (trackRight - trackLeft));
        return UiComponentContracts.SelectSliderThumb(_thumbs, position);
    }

    private void SetDraggedThumb(float x)
    {
        if (_draggedThumb < 0)
        {
            return;
        }

        var trackLeft = _style.ThumbRadius;
        var trackRight = Mathf.Max(trackLeft, Size.X - _style.ThumbRadius);
        var position = trackRight <= trackLeft
            ? 0
            : UiComponentContracts.ClampSliderPosition((x - trackLeft) / (trackRight - trackLeft));
        if (_thumbs.Length == 2)
        {
            position = _draggedThumb == 0
                ? Math.Min(position, _thumbs[1])
                : Math.Max(position, _thumbs[0]);
        }

        if (Mathf.IsEqualApprox((float)_thumbs[_draggedThumb], (float)position))
        {
            return;
        }

        _thumbs[_draggedThumb] = position;
        QueueRedraw();
        EmitSignal(SignalName.ThumbChanged, _draggedThumb, position);
        if (_thumbs.Length == 2)
        {
            EmitSignal(SignalName.RangeChanged, _thumbs[0], _thumbs[1]);
        }
    }

    private static float PositionFor(double position, float left, float right) =>
        Mathf.Lerp(left, right, (float)UiComponentContracts.ClampSliderPosition(position));

    private bool HasMarker => MarkerPosition >= 0;
}
