using Godot;
using NodeRunner.Domain;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

public partial class CreationCard : Control
{
    private UiTokens _tokens = UiTokens.Neon;
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
        UiTokens tokens,
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
        _tokens = tokens;
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

        var panel = new UiPanel
        {
            Tokens = _tokens,
            Variant = UiSurfaceContracts.FrameVariant.Frame,
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
        margin.AddThemeConstantOverride("margin_left", (int)_tokens.Space2);
        margin.AddThemeConstantOverride("margin_top", (int)_tokens.Space2);
        margin.AddThemeConstantOverride("margin_right", (int)_tokens.Space2);
        margin.AddThemeConstantOverride("margin_bottom", (int)_tokens.Space1);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        margin.AddChild(content);
        content.AddChild(CreateTitleRow());
        content.AddChild(CreateLabel(_summary, _tokens.CaptionText, _tokens.Muted));
        if (_achievementProgress > 0)
        {
            content.AddChild(CreateProgressBar());
        }

        if (!string.IsNullOrWhiteSpace(_unlockCredit))
        {
            content.AddChild(CreateLabel(_unlockCredit, _tokens.CaptionText, _tokens.Accent));
        }

        return margin;
    }

    private Control CreateTitleRow()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", (int)_tokens.Space1);
        row.AddChild(CreateLabel(DisplayName(), _tokens.LabelText, _tokens.Ink, expand: true));
        if (_isExample || !string.IsNullOrWhiteSpace(_note))
        {
            row.AddChild(new UiChip
            {
                Tokens = _tokens,
                Text = _isExample ? "Example" : _note,
                Kind = _isExample ? UiChip.ChipKind.Locked : UiChip.ChipKind.Accent,
                CustomMinimumSize = new Vector2(0, 24),
            });
        }

        return row;
    }

    private Control CreateActions()
    {
        var actions = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, _tokens.TouchTarget),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        actions.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderColor = _tokens.Edge,
            BorderWidthTop = (int)_tokens.StrokeHair,
        });

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 0);
        actions.AddChild(row);

        var duplicateButton = CreateActionSegment("Copy", UiIconId.Copy, UiActionButton.ActionKind.Secondary);
        duplicateButton.Disabled = !_canDuplicate;
        duplicateButton.Pressed += () => EmitSignal(SignalName.DuplicateRequested, _creationKey, _creationName);
        row.AddChild(duplicateButton);

        var editButton = CreateActionSegment("Edit", UiIconId.Edit, UiActionButton.ActionKind.Secondary);
        editButton.Disabled = !_canEdit;
        editButton.Pressed += () => EmitSignal(SignalName.EditRequested, _creationKey, _creationName);
        row.AddChild(editButton);

        if (_isExample)
        {
            row.AddChild(new LockedActionSegment
            {
                Tokens = _tokens,
                CustomMinimumSize = new Vector2(0, _tokens.TouchTarget),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            });
        }
        else
        {
            var deleteButton = CreateActionSegment("Delete", UiIconId.Trash, UiActionButton.ActionKind.Danger);
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
            Tokens = _tokens,
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
            stack.AddChild(CreateLabel(_achievementProgressText, _tokens.CaptionText, _tokens.Muted));
        }

        var track = new Panel
        {
            CustomMinimumSize = new Vector2(0, 6),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        track.AddThemeStyleboxOverride("panel", _tokens.ControlStyle(_tokens.Panel, _tokens.Edge, radius: (int)_tokens.RadiusPill));
        track.AddChild(new ColorRect
        {
            Color = _tokens.Accent,
            AnchorRight = _achievementProgress,
            AnchorBottom = 1,
            MouseFilter = MouseFilterEnum.Ignore,
        });
        stack.AddChild(track);
        return stack;
    }

    private Label CreateLabel(string text, UiTokens.TextStyle style, Color color, bool expand = false)
    {
        var label = new Label
        {
            Text = text,
            SizeFlagsHorizontal = expand ? SizeFlags.ExpandFill : SizeFlags.Fill,
            ClipText = true,
        };
        _tokens.ApplyTextStyle(label, style);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private string DisplayName() =>
        _isExample && _creationName.StartsWith("Example: ", StringComparison.OrdinalIgnoreCase)
            ? _creationName["Example: ".Length..]
            : _creationName;

    private UiActionButton CreateButton(string text, UiActionButton.ActionKind kind) =>
        new()
        {
            Tokens = _tokens,
            LabelText = text,
            Kind = kind,
            CustomMinimumSize = new Vector2(56, _tokens.TouchTarget),
        };

    private Button CreateActionSegment(string text, UiIconId iconId, UiActionButton.ActionKind kind, bool disabled = false)
    {
        var color = kind == UiActionButton.ActionKind.Danger ? _tokens.Danger : _tokens.Ink;
        var button = new Button
        {
            Text = text.ToUpperInvariant(),
            Disabled = disabled,
            CustomMinimumSize = new Vector2(0, _tokens.TouchTarget),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _tokens.ApplyTextStyle(button, _tokens.CaptionText);
        UiIcons.Apply(button, iconId, UiIconSize.Standard, color);
        button.AddThemeColorOverride("font_color", color);
        button.AddThemeColorOverride("font_disabled_color", _tokens.Muted);
        button.AddThemeColorOverride("font_hover_color", kind == UiActionButton.ActionKind.Danger ? _tokens.Danger : _tokens.Accent);
        button.AddThemeColorOverride("font_pressed_color", _tokens.OnAccent);
        button.AddThemeStyleboxOverride("normal", CreateSegmentStyle(Colors.Transparent, Colors.Transparent));
        button.AddThemeStyleboxOverride("hover", CreateSegmentStyle(kind == UiActionButton.ActionKind.Danger ? UiTokens.WithAlpha(_tokens.Danger, 0.16f) : _tokens.AccentSoft, _tokens.Edge));
        button.AddThemeStyleboxOverride("pressed", CreateSegmentStyle(kind == UiActionButton.ActionKind.Danger ? _tokens.Danger : _tokens.Accent, kind == UiActionButton.ActionKind.Danger ? _tokens.Danger : _tokens.Accent));
        button.AddThemeStyleboxOverride("focus", CreateSegmentStyle(_tokens.AccentSoft, _tokens.Accent));
        button.AddThemeStyleboxOverride("disabled", CreateSegmentStyle(Colors.Transparent, Colors.Transparent));
        return button;
    }

    private StyleBoxFlat CreateSegmentStyle(Color background, Color border) =>
        new()
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = border.A > 0 ? (int)_tokens.StrokeHair : 0,
            BorderWidthTop = border.A > 0 ? (int)_tokens.StrokeHair : 0,
            BorderWidthRight = border.A > 0 ? (int)_tokens.StrokeHair : 0,
            BorderWidthBottom = border.A > 0 ? (int)_tokens.StrokeHair : 0,
            CornerRadiusTopLeft = (int)_tokens.RadiusSmall,
            CornerRadiusTopRight = (int)_tokens.RadiusSmall,
            CornerRadiusBottomLeft = (int)_tokens.RadiusSmall,
            CornerRadiusBottomRight = (int)_tokens.RadiusSmall,
        };

    private sealed partial class CreatureThumbnail : Control
    {
        public UiTokens Tokens { get; init; } = UiTokens.Neon;

        public CreatureDef? Creature { get; init; }

        public string Summary { get; init; } = string.Empty;

        public override void _Draw()
        {
            DrawRect(new Rect2(Vector2.Zero, Size), Tokens.Background);
            DrawLine(new Vector2(0, Size.Y - 1), new Vector2(Size.X, Size.Y - 1), Tokens.Edge, Tokens.StrokeHair, antialiased: true);
            if (Creature is null || Creature.Nodes.Count == 0)
            {
                DrawString(ThemeDB.FallbackFont, new Vector2(16, Size.Y * 0.52f), Summary, HorizontalAlignment.Left, Size.X - 32, 12, Tokens.Muted);
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
                DrawLine(Map(points[beam.NodeA]), Map(points[beam.NodeB]), Tokens.Ink, 3, antialiased: true);
            }

            for (var i = 0; i < points.Length; i++)
            {
                var mapped = Map(points[i]);
                DrawCircle(mapped, 5.5f, Tokens.PanelRaised);
                DrawArc(mapped, 5.5f, 0, Mathf.Tau, 24, Tokens.Accent, 2, antialiased: true);
            }

            foreach (var core in Creature.Cores)
            {
                var mapped = Map(points[core.NodeIndex]);
                DrawCircle(mapped, 2.8f, Tokens.Accent);
            }
        }
    }

    private sealed partial class LockedActionSegment : Control
    {
        public UiTokens Tokens { get; init; } = UiTokens.Neon;

        public override void _Draw()
        {
            var centerX = Size.X * 0.5f;
            var iconY = 17f;
            var color = Tokens.Muted;
            var lockIcon = UiIcons.Load(UiIconId.Lock, UiIconSize.Standard);
            DrawTextureRect(lockIcon, new Rect2(centerX - 8, iconY - 8, 16, 16), false, color);

            DrawString(ThemeDB.FallbackFont, new Vector2(0, 40), "LOCK", HorizontalAlignment.Center, Size.X, 12, color);
        }
    }
}
