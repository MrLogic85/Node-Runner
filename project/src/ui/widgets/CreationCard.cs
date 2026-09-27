using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

public partial class CreationCard : Control
{
    private string _creationKey = string.Empty;
    private string _creationName = string.Empty;
    private CreatureDef? _creature;
    private string _summary = string.Empty;
    private string _note = string.Empty;
    private string _thumbnail = string.Empty;
    private string _savedState = string.Empty;
    private string _unlockCredit = string.Empty;
    private float _achievementProgress;
    private string _achievementProgressText = string.Empty;
    private bool _isExample;
    private bool _canOpen;
    private bool _canEdit;
    private bool _canDuplicate;
    private bool _canDelete;

    [Signal]
    public delegate void OpenRequestedEventHandler(string creationKey, string creationName);

    [Signal]
    public delegate void EditRequestedEventHandler(string creationKey, string creationName);

    [Signal]
    public delegate void DuplicateRequestedEventHandler(string creationKey, string creationName);

    [Signal]
    public delegate void DeleteRequestedEventHandler(string creationKey, string creationName);

    public void Setup(
        string creationKey,
        string creationName,
        CreatureDef? creature,
        string summary,
        string note,
        string thumbnail,
        string savedState,
        string unlockCredit,
        float achievementProgress,
        string achievementProgressText,
        bool isExample,
        bool canOpen,
        bool canEdit,
        bool canDuplicate,
        bool canDelete)
    {
        _creationKey = creationKey;
        _creationName = creationName;
        _creature = creature;
        _summary = summary;
        _note = note;
        _thumbnail = thumbnail;
        _savedState = savedState;
        _unlockCredit = unlockCredit;
        _achievementProgress = Mathf.Clamp(achievementProgress, 0, 1);
        _achievementProgressText = achievementProgressText;
        _isExample = isExample;
        _canOpen = canOpen;
        _canEdit = canEdit;
        _canDuplicate = canDuplicate;
        _canDelete = canDelete;
        if (IsInsideTree())
        {
            Rebuild();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        Rebuild();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged && IsNodeReady())
        {
            UiThemeRefresh.Guarded(this, Rebuild);
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (PointerInput.TryGetPressPosition(@event, out _) && _canEdit)
        {
            EmitSignal(SignalName.OpenRequested, _creationKey, _creationName);
            AcceptEvent();
        }
    }

    private void Rebuild()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        CustomMinimumSize = new Vector2(194, 204);
        SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;

        var panel = new UiCard
        {
            Kind = UiCard.CardVariant.Frame,
        };
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(panel);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 0);
        panel.AddChild(stack);
        stack.AddChild(CreateThumbnail(_thumbnail));
        stack.AddChild(CreateContent());
        stack.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        stack.AddChild(CreateActions());
    }

    private Control CreateContent()
    {
        var margin = new MarginContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        margin.AddThemeConstantOverride("margin_left", (int)UiSize.Space.S2);
        margin.AddThemeConstantOverride("margin_top", (int)UiSize.Space.S2);
        margin.AddThemeConstantOverride("margin_right", (int)UiSize.Space.S2);
        margin.AddThemeConstantOverride("margin_bottom", (int)UiSize.Space.S1);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)UiSize.Space.S1);
        margin.AddChild(content);
        content.AddChild(CreateTitleRow());
        content.AddChild(CreateLabel(_summary, UiTokens.Typography.Caption, UiTokens.Color.Muted));
        if (_achievementProgress > 0)
        {
            content.AddChild(CreateProgressBar());
        }

        if (!string.IsNullOrWhiteSpace(_unlockCredit))
        {
            content.AddChild(CreateLabel(_unlockCredit, UiTokens.Typography.Caption, UiTokens.Color.Accent));
        }

        return margin;
    }

    private Control CreateTitleRow()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", (int)UiSize.Space.S1);
        row.AddChild(CreateLabel(DisplayName(), UiTokens.Typography.Label, UiTokens.Color.Ink, expand: true));
        if (_isExample || !string.IsNullOrWhiteSpace(_note))
        {
            row.AddChild(new UiChip
            {
                Text = _isExample ? "Example" : _note,
                Kind = _isExample ? UiChip.ChipKind.Neutral : UiChip.ChipKind.Ok,
            });
        }

        return row;
    }

    private Control CreateActions()
    {
        var actions = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, UiSize.Control.Touch),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        actions.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderColor = UiThemeLookup.Color(this, UiTokens.Color.Edge),
            BorderWidthTop = (int)UiSize.Stroke.Hair,
        });

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 0);
        actions.AddChild(row);

        var duplicateButton = CreateActionSegment("Copy", UiIconId.Copy, UiButtonKind.Secondary);
        duplicateButton.Disabled = !_canDuplicate;
        duplicateButton.Pressed += () => EmitSignal(SignalName.DuplicateRequested, _creationKey, _creationName);
        row.AddChild(duplicateButton);

        var editButton = CreateActionSegment("Edit", UiIconId.Edit, UiButtonKind.Secondary);
        editButton.Disabled = !_canEdit;
        editButton.Pressed += () => EmitSignal(SignalName.EditRequested, _creationKey, _creationName);
        row.AddChild(editButton);

        if (_isExample)
        {
            row.AddChild(new LockedActionSegment
            {
                CustomMinimumSize = new Vector2(0, UiSize.Control.Touch),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            });
        }
        else
        {
            var deleteButton = CreateActionSegment("Delete", UiIconId.Trash, UiButtonKind.Tertiary);
            deleteButton.Disabled = !_canDelete;
            deleteButton.Pressed += () => EmitSignal(SignalName.DeleteRequested, _creationKey, _creationName);
            row.AddChild(deleteButton);
        }

        return actions;
    }

    private Control CreateThumbnail(string text)
    {
        var thumbnail = new CreatureThumbnail
        {
            Creature = _creature,
            Summary = text,
            CustomMinimumSize = new Vector2(0, 82),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        return thumbnail;
    }

    private Control CreateProgressBar()
    {
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 2);
        if (!string.IsNullOrWhiteSpace(_achievementProgressText))
        {
            stack.AddChild(CreateLabel(_achievementProgressText, UiTokens.Typography.Caption, UiTokens.Color.Muted));
        }

        var track = new Panel
        {
            CustomMinimumSize = new Vector2(0, 6),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        track.AddThemeStyleboxOverride("panel", UiThemeLookup.CreateStyleBox(UiThemeLookup.Color(this, UiTokens.Color.Panel), UiThemeLookup.Color(this, UiTokens.Color.Edge), radius: UiSize.Radius.Pill));
        track.AddChild(new ColorRect
        {
            Color = UiThemeLookup.Color(this, UiTokens.Color.Accent),
            AnchorRight = _achievementProgress,
            AnchorBottom = 1,
            MouseFilter = MouseFilterEnum.Ignore,
        });
        stack.AddChild(track);
        return stack;
    }

    private Label CreateLabel(string text, UiTokens.Typography style, UiTokens.Color color, bool expand = false)
    {
        var label = new Label
        {
            Text = text,
            SizeFlagsHorizontal = expand ? SizeFlags.ExpandFill : SizeFlags.Fill,
            ClipText = true,
        };
        UiThemeLookup.ApplyTextStyle(label, style, color);
        return label;
    }

    private string DisplayName() =>
        _isExample && _creationName.StartsWith("Example: ", StringComparison.OrdinalIgnoreCase)
            ? _creationName["Example: ".Length..]
            : _creationName;

    private UiButton CreateButton(string text, UiButtonKind kind) =>
        new()
        {
            Text = text,
            Kind = kind,
            CustomMinimumSize = new Vector2(56, UiSize.Control.Touch),
        };

    private Button CreateActionSegment(string text, UiIconId iconId, UiButtonKind kind, bool disabled = false)
    {
        var color = kind == UiButtonKind.Tertiary ? UiThemeLookup.Color(this, UiTokens.Color.Danger) : UiThemeLookup.Color(this, UiTokens.Color.Ink);
        var button = new Button
        {
            Text = text.ToUpperInvariant(),
            Disabled = disabled,
            CustomMinimumSize = new Vector2(0, UiSize.Control.Touch),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        UiThemeLookup.ApplyTypography(button, UiTokens.Typography.Caption);
        UiIcons.Apply(button, iconId, UiIconSize.Standard, color);
        button.AddThemeColorOverride("font_color", color);
        button.AddThemeColorOverride("font_disabled_color", UiThemeLookup.Color(this, UiTokens.Color.Muted));
        button.AddThemeColorOverride("font_hover_color", kind == UiButtonKind.Tertiary ? UiThemeLookup.Color(this, UiTokens.Color.Danger) : UiThemeLookup.Color(this, UiTokens.Color.Accent));
        button.AddThemeColorOverride("font_pressed_color", UiThemeLookup.Color(this, UiTokens.Color.OnAccent));
        button.AddThemeStyleboxOverride("normal", CreateSegmentStyle(Colors.Transparent, Colors.Transparent));
        button.AddThemeStyleboxOverride(
            "hover",
            CreateSegmentStyle(
                kind == UiButtonKind.Tertiary
                    ? UiThemeLookup.Color(this, UiTokens.Color.Danger).WithAlpha(0.16f)
                    : UiThemeLookup.Color(this, UiTokens.Color.Accent).WithAlpha(UiThemeLookup.Alpha(this, UiTokens.Alpha.Soft)),
                UiThemeLookup.Color(this, UiTokens.Color.Edge)));
        button.AddThemeStyleboxOverride("pressed", CreateSegmentStyle(kind == UiButtonKind.Tertiary ? UiThemeLookup.Color(this, UiTokens.Color.Danger) : UiThemeLookup.Color(this, UiTokens.Color.Accent), kind == UiButtonKind.Tertiary ? UiThemeLookup.Color(this, UiTokens.Color.Danger) : UiThemeLookup.Color(this, UiTokens.Color.Accent)));
        button.AddThemeStyleboxOverride(
            "focus",
            CreateSegmentStyle(
                UiThemeLookup.Color(this, UiTokens.Color.Accent).WithAlpha(UiThemeLookup.Alpha(this, UiTokens.Alpha.Soft)),
                UiThemeLookup.Color(this, UiTokens.Color.Accent)));
        button.AddThemeStyleboxOverride("disabled", CreateSegmentStyle(Colors.Transparent, Colors.Transparent));
        return button;
    }

    private StyleBoxFlat CreateSegmentStyle(Color background, Color border) =>
        new()
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = border.A > 0 ? (int)UiSize.Stroke.Hair : 0,
            BorderWidthTop = border.A > 0 ? (int)UiSize.Stroke.Hair : 0,
            BorderWidthRight = border.A > 0 ? (int)UiSize.Stroke.Hair : 0,
            BorderWidthBottom = border.A > 0 ? (int)UiSize.Stroke.Hair : 0,
            CornerRadiusTopLeft = (int)UiSize.Radius.Small,
            CornerRadiusTopRight = (int)UiSize.Radius.Small,
            CornerRadiusBottomLeft = (int)UiSize.Radius.Small,
            CornerRadiusBottomRight = (int)UiSize.Radius.Small,
        };

    private sealed partial class CreatureThumbnail : Control
    {
        public CreatureDef? Creature { get; init; }

        public string Summary { get; init; } = string.Empty;

        public override void _Draw()
        {
            DrawRect(new Rect2(Vector2.Zero, Size), UiThemeLookup.Color(this, UiTokens.Color.Background));
            DrawLine(new Vector2(0, Size.Y - 1), new Vector2(Size.X, Size.Y - 1), UiThemeLookup.Color(this, UiTokens.Color.Edge), UiSize.Stroke.Hair, antialiased: false);
            if (Creature is null || Creature.Nodes.Count == 0)
            {
                DrawString(ThemeDB.FallbackFont, new Vector2(16, Size.Y * 0.52f), Summary, HorizontalAlignment.Left, Size.X - 32, 12, UiThemeLookup.Color(this, UiTokens.Color.Muted));
                return;
            }

            var points = Creature.Nodes.Select(node => new Vector2((float)node.Position.X, (float)node.Position.Y)).ToArray();
            var min = points[0];
            var max = points[0];
            foreach (var point in points)
            {
                min = new Vector2(Mathf.Min(min.X, point.X), Mathf.Min(min.Y, point.Y));
                max = new Vector2(Mathf.Max(max.X, point.X), Mathf.Max(max.Y, point.Y));
            }

            var content = new Rect2(14, 12, Size.X - 28, Size.Y - 28);
            var span = new Vector2(Mathf.Max(1, max.X - min.X), Mathf.Max(1, max.Y - min.Y));
            var scale = Mathf.Min(content.Size.X / span.X, content.Size.Y / span.Y) * 0.74f;
            var centerOffset = content.GetCenter() - ((min + max) * 0.5f * scale);

            Vector2 Map(Vector2 point) => (point * scale) + centerOffset;

            foreach (var beam in Creature.Beams)
            {
                DrawLine(Map(points[beam.NodeA]), Map(points[beam.NodeB]), UiThemeLookup.Color(this, UiTokens.Color.Ink), 3, antialiased: false);
            }

            for (var i = 0; i < points.Length; i++)
            {
                var mapped = Map(points[i]);
                DrawCircle(mapped, 5.5f, UiThemeLookup.Color(this, UiTokens.Color.PanelRaised));
                DrawArc(mapped, 5.5f, 0, Mathf.Tau, 24, UiThemeLookup.Color(this, UiTokens.Color.Accent), 2, antialiased: false);
            }

            foreach (var core in Creature.Cores)
            {
                var mapped = Map(points[core.NodeIndex]);
                DrawCircle(mapped, 2.8f, UiThemeLookup.Color(this, UiTokens.Color.Accent));
            }
        }
    }

    private sealed partial class LockedActionSegment : Control
    {
        public override void _Draw()
        {
            var centerX = Size.X * 0.5f;
            var iconY = 17f;
            var color = UiThemeLookup.Color(this, UiTokens.Color.Muted);
            var lockIcon = UiIcons.Load(UiIconId.Lock, UiIconSize.Standard);
            DrawTextureRect(lockIcon, new Rect2(centerX - 8, iconY - 8, 16, 16), false, color);

            DrawString(ThemeDB.FallbackFont, new Vector2(0, 40), "LOCK", HorizontalAlignment.Center, Size.X, 12, color);
        }
    }
}
