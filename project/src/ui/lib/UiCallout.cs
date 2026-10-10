using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Callout (<c>c_call</c>): a small note that points at a spot in a figure. Its position is data
/// set by the caller; kind only colours the border. Code may add <see cref="Lines"/> of live
/// values under the caption (#1064).
/// </summary>
[Tool]
[GlobalClass]
public partial class UiCallout : PanelContainer
{
    /// <summary>Shares chip's names: warn (the default), danger explains a refusal, ok marks a target.</summary>
    public enum CalloutKind
    {
        Warning,
        Danger,
        Ok,
    }

    private CalloutKind _kind;
    private string _text = string.Empty;
    private UiIconId _iconId = UiIconId.None;
    private UiIconCaption? _content;
    private VBoxContainer? _body;
    private MarginContainer? _linesBox;
    private VBoxContainer? _rows;
    private IReadOnlyList<UiCalloutLine>? _lines;
    private IReadOnlyList<UiCalloutLine>? _builtLines;
    private bool _linesBuilt;
    private readonly List<(UiLabel Label, UiMeterBar Bar)> _meters = [];
    private readonly List<UiLabel> _notes = [];

    private const string _bodyName = "_UiBody";
    private const string _linesName = "_UiLines";

    [Export]
    public string Text
    {
        get => _text;
        set
        {
            if (_text == value)
            {
                return;
            }

            _text = value;
            Refresh();
        }
    }

    [Export]
    public CalloutKind Kind
    {
        get => _kind;
        set
        {
            if (!Enum.IsDefined(value))
            {
                GD.PushError($"Invalid callout kind: {value}. Keeping {_kind}.");
                return;
            }

            if (_kind == value)
            {
                return;
            }

            _kind = value;
            Refresh();
        }
    }

    [Export]
    public UiIconId IconId
    {
        get => _iconId;
        set
        {
            if (!Enum.IsDefined(value))
            {
                GD.PushError($"Invalid callout icon: {value}. Keeping {_iconId}.");
                return;
            }

            if (_iconId == value)
            {
                return;
            }

            _iconId = value;
            Refresh();
        }
    }

    /// <summary>
    /// Lines under the caption (#1064), indented to the caption's text, or null for none. Set in
    /// code and never saved. A list of the same shape updates the labels and values in place;
    /// setting the same list again does nothing.
    /// </summary>
    public IReadOnlyList<UiCalloutLine>? Lines
    {
        get => _lines;
        set
        {
            if (ReferenceEquals(_lines, value))
            {
                return;
            }

            _lines = value;
            RefreshLines();
        }
    }

    public static UiTokens.Color BorderFor(CalloutKind kind) => kind switch
    {
        CalloutKind.Danger => UiTokens.Color.Danger,
        CalloutKind.Ok => UiTokens.Color.Accent,
        _ => UiTokens.Color.Halo,
    };

    public override void _EnterTree() => RequestReady();

    public UiCallout() => MouseFilter = MouseFilterEnum.Ignore;

    public override void _Ready() => Refresh();

    private readonly UiUnsavedState _unsaved = new("theme_override_styles/panel");

    public override void _Notification(int what)
    {
        if (_unsaved.Handle(this, what, Refresh))
        {
            return;
        }

        if (what == NotificationThemeChanged && IsNodeReady())
        {
            UiThemeRefresh.Guarded(this, Refresh);
        }
    }

    private void Refresh()
    {
        if (!IsInsideTree())
        {
            return;
        }

        // Re-found after a C# assembly reload, which clears managed fields but keeps the children.
        EnsureBody();

        UiTranslation.ShareContext(this, _content.Label);
        _content.Apply(
            Text,
            IconId,
            UiChip.IconSizeFor(IconId, glyphSized: false),
            UiTokens.Color.Ink,
            UiThemeLookup.Color(this, UiTokens.Color.Ink));
        AddThemeStyleboxOverride("panel", UiThemeLookup.CreateStyleBox(
            UiThemeLookup.Color(this, UiTokens.Color.Panel),
            UiThemeLookup.Color(this, BorderFor(Kind)),
            radius: UiSize.Radius.Medium,
            horizontalPadding: UiSize.Space.S2 + UiSize.Stroke.Hair,
            verticalPadding: UiSize.Space.S1 + UiSize.Stroke.Hair));
        RefreshLines();
    }

    // The caption row, then the lines below it; unowned, so never saved into a scene.
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_body), nameof(_content), nameof(_linesBox), nameof(_rows))]
    private void EnsureBody()
    {
        if (_body is not null && _content is not null && _linesBox is not null && _rows is not null)
        {
            return;
        }

        _body = GetNodeOrNull<VBoxContainer>(_bodyName);
        if (_body is null)
        {
            _body = new VBoxContainer { Name = _bodyName, MouseFilter = MouseFilterEnum.Ignore };
            _body.AddThemeConstantOverride("separation", 0);
            AddChild(_body);
        }

        _content = UiIconCaption.Ensure(_body);
        _linesBox = _body.GetNodeOrNull<MarginContainer>(_linesName);
        if (_linesBox is null)
        {
            _linesBox = new MarginContainer { Name = _linesName, MouseFilter = MouseFilterEnum.Ignore };
            _rows = new VBoxContainer { Name = "Rows", MouseFilter = MouseFilterEnum.Ignore };
            _rows.AddThemeConstantOverride("separation", 0);
            _linesBox.AddChild(_rows);
            _body.AddChild(_linesBox);
        }

        _rows = _linesBox.GetNode<VBoxContainer>("Rows");
    }

    private void RefreshLines()
    {
        if (!IsInsideTree() || _linesBox is null || _rows is null)
        {
            return;
        }

        IReadOnlyList<UiCalloutLine> lines = _lines ?? [];
        if (!_linesBuilt || !UiCalloutLine.SameShape(_builtLines, lines))
        {
            BuildLines(lines);
        }

        var meter = 0;
        var note = 0;
        foreach (var line in lines)
        {
            if (line.Note is { } text)
            {
                _notes[note++].Text = text;
                continue;
            }

            foreach (var value in line.Meters)
            {
                var (label, bar) = _meters[meter++];
                label.Text = value.Label;
                bar.Value = value.Value;
                bar.Centred = value.Centred;
                bar.FillColor = line.Color;
            }
        }

        _linesBox.Visible = lines.Count > 0;

        // With lines below it the caption starts at the left, so the lines sit under its text.
        if (_content is not null)
        {
            _content.Row.Alignment = lines.Count > 0 ? BoxContainer.AlignmentMode.Begin : BoxContainer.AlignmentMode.Center;
        }

        var indent = IconId == UiIconId.None ? 0 : UiIcons.Pixels(UiChip.IconSizeFor(IconId, glyphSized: false)) + UiSize.Space.S2;
        _linesBox.AddThemeConstantOverride("margin_left", indent);
    }

    private void BuildLines(IReadOnlyList<UiCalloutLine> lines)
    {
        foreach (var child in _rows!.GetChildren())
        {
            _rows.RemoveChild(child);
            child.QueueFree();
        }

        _meters.Clear();
        _notes.Clear();
        foreach (var line in lines)
        {
            if (line.Note is not null)
            {
                var note = SmallLabel();
                _notes.Add(note);
                _rows.AddChild(note);
                continue;
            }

            // Labels and bars take their own columns, so wrapped rows keep their bars lined up.
            var grid = new GridContainer { Columns = UiCalloutLine.MetersPerRow * 2, MouseFilter = MouseFilterEnum.Ignore };
            grid.AddThemeConstantOverride("h_separation", UiSize.Space.S1);
            grid.AddThemeConstantOverride("v_separation", 0);
            for (var index = 0; index < line.Meters.Count; index++)
            {
                var label = SmallLabel();
                var bar = new UiMeterBar();
                if (index % UiCalloutLine.MetersPerRow == 0)
                {
                    grid.AddChild(label);
                }
                else
                {
                    // The gap before the next meter is S2, wider than the S1 between a label and its bar.
                    var gap = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
                    gap.AddThemeConstantOverride("margin_left", UiSize.Space.S2 - UiSize.Space.S1);
                    gap.AddChild(label);
                    grid.AddChild(gap);
                }

                grid.AddChild(bar);
                _meters.Add((label, bar));
            }

            _rows.AddChild(grid);
        }

        _builtLines = lines;
        _linesBuilt = true;
    }

    // Line text arrives in the player's language, so it is not translated again.
    private static UiLabel SmallLabel() => new()
    {
        TextStyle = UiTokens.Typography.Caption,
        TextColor = UiTokens.Color.Muted,
        VerticalAlignment = VerticalAlignment.Center,
        MouseFilter = MouseFilterEnum.Ignore,
        AutoTranslateMode = AutoTranslateModeEnum.Disabled,
    };
}
