using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>How one end of the slider's filled span looks and behaves.</summary>
public enum UiSliderEndKind
{
    /// <summary>A rounded end, like a progress bar's.</summary>
    Rounded,

    /// <summary>A draggable thumb.</summary>
    Thumb,

    /// <summary>A square end with a tick across the track.</summary>
    Marker,
}

/// <summary>One end of the filled span: its look and its position, 0…1.</summary>
public readonly record struct UiSliderEnd(UiSliderEndKind Kind, double Position)
{
    public static UiSliderEnd Rounded(double position) => new(UiSliderEndKind.Rounded, position);

    public static UiSliderEnd Thumb(double position) => new(UiSliderEndKind.Thumb, position);

    public static UiSliderEnd Marker(double position) => new(UiSliderEndKind.Marker, position);
}

/// <summary>The slider's filled span from <see cref="Low"/> to <see cref="High"/>; only thumb ends can be dragged.</summary>
public readonly record struct UiSliderValue
{
    public UiSliderValue(UiSliderEnd low, UiSliderEnd high)
    {
        var (lowPosition, highPosition) = (UiComponentContracts.ClampSliderPosition(low.Position), UiComponentContracts.ClampSliderPosition(high.Position));
        if (highPosition < lowPosition)
        {
            (lowPosition, highPosition) = (highPosition, lowPosition);
        }

        Low = low with { Position = lowPosition };
        High = high with { Position = highPosition };
    }

    public UiSliderEnd Low { get; }

    public UiSliderEnd High { get; }

    public int ThumbCount => (Low.Kind == UiSliderEndKind.Thumb ? 1 : 0) + (High.Kind == UiSliderEndKind.Thumb ? 1 : 0);

    public static UiSliderValue Progress(double value) => new(UiSliderEnd.Rounded(0), UiSliderEnd.Rounded(value));

    public static UiSliderValue Thumb(double value) => new(UiSliderEnd.Rounded(0), UiSliderEnd.Thumb(value));

    public static UiSliderValue Thumbs(double low, double high) => new(UiSliderEnd.Thumb(low), UiSliderEnd.Thumb(high));

    /// <summary>The position of thumb <paramref name="index"/>, counting thumb ends from Low.</summary>
    public double ThumbAt(int index) => ThumbEnd(index).Position;

    /// <summary>The thumb nearest <paramref name="position"/>, or -1 with no thumb.</summary>
    public int SelectThumb(double position)
    {
        if (ThumbCount < 2)
        {
            return ThumbCount - 1;
        }

        var target = UiComponentContracts.ClampSliderPosition(position);
        if (Low.Position == High.Position)
        {
            return target < Low.Position ? 0 : 1;
        }

        return Math.Abs(target - Low.Position) <= Math.Abs(target - High.Position) ? 0 : 1;
    }

    /// <summary>Moves thumb <paramref name="index"/> to <paramref name="position"/>, stopping at the other end.</summary>
    public UiSliderValue WithThumb(int index, double position) =>
        IsLowThumb(index)
            ? new(Low with { Position = Math.Min(position, High.Position) }, High)
            : new(Low, ThumbEnd(index) with { Position = Math.Max(position, Low.Position) });

    private UiSliderEnd ThumbEnd(int index) =>
        IsLowThumb(index) ? Low
        : index == ThumbCount - 1 && High.Kind == UiSliderEndKind.Thumb ? High
        : throw new ArgumentOutOfRangeException(nameof(index), index, null);

    private bool IsLowThumb(int index) => index == 0 && Low.Kind == UiSliderEndKind.Thumb;
}

/// <summary>Canonical normalized slider: a filled span whose ends are rounded, thumbs or markers, with steps, a marker and a disabled state.</summary>
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

    /// <summary>A tap or sideways drag began at <paramref name="position"/> on a slider with no thumb. A thumb set in reply takes the touch.</summary>
    [Signal]
    public delegate void TrackPressedEventHandler(double position);

    private string _labelText = "Value";
    private string _readoutText = "50";
    private UiSliderEndKind _lowEnd = UiSliderEndKind.Rounded;
    private UiSliderEndKind _highEnd = UiSliderEndKind.Thumb;
    private double _lowPosition;
    private double _highPosition = 0.5;
    private string[] _stepLabels = [];
    private Func<string>? _readoutSource;
    private IReadOnlyList<Func<string>>? _stepLabelSources;
    private double _step;
    private double _markerPosition = -1;
    private string _markerText = string.Empty;
    private bool _disabled;
    private UiSliderStyle _style;
    private float _minimumHeight;
    private HBoxContainer? _header;
    private Label? _label;
    private Label? _readout;
    private Label? _markerLabel;
    private StyleBoxFlat? _thumbStyle;
    private StyleBoxFlat? _trackSegmentStyle;
    private readonly List<Label> _stepLabelNodes = [];
    private int _draggedThumb = -1;
    private int _pressedThumb = -1;
    private bool _pressed;
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

    /// <summary>
    /// Code-set readout text, asked again when the language changes. While set it is shown instead
    /// of <see cref="ReadoutText"/> and not translated again; null shows <see cref="ReadoutText"/> (#756).
    /// </summary>
    public Func<string>? ReadoutSource
    {
        get => _readoutSource;
        set
        {
            _readoutSource = value;
            Refresh();
        }
    }

    public UiSliderValue Value
    {
        get => new(new UiSliderEnd(LowEnd, LowPosition), new UiSliderEnd(HighEnd, HighPosition));
        set => SetValue(value);
    }

    [Export]
    public UiSliderEndKind LowEnd
    {
        get => _lowEnd;
        set
        {
            _lowEnd = value;
            QueueRedraw();
        }
    }

    [Export]
    public UiSliderEndKind HighEnd
    {
        get => _highEnd;
        set
        {
            _highEnd = value;
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

    /// <summary>
    /// The distance between two stops on the 0…1 track, like Godot's <c>Range.step</c>: a dragged
    /// thumb stops only on whole steps, so it never runs ahead of a stepped value (#711). 0 moves smoothly.
    /// </summary>
    [Export(PropertyHint.Range, "0,1,0.001")]
    public double Step
    {
        get => _step;
        set => _step = Math.Max(0, value);
    }

    /// <summary>
    /// Code-set step labels, asked again when the language changes. While set they are shown
    /// instead of <see cref="StepLabels"/> and not translated again; null shows <see cref="StepLabels"/> (#756).
    /// </summary>
    public IReadOnlyList<Func<string>>? StepLabelSources
    {
        get => _stepLabelSources;
        set
        {
            _stepLabelSources = value;
            RebuildStepLabels();
            Refresh();
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
                ResetTouch();
            }

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
        _style = UiSliderStyle.Default;
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
        ResetTouch();
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
        if (what == NotificationTranslationChanged)
        {
            AskTextSources();
        }
        else if (what == NotificationThemeChanged && IsNodeReady())
        {
            UiThemeRefresh.Guarded(this, () =>
            {
                _style = UiSliderStyle.Default;
                Refresh();
            });
        }
        else if (what == NotificationResized)
        {
            LayoutContent();
        }
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (Disabled)
        {
            ResetTouch();
            return;
        }

        if (PointerInput.TryGetPressPosition(inputEvent, out var pressPosition))
        {
            ResetTouch();
            _pressed = true;
            _pressPosition = pressPosition;
            return;
        }

        if (_pressed && PointerInput.TryGetDragPosition(inputEvent, out var dragPosition))
        {
            if (_draggedThumb < 0)
            {
                var delta = dragPosition - _pressPosition;
                if (Mathf.Abs(delta.Y) > Mathf.Abs(delta.X) || !TakeThumb())
                {
                    ResetTouch();
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
            if (_draggedThumb < 0 && _pressed && TakeThumb())
            {
                _draggedThumb = _pressedThumb;
                SetDraggedThumb(_pressPosition.X);
            }

            if (_draggedThumb >= 0)
            {
                EmitSignal(SignalName.ThumbChangeCommitted, _draggedThumb, Value.ThumbAt(_draggedThumb));
            }

            ResetTouch();
        }
    }

    // With no thumb, the press is reported first, so a thumb set in reply takes it.
    private bool TakeThumb()
    {
        if (Value.ThumbCount == 0)
        {
            EmitSignal(SignalName.TrackPressed, UiComponentContracts.SnapSliderPosition(TrackPosition(_pressPosition.X), Step));
        }

        _pressedThumb = Value.SelectThumb(TrackPosition(_pressPosition.X));
        return _pressedThumb >= 0;
    }

    private void ResetTouch()
    {
        _pressed = false;
        _draggedThumb = -1;
        _pressedThumb = -1;
    }

    public override void _Draw()
    {
        var trackLeft = _style.ThumbRadius;
        var trackRight = Mathf.Max(trackLeft, Size.X - _style.ThumbRadius);
        var trackY = TrackY;
        var opacity = Disabled ? UiSliderStyle.DisabledOpacity : 1f;
        var line = Disabled ? UiThemeLookup.Color(this, UiTokens.Color.LineStrong) : UiThemeLookup.Color(this, UiTokens.Color.Line).ScaleAlpha(opacity);
        var accent = UiThemeLookup.Color(this, UiTokens.Color.Accent).ScaleAlpha(opacity);
        var value = Value;
        var fillStart = PositionFor(value.Low.Position, trackLeft, trackRight);
        var fillEnd = PositionFor(value.High.Position, trackLeft, trackRight);

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
        DrawTrackSegment(fillStart, fillEnd, trackY, accent, value.Low.Kind != UiSliderEndKind.Marker, value.High.Kind != UiSliderEndKind.Marker);
        DrawEndMarker(value.Low, fillStart, trackY, accent);
        DrawEndMarker(value.High, fillEnd, trackY, accent);

        if (HasMarker)
        {
            var markerX = PositionFor(MarkerPosition, trackLeft, trackRight);
            using var pen = UiPixelPen.Begin(this);
            pen.Line(
                new Vector2(markerX, trackY - _style.MarkerHalfHeight),
                new Vector2(markerX, trackY + _style.MarkerHalfHeight),
                UiThemeLookup.Color(this, UiTokens.Color.Halo).ScaleAlpha(opacity),
                UiSize.Stroke.Signal);
        }

        for (var index = 0; index < value.ThumbCount; index++)
        {
            DrawThumb(new Vector2(PositionFor(value.ThumbAt(index), trackLeft, trackRight), trackY), accent);
        }

    }

    private float TrackY => CalculateTrackY(
        _style,
        UiThemeLookup.FontSize(this, UiTokens.Typography.Overline) + UiSize.Widget.SmallTextLeading,
        UiSize.Space.S3,
        HasValueLabelRow);

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

        // The label fills the row and trims first, so a long readout stays inside it.
        _label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _readout.Name = "Readout";
        _readout.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        _header.AddChild(_label, false, InternalMode.Front);
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

    private string ShownReadout => _readoutSource?.Invoke() ?? ReadoutText;

    private IReadOnlyList<string> ShownStepLabels =>
        _stepLabelSources is { } sources ? [.. sources.Select(source => source())] : _stepLabels;

    private int StepLabelCount => _stepLabelSources?.Count ?? _stepLabels.Length;

    // Godot sends a language change while it walks the tree, when this node may not add children,
    // so the step labels keep their nodes and only take the new text.
    private void AskTextSources()
    {
        if (_stepLabelSources is not null && _stepLabelNodes.Count == _stepLabelSources.Count)
        {
            for (var index = 0; index < _stepLabelNodes.Count; index++)
            {
                _stepLabelNodes[index].Text = _stepLabelSources[index]();
            }
        }

        if (_readoutSource is not null || _stepLabelSources is not null)
        {
            Refresh();
        }
    }

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
        foreach (var text in ShownStepLabels)
        {
            var label = CreateLabel();
            label.Name = "StepLabel";
            label.AutoTranslateMode = _stepLabelSources is null ? AutoTranslateModeEnum.Inherit : AutoTranslateModeEnum.Disabled;
            label.Text = text;
            AddChild(label, false, InternalMode.Front);
            _stepLabelNodes.Add(label);
        }
    }

    private void SetValue(UiSliderValue value)
    {
        (_lowEnd, _lowPosition) = (value.Low.Kind, value.Low.Position);
        (_highEnd, _highPosition) = (value.High.Kind, value.High.Position);
        QueueRedraw();
    }

    private void NormalizeRangePositionsIfReady()
    {
        if (IsInsideTree())
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

    public override Vector2 _GetMinimumSize() => new(0, _minimumHeight);

    private void Refresh()
    {
        if (!IsInsideTree())
        {
            return;
        }

        EnsureLabels();
        _style = UiSliderStyle.Default;
        var minimumHeight = CalculateMinimumHeight(
            _style,
            UiThemeLookup.FontSize(this, UiTokens.Typography.Overline) + UiSize.Widget.SmallTextLeading,
            UiSize.Space.S3,
            UiSize.Space.S2,
            UiThemeLookup.FontSize(this, UiTokens.Typography.ReadoutSmall) + UiSize.Widget.SmallTextLeading,
            HasValueLabelRow,
            HasStepLabelRow,
            HasMarkerBelowRow);
        if (_minimumHeight != minimumHeight)
        {
            _minimumHeight = minimumHeight;
            UpdateMinimumSize();
        }

        _label!.Text = LabelText;
        _readout!.AutoTranslateMode = _readoutSource is null ? AutoTranslateModeEnum.Inherit : AutoTranslateModeEnum.Disabled;
        var readout = ShownReadout;
        _readout.Text = readout;
        _markerLabel!.Text = MarkerText;
        _label.Visible = HasValueLabelRow && !string.IsNullOrWhiteSpace(LabelText);
        _readout.Visible = HasValueLabelRow && !string.IsNullOrWhiteSpace(readout);
        _header!.Visible = HasValueLabelRow;
        _header.AddThemeConstantOverride("separation", (int)UiSize.Space.S1);
        _markerLabel.Visible = HasMarkerText && (HasValueLabelRow || HasMarkerBelowRow);

        ApplyLabelStyle(_label, UiTokens.Typography.Overline, UiTokens.Color.Muted);
        ApplyLabelStyle(_readout, UiTokens.Typography.ReadoutMedium, UiTokens.Color.Ink);
        ApplyLabelStyle(_markerLabel, UiTokens.Typography.ReadoutSmall, UiTokens.Color.Halo);
        _thumbStyle = UiThemeLookup.CreateStyleBox(UiThemeLookup.Color(this, UiTokens.Color.Accent),
            Colors.Transparent,
            borderWidth: 0,
            radius: _style.ThumbRadius);
        UiGlow.ApplyToControl(_thumbStyle, UiThemeLookup.Color(this, UiTokens.Color.Accent), UiThemeLookup.EffectsEnabled(this));
        _trackSegmentStyle ??= new StyleBoxFlat();
        foreach (var stepLabel in _stepLabelNodes)
        {
            stepLabel.Visible = HasStepLabelRow;
            ApplyLabelStyle(stepLabel, UiTokens.Typography.ReadoutSmall, UiTokens.Color.Muted);
        }

        LayoutContent();
        QueueRedraw();
    }

    private void ApplyLabelStyle(Label label, UiTokens.Typography style, UiTokens.Color color)
    {
        UiThemeLookup.ApplyTextStyle(label, style, color);
        label.SelfModulate = Colors.White with { A = Disabled ? UiSliderStyle.DisabledOpacity : 1f };
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
            var minimumX = HasMarkerBelowRow || !_label.Visible ? 0 : LabelTextWidth() + UiSize.Space.S1;
            var maximumX = HasMarkerBelowRow || !_readout.Visible
                ? Mathf.Max(0, Size.X - _markerLabel.Size.X)
                : _readout.Position.X - UiSize.Space.S1 - _markerLabel.Size.X;
            var markerX = maximumX >= minimumX
                ? Mathf.Clamp(desiredCenter - (_markerLabel.Size.X * 0.5f), minimumX, maximumX)
                : minimumX;
            _markerLabel.Position = new Vector2(markerX, HasMarkerBelowRow ? TrackY + UiSize.Space.S2 : 0);
        }

        if (!HasStepLabelRow)
        {
            return;
        }

        var labelY = TrackY + UiSize.Space.S2;
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

    // The label fills its row, so the marker text keeps clear of its text, not its box.
    private float LabelTextWidth()
    {
        var text = _label!.Uppercase ? _label.Text.ToUpperInvariant() : _label.Text;
        var width = _label.GetThemeFont("font").GetStringSize(text, fontSize: _label.GetThemeFontSize("font_size")).X;
        return Mathf.Min(_label.Size.X, width);
    }

    private void DrawSteps(float trackLeft, float trackRight, float trackY, float opacity)
    {
        if (StepLabelCount < 2)
        {
            return;
        }

        var color = UiThemeLookup.Color(this, UiTokens.Color.LineStrong).ScaleAlpha(opacity);
        using var pen = UiPixelPen.Begin(this);
        for (var index = 0; index < StepLabelCount; index++)
        {
            var x = Mathf.Lerp(trackLeft, trackRight, index / (float)(StepLabelCount - 1));
            pen.Line(
                new Vector2(x, trackY - _style.StepTickHalfHeight),
                new Vector2(x, trackY + _style.StepTickHalfHeight),
                color,
                UiSize.Stroke.Signal);
        }
    }

    private void DrawEndMarker(UiSliderEnd end, float x, float trackY, Color color)
    {
        if (end.Kind == UiSliderEndKind.Marker)
        {
            using var pen = UiPixelPen.Begin(this);
            pen.Line(
                new Vector2(x, trackY - _style.StepTickHalfHeight),
                new Vector2(x, trackY + _style.StepTickHalfHeight),
                color,
                UiSize.Stroke.Signal);
        }
    }

    private void DrawDisabledTrackSegment(float startX, float endX, float trackY, Color color)
    {
        if (endX <= startX)
        {
            return;
        }

        using var pen = UiPixelPen.Begin(this);
        pen.DashedLine(new Vector2(startX, trackY), new Vector2(endX, trackY), color, _style.TrackWidth, _style.DisabledDashLength);
    }

    private void DrawRoundedTrackSegment(float startX, float endX, float trackY, Color color) =>
        DrawTrackSegment(startX, endX, trackY, color, roundStart: true, roundEnd: true);

    private void DrawTrackSegment(float startX, float endX, float trackY, Color color, bool roundStart, bool roundEnd)
    {
        if (endX <= startX)
        {
            return;
        }

        var radius = Mathf.CeilToInt(_style.TrackWidth * 0.5f);
        _trackSegmentStyle!.BgColor = color;
        _trackSegmentStyle.CornerRadiusTopLeft = roundStart ? radius : 0;
        _trackSegmentStyle.CornerRadiusBottomLeft = roundStart ? radius : 0;
        _trackSegmentStyle.CornerRadiusTopRight = roundEnd ? radius : 0;
        _trackSegmentStyle.CornerRadiusBottomRight = roundEnd ? radius : 0;
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

        using var pen = UiPixelPen.Begin(this);
        pen.Disc(
            center,
            _style.ThumbRadius - _style.DisabledThumbInset,
            UiThemeLookup.Color(this, UiTokens.Color.PanelRaised).ScaleAlpha(UiSliderStyle.DisabledOpacity));
        for (var index = 0; index < UiSliderStyle.DisabledThumbSegments; index += 2)
        {
            pen.Arc(
                center,
                _style.ThumbRadius,
                Mathf.Tau * index / UiSliderStyle.DisabledThumbSegments,
                Mathf.Tau * (index + 1) / UiSliderStyle.DisabledThumbSegments,
                UiSliderStyle.DisabledThumbArcPoints,
                color,
                UiSize.Stroke.Hair);
        }
    }

    private double TrackPosition(float x)
    {
        var trackLeft = _style.ThumbRadius;
        var trackRight = Mathf.Max(trackLeft, Size.X - _style.ThumbRadius);
        return trackRight <= trackLeft
            ? 0
            : UiComponentContracts.ClampSliderPosition((x - trackLeft) / (trackRight - trackLeft));
    }

    private void SetDraggedThumb(float x)
    {
        if (_draggedThumb < 0)
        {
            return;
        }

        var next = Value.WithThumb(_draggedThumb, UiComponentContracts.SnapSliderPosition(TrackPosition(x), Step));
        if (Mathf.IsEqualApprox((float)Value.ThumbAt(_draggedThumb), (float)next.ThumbAt(_draggedThumb)))
        {
            return;
        }

        Value = next;
        EmitSignal(SignalName.ThumbChanged, _draggedThumb, Value.ThumbAt(_draggedThumb));
        if (Value.ThumbCount == 2)
        {
            EmitSignal(SignalName.RangeChanged, Value.Low.Position, Value.High.Position);
        }
    }

    private static float PositionFor(double position, float left, float right) =>
        Mathf.Lerp(left, right, (float)UiComponentContracts.ClampSliderPosition(position));

    private bool HasMarker => MarkerPosition >= 0;

    private bool HasMarkerText => HasMarker && !string.IsNullOrWhiteSpace(MarkerText);

    private bool HasValueLabelRow =>
        !string.IsNullOrWhiteSpace(LabelText)
        || _readoutSource is not null
        || !string.IsNullOrWhiteSpace(ReadoutText)
        || HasMarkerText && HasStepLabelRow;

    private bool HasStepLabelRow => StepLabelCount >= 2;

    private bool HasMarkerBelowRow => HasMarkerText && !HasStepLabelRow;

    private static float CalculateTrackY(UiSliderStyle style, float labelLineHeight, float space3, bool hasValueLabelRow) =>
        hasValueLabelRow
            ? labelLineHeight + space3
            : TrackHalfHeight(style);

    private static float TrackHalfHeight(UiSliderStyle style) =>
        Math.Max(style.ThumbRadius, Math.Max(style.MarkerHalfHeight, style.TrackWidth * 0.5f));

    public static float CalculateMinimumHeight(
        UiSliderStyle style,
        float labelLineHeight,
        float space3,
        float space2,
        float stepLineHeight,
        bool hasValueLabelRow,
        bool hasStepLabelRow,
        bool hasMarkerBelowRow)
    {
        var trackY = CalculateTrackY(style, labelLineHeight, space3, hasValueLabelRow);
        var height = trackY + TrackHalfHeight(style);
        if (hasStepLabelRow || hasMarkerBelowRow)
        {
            height = Math.Max(height, trackY + space2 + stepLineHeight);
        }

        return height;
    }
}
