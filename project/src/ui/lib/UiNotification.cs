using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>Nonblocking notification queue. Add to an overlay parent and pause during modal UI.</summary>
public sealed partial class UiNotification : Control
{
    public const double LifetimeSeconds = 5;
    public const float SwipeDistance = 48;
    private const float _tapSlop = 8;
    private const double _returnSeconds = 0.18;
    private const double _dismissSeconds = 0.22;

    public UiTokens Tokens { get; set; } = UiTokens.Neon;
    public bool HasNotification => _card is not null;
    public int PendingCount => _queue.Count;

    private bool _paused;
    public bool Paused
    {
        get => _paused;
        set
        {
            _paused = value;
            if (value)
            {
                CancelGesture();
            }
            UpdateMotionPause();
        }
    }

    private readonly Queue<UiNotificationSpec> _queue = [];
    private UiNotificationSpec? _current;
    private UiCard? _card;
    private float _preferredWidth;
    private double _remaining;
    private bool _pointerDown;
    private bool _activating;
    private Vector2 _pointerStart;
    private float _pointerTravel;
    private bool _horizontalDrag;
    private bool _verticalDrag;
    private bool _dismissing;
    private bool _applicationFocused = true;
    private float _swipeOffset;
    private Vector2 _restPosition;
    private Tween? _motion;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        GetViewport().SizeChanged += FitViewport;
        Resized += QueueLayout;
        FitViewport();
    }

    public void Enqueue(UiNotificationSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        _queue.Enqueue(spec);
        ShowNext();
    }

    public void Clear()
    {
        _queue.Clear();
        Dismiss();
    }

    public void Dismiss()
    {
        StopMotion();
        _pointerDown = false;
        _horizontalDrag = false;
        _verticalDrag = false;
        _dismissing = false;
        _swipeOffset = 0;
        _card?.Hide();
        _card?.QueueFree();
        _card = null;
        _current = null;
        ShowNext();
    }

    public void Activate()
    {
        if (Paused || _pointerDown || _dismissing || _motion is not null || _activating || _current is not { } spec)
        {
            return;
        }
        _activating = true;
        bool dismiss;
        try
        {
            dismiss = spec.OnClick?.Invoke() ?? false;
        }
        catch (Exception exception)
        {
            GD.PushError($"Notification action failed: {exception}");
            if (GodotObject.IsInstanceValid(this) && IsInsideTree() && ReferenceEquals(spec, _current))
            {
                var pending = _queue.ToArray();
                _queue.Clear();
                _queue.Enqueue(new(UiPopupType.Danger, "Action failed", "The action could not be completed. Please try again."));
                foreach (var notification in pending)
                {
                    _queue.Enqueue(notification);
                }
                Dismiss();
            }
            return;
        }
        finally
        {
            _activating = false;
        }
        if (dismiss && GodotObject.IsInstanceValid(this) && IsInsideTree() && ReferenceEquals(spec, _current))
        {
            Dismiss();
        }
    }

    public override void _Process(double delta)
    {
        if (Paused || !_applicationFocused || !IsVisibleInTree() || _pointerDown || _motion is not null)
        {
            return;
        }
        ShowNext();
        if (_card is not null)
        {
            _remaining -= delta;
            if (_remaining <= 0)
            {
                Dismiss();
            }
        }
    }

    private void ShowNext()
    {
        if (!IsNodeReady() || !IsInsideTree() || _card is not null || Paused || !_queue.TryDequeue(out var spec))
        {
            return;
        }
        _current = spec;
        _remaining = LifetimeSeconds;
        var content = GD.Load<PackedScene>("res://scenes/ui/UiNotificationContent.tscn").Instantiate<UiNotificationContent>();
        _card = content;
        _preferredWidth = content.CustomMinimumSize.X;
        AddChild(_card);
        content.Bind(spec, Tokens);
        _card.FocusMode = FocusModeEnum.All;
        _card.MouseFilter = MouseFilterEnum.Stop;
        _card.GuiInput += OnCardInput;
        _card.MinimumSizeChanged += QueueLayout;
        LayoutCard();
        QueueLayout();
    }

    private void OnCardInput(InputEvent input)
    {
        if (Paused || _dismissing)
        {
            return;
        }
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouse)
        {
            StopMotion();
            _pointerDown = true;
            _pointerStart = GetGlobalTransformWithCanvas().AffineInverse()
                * (_card!.GetGlobalTransformWithCanvas() * mouse.Position);
            // Re-grabbing a returning card should not jump it back under the finger.
            _pointerStart.X -= _swipeOffset;
            _pointerTravel = 0;
            _horizontalDrag = !Mathf.IsZeroApprox(_swipeOffset);
            _verticalDrag = false;
            _card.AcceptEvent();
        }
        else if (input.IsActionPressed("ui_accept") && !input.IsEcho())
        {
            _card!.AcceptEvent();
            Activate();
        }
    }

    public override void _Input(InputEvent input)
    {
        if (Paused || !_pointerDown || _card is null)
        {
            return;
        }
        if (input is InputEventMouseMotion motion)
        {
            TrackPointer(motion.Position);
            GetViewport().SetInputAsHandled();
        }
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } mouse)
        {
            var distance = TrackPointer(mouse.Position);
            _pointerDown = false;
            GetViewport().SetInputAsHandled();
            if (_horizontalDrag && Mathf.Abs(distance.X) >= SwipeDistance && Mathf.Abs(distance.X) > Mathf.Abs(distance.Y))
            {
                _dismissing = true;
                _card.FocusMode = FocusModeEnum.None;
                AnimateOffset(DismissOffset(), _dismissSeconds);
            }
            else if (!_horizontalDrag && !_verticalDrag && _pointerTravel <= _tapSlop)
            {
                Activate();
            }
            else if (!Mathf.IsZeroApprox(_swipeOffset))
            {
                AnimateOffset(0, _returnSeconds);
            }
        }
    }

    private Vector2 TrackPointer(Vector2 viewportPosition)
    {
        var distance = GetGlobalTransformWithCanvas().AffineInverse() * viewportPosition - _pointerStart;
        _pointerTravel = Mathf.Max(_pointerTravel, distance.Length());
        if (!_horizontalDrag && !_verticalDrag && _pointerTravel > _tapSlop)
        {
            _horizontalDrag = Mathf.Abs(distance.X) > Mathf.Abs(distance.Y);
            _verticalDrag = !_horizontalDrag;
        }
        if (_horizontalDrag)
        {
            SetSwipeOffset(distance.X);
        }
        return distance;
    }

    private void SetSwipeOffset(float offset)
    {
        _swipeOffset = offset;
        if (_card is not null)
        {
            _card.Position = _restPosition + new Vector2(offset, 0);
        }
    }

    private float DismissOffset() => _swipeOffset < 0
        ? -_restPosition.X - _card!.Size.X - Tokens.Space4
        : Size.X - _restPosition.X + Tokens.Space4;

    private void AnimateOffset(float target, double seconds)
    {
        StopMotion();
        _motion = CreateTween();
        _motion.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _motion.TweenMethod(Callable.From<float>(SetSwipeOffset), _swipeOffset, target, seconds);
        _motion.TweenCallback(Callable.From(() =>
        {
            _motion = null;
            if (_dismissing)
            {
                Dismiss();
            }
            else
            {
                SetSwipeOffset(0);
            }
        }));
        UpdateMotionPause();
    }

    private void StopMotion()
    {
        _motion?.Kill();
        _motion = null;
    }

    private void CancelGesture()
    {
        _pointerDown = false;
        _horizontalDrag = false;
        _verticalDrag = false;
        if (!_dismissing)
        {
            StopMotion();
            SetSwipeOffset(0);
        }
    }

    private void UpdateMotionPause()
    {
        if (Paused || !_applicationFocused || !IsVisibleInTree())
        {
            _motion?.Pause();
        }
        else
        {
            _motion?.Play();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut)
        {
            _applicationFocused = false;
            CancelGesture();
            UpdateMotionPause();
        }
        else if (what == NotificationApplicationFocusIn)
        {
            _applicationFocused = true;
            UpdateMotionPause();
        }
        else if (what == NotificationVisibilityChanged && IsInsideTree())
        {
            if (!IsVisibleInTree())
            {
                CancelGesture();
            }
            UpdateMotionPause();
        }
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= FitViewport;
        Resized -= QueueLayout;
        StopMotion();
        _queue.Clear();
        _current = null;
        _pointerDown = false;
    }

    private void FitViewport()
    {
        if (GetParentControl() is null)
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
            Size = GetViewportRect().Size;
        }
        QueueLayout();
    }

    private void QueueLayout() => Callable.From(LayoutCard).CallDeferred();

    private void LayoutCard()
    {
        if (_card is null)
        {
            return;
        }
        var previousRestPosition = _restPosition;
        var previousSize = _card.Size;
        var width = Mathf.Min(_preferredWidth, Size.X - Tokens.Space4 * 2);
        _card.CustomMinimumSize = new Vector2(width, 0);
        _card.Size = new Vector2(width, 0);
        _restPosition = new Vector2((Size.X - width) / 2, Size.Y - _card.Size.Y - Tokens.Space4);
        SetSwipeOffset(_swipeOffset);
        if (previousRestPosition != _restPosition || previousSize != _card.Size)
        {
            if (_dismissing)
            {
                AnimateOffset(DismissOffset(), _dismissSeconds);
            }
            else
            {
                CancelGesture();
            }
        }
    }
}
