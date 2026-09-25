using Godot;

namespace NodeRunner.Ui.Lib;

public enum UiSliderValueKind
{
    Thumb,
    Range,
    Progress,
}

public readonly record struct UiSliderValue
{
    private UiSliderValue(UiSliderValueKind kind, double low, double high)
    {
        Kind = kind;
        Low = kind == UiSliderValueKind.Range
            ? UiComponentContracts.ClampSliderPosition(low)
            : 0;
        High = UiComponentContracts.ClampSliderPosition(high);
        if (Kind == UiSliderValueKind.Range && High < Low)
        {
            (Low, High) = (High, Low);
        }
    }

    public UiSliderValueKind Kind { get; }

    public double Low { get; }

    public double High { get; }

    public int ThumbCount => Kind switch
    {
        UiSliderValueKind.Progress => 0,
        UiSliderValueKind.Range => 2,
        _ => 1,
    };

    public double FillStart => Kind == UiSliderValueKind.Range ? Low : 0;

    public double FillEnd => High;

    public static UiSliderValue Progress(double value) =>
        new(UiSliderValueKind.Progress, 0, value);

    public static UiSliderValue Thumb(double value) =>
        new(UiSliderValueKind.Thumb, 0, value);

    public static UiSliderValue Thumbs(double low, double high) =>
        new(UiSliderValueKind.Range, low, high);

    public static UiSliderValue From(UiSliderValueKind kind, double low, double high) =>
        kind switch
        {
            UiSliderValueKind.Progress => Progress(high),
            UiSliderValueKind.Range => Thumbs(low, high),
            _ => Thumb(high),
        };

    public double ThumbAt(int index) =>
        index switch
        {
            0 when Kind == UiSliderValueKind.Thumb => High,
            0 when Kind == UiSliderValueKind.Range => Low,
            1 when ThumbCount > 1 => High,
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, null),
        };

    public int SelectThumb(double position)
    {
        if (ThumbCount == 0)
        {
            return -1;
        }

        if (ThumbCount == 1)
        {
            return 0;
        }

        var target = UiComponentContracts.ClampSliderPosition(position);
        if (Low == High)
        {
            return target < Low ? 0 : 1;
        }

        var lowDistance = Math.Abs(target - Low);
        var highDistance = Math.Abs(target - High);
        return lowDistance <= highDistance ? 0 : 1;
    }

    public UiSliderValue WithThumb(int index, double position) =>
        Kind switch
        {
            UiSliderValueKind.Thumb when index == 0 => Thumb(position),
            UiSliderValueKind.Range when index == 0 => Thumbs(Math.Min(position, High), High),
            UiSliderValueKind.Range when index == 1 => Thumbs(Low, Math.Max(position, Low)),
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, null),
        };
}

/// <summary>Canonical normalized slider with one or two thumbs, steps, marker, and disabled state.</summary>
[Tool]
[GlobalClass]
public partial class UiSlider : Control, ISerializationListener
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
    private UiSliderValueKind _valueKind = UiSliderValueKind.Thumb;
    private double _lowPosition = 0.5;
    private double _highPosition = 0.5;
    private string[] _stepLabels = [];
    private double _markerPosition = -1;
    private string _markerText = string.Empty;
    private bool _disabled;
    private UiSliderStyle _style = UiSliderStyle.From(UiTokens.Neon);
    private HBoxContainer? _header;
    private Label? _label;
    private Label? _readout;
    private Label? _markerLabel;
    private StyleBoxFlat? _thumbStyle;
    private StyleBoxFlat? _trackSegmentStyle;
    private readonly List<Label> _stepLabelNodes = [];
    private int _draggedThumb = -1;
    private int _pressedThumb = -1;
    private Vector2 _pressPosition;
    // Object/method callables survive assembly reloads without retaining managed delegates.
    private Callable HeaderSortCallback => new(this, MethodName.LayoutContent);

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

    public UiSliderValue Value
    {
        get => UiSliderValue.From(ValueKind, LowPosition, HighPosition);
        set => SetValue(value);
    }

    [Export]
    public UiSliderValueKind ValueKind
    {
        get => _valueKind;
        set
        {
            if (_valueKind == value)
            {
                return;
            }

            _valueKind = value;
            if (value == UiSliderValueKind.Range && IsInsideTree())
            {
                NormalizeRangePositions();
            }
            NotifyPropertyListChanged();
            QueueRedraw();
        }
    }

    [Export(PropertyHint.Range, "0,1,0.001")]
    public double LowPosition
    {
        get => _lowPosition;
        set
        {
            _lowPosition = UiComponentContracts.ClampSliderPosition(value);
            NormalizeRangePositionsIfReady();
            QueueRedraw();
        }
    }

    [Export(PropertyHint.Range, "0,1,0.001")]
    public double HighPosition
    {
        get => _highPosition;
        set
        {
            _highPosition = UiComponentContracts.ClampSliderPosition(value);
            NormalizeRangePositionsIfReady();
            QueueRedraw();
        }
    }

    public override void _ValidateProperty(Godot.Collections.Dictionary property)
    {
        if (property["name"].AsString() == PropertyName.LowPosition && ValueKind != UiSliderValueKind.Range)
        {
            var usage = (PropertyUsageFlags)property["usage"].AsInt64();
            property["usage"] = (long)(usage & ~(PropertyUsageFlags.Editor | PropertyUsageFlags.Storage));
        }
    }

    [Export]
    public string[] StepLabels
    {
        get => _stepLabels;
        set
        {
            _stepLabels = value ?? [];
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
    public bool Disabled
    {
        get => _disabled;
        set
        {
            _disabled = value;
            if (value)
            {
                _draggedThumb = -1;
                _pressedThumb = -1;
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

    public override void _EnterTree()
    {
        RequestReady();
    }

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.None;
        MouseFilter = MouseFilterEnum.Pass;
        InitializeContent();
    }

    private void InitializeContent()
    {
        DisconnectContent();
        RecoverLabels();
        EnsureLabels();
        ConnectContent();
        NormalizeRangePositionsIfReady();
        RebuildStepLabels();
        Refresh();
    }

    public override void _ExitTree()
    {
        DisconnectContent();
        _draggedThumb = -1;
        _pressedThumb = -1;
    }

    public void OnBeforeSerialize() => DisconnectContent();

    public void OnAfterDeserialize() => CallDeferred(MethodName.RestoreContent);

    private void RestoreContent()
    {
        if (IsInsideTree())
        {
            InitializeContent();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            LayoutContent();
        }
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (Disabled || Value.ThumbCount == 0)
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
                EmitSignal(SignalName.ThumbChangeCommitted, _draggedThumb, Value.ThumbAt(_draggedThumb));
            }
            else if (_pressedThumb >= 0)
            {
                _draggedThumb = _pressedThumb;
                SetDraggedThumb(_pressPosition.X);
                EmitSignal(SignalName.ThumbChangeCommitted, _draggedThumb, Value.ThumbAt(_draggedThumb));
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
        var opacity = Disabled ? UiSliderStyle.DisabledOpacity : 1f;
        var line = UiTokens.MultiplyAlpha(Disabled ? _tokens.LineStrong : _tokens.Line, opacity);
        var accent = UiTokens.MultiplyAlpha(_tokens.Accent, opacity);
        var fillStart = PositionFor(Value.FillStart, trackLeft, trackRight);
        var fillEnd = PositionFor(Value.FillEnd, trackLeft, trackRight);

        if (!Disabled)
        {
            DrawRoundedTrackSegment(trackLeft, trackRight, trackY, line);
        }
        else
        {
            DrawDisabledTrackSegment(trackLeft, fillStart, trackY, line);
            DrawDisabledTrackSegment(fillEnd, trackRight, trackY, line);
        }

        DrawSteps(trackLeft, trackRight, trackY, opacity);
        DrawRoundedTrackSegment(fillStart, fillEnd, trackY, accent);

        if (HasMarker)
        {
            var markerX = PositionFor(MarkerPosition, trackLeft, trackRight);
            DrawLine(
                new Vector2(markerX, trackY - _style.MarkerHalfHeight),
                new Vector2(markerX, trackY + _style.MarkerHalfHeight),
                UiTokens.MultiplyAlpha(_tokens.Halo, opacity),
                _tokens.StrokeSignal,
                antialiased: false);
        }

        for (var index = 0; index < Value.ThumbCount; index++)
        {
            DrawThumb(new Vector2(PositionFor(Value.ThumbAt(index), trackLeft, trackRight), trackY), accent);
        }

    }

    private float TrackY => CalculateTrackY(_style, _tokens, HasValueLabelRow);

    private void EnsureLabels()
    {
        if (_label is not null)
        {
            return;
        }

        _label = CreateLabel();
        _readout = CreateLabel();
        _markerLabel = CreateLabel();
        _header = new HBoxContainer
        {
            Name = "SliderHeader",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(_header, false, InternalMode.Front);
        _header.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        _label.Name = "Label";
        _label.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        _readout.Name = "Readout";
        _readout.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        _header.AddChild(_label, false, InternalMode.Front);
        _header.AddChild(new Control
        {
            Name = "Spacer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        }, false, InternalMode.Front);
        _header.AddChild(_readout, false, InternalMode.Front);
        _markerLabel.Name = "Marker";
        AddChild(_markerLabel, false, InternalMode.Front);
    }

    private void RecoverLabels()
    {
        _header = GetChildren(includeInternal: true)
            .OfType<HBoxContainer>()
            .FirstOrDefault(header => header.Name == "SliderHeader");
        _markerLabel = GetChildren(includeInternal: true)
            .OfType<Label>()
            .FirstOrDefault(label => label.Name == "Marker");
        if (_header is null)
        {
            DiscardIncompleteContent();
            _label = null;
            _readout = null;
            _markerLabel = null;
            return;
        }

        var headerLabels = _header.GetChildren(includeInternal: true)
            .OfType<Label>()
            .ToArray();
        _label = headerLabels.FirstOrDefault(label => label.Name == "Label");
        _readout = headerLabels.FirstOrDefault(label => label.Name == "Readout");
        if (_label is null || _readout is null || _markerLabel is null)
        {
            DiscardIncompleteContent();
            _header = null;
            _label = null;
            _readout = null;
            _markerLabel = null;
        }
    }

    private void DiscardIncompleteContent()
    {
        if (_header is not null)
        {
            RemoveChild(_header);
            _header.QueueFree();
        }
        if (_markerLabel is not null)
        {
            RemoveChild(_markerLabel);
            _markerLabel.QueueFree();
        }
    }

    private void ConnectContent()
    {
        if (_header is not null && !_header.IsConnected(Container.SignalName.SortChildren, HeaderSortCallback))
        {
            _header.Connect(Container.SignalName.SortChildren, HeaderSortCallback);
        }
    }

    private void DisconnectContent()
    {
        if (_header is not null && _header.IsConnected(Container.SignalName.SortChildren, HeaderSortCallback))
        {
            _header.Disconnect(Container.SignalName.SortChildren, HeaderSortCallback);
        }
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

        var existingLabels = GetChildren(includeInternal: true)
            .OfType<Label>()
            .Where(label => label.Name == "StepLabel")
            .Concat(_stepLabelNodes)
            .Distinct()
            .ToArray();
        foreach (var label in existingLabels)
        {
            label.QueueFree();
        }

        _stepLabelNodes.Clear();
        foreach (var text in StepLabels)
        {
            var label = CreateLabel();
            label.Name = "StepLabel";
            label.Text = text;
            AddChild(label, false, InternalMode.Front);
            _stepLabelNodes.Add(label);
        }
    }

    private void SetValue(UiSliderValue value)
    {
        var kindChanged = _valueKind != value.Kind;
        _valueKind = value.Kind;
        _lowPosition = value.Low;
        _highPosition = value.High;
        if (kindChanged)
        {
            NotifyPropertyListChanged();
        }
        QueueRedraw();
    }

    private void NormalizeRangePositionsIfReady()
    {
        if (ValueKind == UiSliderValueKind.Range && IsInsideTree())
        {
            NormalizeRangePositions();
        }
    }

    private void NormalizeRangePositions()
    {
        if (_highPosition >= _lowPosition)
        {
            return;
        }

        (_lowPosition, _highPosition) = (_highPosition, _lowPosition);
        NotifyPropertyListChanged();
    }

    private void Refresh()
    {
        if (!IsInsideTree())
        {
            return;
        }

        EnsureLabels();
        _style = UiSliderStyle.From(_tokens);
        CustomMinimumSize = new Vector2(
            0,
            CalculateMinimumHeight(_style, _tokens, HasValueLabelRow, HasStepLabelRow, HasMarkerBelowRow));

        _label!.Text = LabelText;
        _readout!.Text = ReadoutText;
        _markerLabel!.Text = MarkerText;
        _label.Visible = HasValueLabelRow && !string.IsNullOrWhiteSpace(LabelText);
        _readout.Visible = HasValueLabelRow && !string.IsNullOrWhiteSpace(ReadoutText);
        _header!.Visible = HasValueLabelRow;
        _header.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        _markerLabel.Visible = HasMarkerText && (HasValueLabelRow || HasMarkerBelowRow);

        ApplyLabelStyle(_label, _tokens.OverlineText, _tokens.Muted);
        ApplyLabelStyle(_readout, _tokens.ReadoutMediumText, _tokens.Ink);
        ApplyLabelStyle(_markerLabel, _tokens.ReadoutSmallText, _tokens.Halo);
        _thumbStyle = _tokens.ControlStyle(
            _tokens.Accent,
            Colors.Transparent,
            borderWidth: 0,
            radius: _style.ThumbRadius);
        UiGlow.ApplyToControl(_thumbStyle, _tokens.Accent, _tokens.EffectsEnabled);
        _trackSegmentStyle ??= new StyleBoxFlat();
        foreach (var stepLabel in _stepLabelNodes)
        {
            stepLabel.Visible = HasStepLabelRow;
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
            UiTokens.MultiplyAlpha(color, Disabled ? UiSliderStyle.DisabledOpacity : 1f));
    }

    private void LayoutContent()
    {
        if (_label is null || _readout is null || _markerLabel is null)
        {
            return;
        }

        _markerLabel.ResetSize();

        if (_markerLabel.Visible)
        {
            var desiredCenter = Mathf.Lerp(
                _style.ThumbRadius,
                Size.X - _style.ThumbRadius,
                (float)MarkerPosition);
            var minimumX = HasMarkerBelowRow || !_label.Visible ? 0 : _label.Size.X + _tokens.Space1;
            var maximumX = HasMarkerBelowRow || !_readout.Visible
                ? Mathf.Max(0, Size.X - _markerLabel.Size.X)
                : _readout.Position.X - _tokens.Space1 - _markerLabel.Size.X;
            var markerX = maximumX >= minimumX
                ? Mathf.Clamp(desiredCenter - (_markerLabel.Size.X * 0.5f), minimumX, maximumX)
                : minimumX;
            _markerLabel.Position = new Vector2(markerX, HasMarkerBelowRow ? TrackY + _tokens.Space2 : 0);
        }

        if (!HasStepLabelRow)
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
                _tokens.StrokeSignal);
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
            antialiased: false);
    }

    private void DrawRoundedTrackSegment(float startX, float endX, float trackY, Color color)
    {
        if (endX <= startX)
        {
            return;
        }

        var radius = Mathf.CeilToInt(_style.TrackWidth * 0.5f);
        _trackSegmentStyle!.BgColor = color;
        _trackSegmentStyle.CornerRadiusTopLeft = radius;
        _trackSegmentStyle.CornerRadiusTopRight = radius;
        _trackSegmentStyle.CornerRadiusBottomLeft = radius;
        _trackSegmentStyle.CornerRadiusBottomRight = radius;
        DrawStyleBox(
            _trackSegmentStyle,
            new Rect2(startX, trackY - (_style.TrackWidth * 0.5f), endX - startX, _style.TrackWidth));
    }

    private void DrawThumb(Vector2 center, Color color)
    {
        if (!Disabled)
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
                antialiased: false);
        }
    }

    private int NearestThumb(float x)
    {
        if (Value.ThumbCount == 1)
        {
            return 0;
        }

        var trackLeft = _style.ThumbRadius;
        var trackRight = Mathf.Max(trackLeft, Size.X - _style.ThumbRadius);
        var position = trackRight <= trackLeft
            ? 0
            : UiComponentContracts.ClampSliderPosition((x - trackLeft) / (trackRight - trackLeft));
        return Value.SelectThumb(position);
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
        var next = Value.WithThumb(_draggedThumb, position);

        if (Mathf.IsEqualApprox((float)Value.ThumbAt(_draggedThumb), (float)next.ThumbAt(_draggedThumb)))
        {
            return;
        }

        Value = next;
        QueueRedraw();
        EmitSignal(SignalName.ThumbChanged, _draggedThumb, Value.ThumbAt(_draggedThumb));
        if (Value.ThumbCount == 2)
        {
            EmitSignal(SignalName.RangeChanged, Value.Low, Value.High);
        }
    }

    private static float PositionFor(double position, float left, float right) =>
        Mathf.Lerp(left, right, (float)UiComponentContracts.ClampSliderPosition(position));

    private bool HasMarker => MarkerPosition >= 0;

    private bool HasMarkerText => HasMarker && !string.IsNullOrWhiteSpace(MarkerText);

    private bool HasValueLabelRow =>
        !string.IsNullOrWhiteSpace(LabelText)
        || !string.IsNullOrWhiteSpace(ReadoutText)
        || HasMarkerText && HasStepLabelRow;

    private bool HasStepLabelRow => StepLabels.Length >= 2;

    private bool HasMarkerBelowRow => HasMarkerText && !HasStepLabelRow;

    private static float CalculateTrackY(UiSliderStyle style, UiTokens tokens, bool hasValueLabelRow) =>
        hasValueLabelRow
            ? tokens.OverlineText.LineHeight + tokens.Space3
            : TrackHalfHeight(style);

    private static float TrackHalfHeight(UiSliderStyle style) =>
        Math.Max(style.ThumbRadius, Math.Max(style.MarkerHalfHeight, style.TrackWidth * 0.5f));

    public static float CalculateMinimumHeight(
        UiSliderStyle style,
        UiTokens tokens,
        bool hasValueLabelRow,
        bool hasStepLabelRow,
        bool hasMarkerBelowRow)
    {
        var trackY = CalculateTrackY(style, tokens, hasValueLabelRow);
        var height = trackY + TrackHalfHeight(style);
        if (hasStepLabelRow || hasMarkerBelowRow)
        {
            height = Math.Max(height, trackY + tokens.Space2 + tokens.ReadoutSmallText.LineHeight);
        }

        return height;
    }
}
