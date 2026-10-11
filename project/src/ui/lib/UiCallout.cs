using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// Callout (<c>c_call</c>): a small note that points at a spot in a figure. Its position is data
/// set by the caller; kind only colours the border. Code may add <see cref="Lines"/> of live
/// values under the caption (#1064), and further <see cref="Sections"/> below it (#1066). The
/// glyphs and line headings share a first column, as wide as the widest of them in any section,
/// and the captions, meters and notes start past it (#1150); see <see cref="UiCalloutGrid"/>.
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
    private VBoxContainer? _body;
    private Block? _first;
    private readonly List<Block> _sectionBlocks = [];
    private IReadOnlyList<UiCalloutLine>? _lines;
    private IReadOnlyList<UiCalloutSection>? _sections;

    private const string _bodyName = "_UiBody";
    private const string _sectionPrefix = "_UiSection";
    private const string _dividerPrefix = "_UiDivider";

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
    /// Lines under the caption (#1064), or null for none: each meter line's heading in the glyph's
    /// column, its meters and notes under the caption's text. Set in code and never saved. A list
    /// for the same grid (<see cref="UiCalloutGrid.SameGrid"/>) updates in place; setting the same
    /// list again does nothing.
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
            RefreshContent();
        }
    }

    /// <summary>
    /// More captions under this one (#1066), each with its own glyph and lines and a divider above
    /// it, or null for none: one callout about several things at one spot. Set in code and never
    /// saved. Sections of the same shape update in place; setting the same list again does nothing.
    /// </summary>
    public IReadOnlyList<UiCalloutSection>? Sections
    {
        get => _sections;
        set
        {
            if (ReferenceEquals(_sections, value))
            {
                return;
            }

            _sections = value;
            RefreshContent();
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

        AddThemeStyleboxOverride("panel", UiThemeLookup.CreateStyleBox(
            UiThemeLookup.Color(this, UiTokens.Color.Panel),
            UiThemeLookup.Color(this, BorderFor(Kind)),
            radius: UiSize.Radius.Medium,
            horizontalPadding: UiSize.Space.S2 + UiSize.Stroke.Hair,
            verticalPadding: UiSize.Space.S1 + UiSize.Stroke.Hair));
        RefreshContent();
    }

    private void RefreshContent()
    {
        if (!IsInsideTree())
        {
            return;
        }

        // Re-found after a C# assembly reload, which clears managed fields but keeps the children.
        EnsureBody();
        IReadOnlyList<UiCalloutSection> sections = _sections ?? [];
        if (_sectionBlocks.Count != sections.Count)
        {
            BuildSections(sections.Count);
        }

        UiTranslation.ShareContext(this, _first.Label);
        _first.Apply(this, Text, IconId, _lines ?? []);
        for (var index = 0; index < sections.Count; index++)
        {
            UiTranslation.ShareContext(this, _sectionBlocks[index].Label);
            _sectionBlocks[index].Apply(this, sections[index].Text, sections[index].IconId, sections[index].Lines ?? []);
        }

        // Every section shares the first column, so stacked captions and lines line up.
        var column = _sectionBlocks.Aggregate(_first.ColumnNeed, (widest, block) => Math.Max(widest, block.ColumnNeed));
        _first.SetColumn(column);
        foreach (var block in _sectionBlocks)
        {
            block.SetColumn(column);
        }
    }

    // The first caption and its lines straight in the body; unowned, so never saved into a scene.
    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_body), nameof(_first))]
    private void EnsureBody()
    {
        if (_body is not null && _first is not null)
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

        _first = Block.Ensure(_body);
        _sectionBlocks.Clear();
        foreach (var child in _body.GetChildren())
        {
            if (child.Name.ToString().StartsWith(_sectionPrefix, StringComparison.Ordinal)
                || child.Name.ToString().StartsWith(_dividerPrefix, StringComparison.Ordinal))
            {
                _body.RemoveChild(child);
                child.QueueFree();
            }
        }
    }

    // Each section is a divider and then a caption with its lines, as the first one.
    private void BuildSections(int count)
    {
        while (_sectionBlocks.Count > count)
        {
            var last = _sectionBlocks.Count;
            foreach (var name in new[] { $"{_dividerPrefix}{last}", $"{_sectionPrefix}{last}" })
            {
                var child = _body!.GetNode(name);
                _body.RemoveChild(child);
                child.QueueFree();
            }

            _sectionBlocks.RemoveAt(_sectionBlocks.Count - 1);
        }

        while (_sectionBlocks.Count < count)
        {
            var number = _sectionBlocks.Count + 1;
            var divider = new MarginContainer { Name = $"{_dividerPrefix}{number}", MouseFilter = MouseFilterEnum.Ignore };
            divider.AddThemeConstantOverride("margin_top", UiSize.Space.S1);
            divider.AddThemeConstantOverride("margin_bottom", UiSize.Space.S1);
            divider.AddChild(new UiDivider { Color = UiTokens.Color.Line });
            var section = new VBoxContainer { Name = $"{_sectionPrefix}{number}", MouseFilter = MouseFilterEnum.Ignore };
            section.AddThemeConstantOverride("separation", 0);
            _body!.AddChild(divider);
            _body.AddChild(section);
            _sectionBlocks.Add(Block.Ensure(section));
        }
    }

    // A caption and the lines under it (#1064), laid out by UiCalloutGrid (#1150).
    private sealed class Block
    {
        private const string _rowsName = "_UiRows";

        private readonly UiIconCaption _caption;
        private readonly VBoxContainer _rows;
        private readonly List<(UiLabel Label, UiMeterBar Bar)> _meters = [];
        private readonly List<(UiLabel Label, MarginContainer Box)> _notes = [];
        private readonly List<UiLabel> _headings = [];
        private IReadOnlyList<UiCalloutLine>? _builtLines;
        private bool _linesBuilt;
        private bool _builtWithColumn;
        private float _iconPixels;

        private Block(UiIconCaption caption, VBoxContainer rows)
        {
            _caption = caption;
            _rows = rows;
        }

        public static Block Ensure(VBoxContainer container)
        {
            var caption = UiIconCaption.Ensure(container);
            var rows = container.GetNodeOrNull<VBoxContainer>(_rowsName);
            if (rows is null)
            {
                rows = new VBoxContainer { Name = _rowsName, MouseFilter = MouseFilterEnum.Ignore };
                rows.AddThemeConstantOverride("separation", 0);
                container.AddChild(rows);
            }

            return new Block(caption, rows);
        }

        public UiLabel Label => _caption.Label;

        /// <summary>How wide this block wants the first column: its glyph or its widest heading; 0 without a column.</summary>
        public float ColumnNeed => _builtWithColumn
            ? _headings.Aggregate(_iconPixels, (widest, heading) => Math.Max(widest, heading.GetMinimumSize().X))
            : 0;

        public void Apply(Control owner, string text, UiIconId iconId, IReadOnlyList<UiCalloutLine> lines)
        {
            _caption.Apply(
                text,
                iconId,
                UiChip.IconSizeFor(iconId, glyphSized: false),
                UiTokens.Color.Ink,
                UiThemeLookup.Color(owner, UiTokens.Color.Ink));

            _iconPixels = iconId == UiIconId.None ? 0 : UiIcons.Pixels(UiChip.IconSizeFor(iconId, glyphSized: false));
            var withColumn = UiCalloutGrid.HasColumn(iconId, lines);
            if (!_linesBuilt || !UiCalloutGrid.SameGrid(_builtLines, _builtWithColumn, lines, withColumn))
            {
                BuildLines(lines, withColumn);
            }

            var meter = 0;
            var note = 0;
            var heading = 0;
            foreach (var line in lines)
            {
                if (line.Note is { } noteText)
                {
                    _notes[note++].Label.Text = noteText;
                    continue;
                }

                if (withColumn)
                {
                    _headings[heading++].Text = line.Heading ?? string.Empty;
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

            _rows.Visible = lines.Count > 0;

            // With lines below it the caption starts at the left, so the lines sit under its text.
            _caption.Row.Alignment = lines.Count > 0 ? BoxContainer.AlignmentMode.Begin : BoxContainer.AlignmentMode.Center;
        }

        /// <summary>
        /// Widens the first column to <paramref name="column"/>, so the caption, meters and notes
        /// start <see cref="UiSize.Space.S2"/> past it; a block without a column keeps none.
        /// </summary>
        public void SetColumn(float column)
        {
            var width = _builtWithColumn ? column : 0;
            if (width > 0)
            {
                // The glyph's cell spans the column, drawn at its left, so the caption's text starts
                // where the meters do, with or without a glyph.
                _caption.Icon.Visible = true;
                _caption.Icon.StretchMode = TextureRect.StretchModeEnum.KeepAspect;
                _caption.Icon.CustomMinimumSize = new Vector2(width, _iconPixels);
            }

            var lead = width > 0 ? width + UiSize.Space.S2 : 0;
            foreach (var heading in _headings)
            {
                // The grid adds S1 after the column, so the cell takes the rest of the S2 gap.
                heading.CustomMinimumSize = new Vector2(lead - UiSize.Space.S1, 0);
            }

            foreach (var (_, box) in _notes)
            {
                box.AddThemeConstantOverride("margin_left", (int)lead);
            }
        }

        private void BuildLines(IReadOnlyList<UiCalloutLine> lines, bool withColumn)
        {
            foreach (var child in _rows.GetChildren())
            {
                _rows.RemoveChild(child);
                child.QueueFree();
            }

            _meters.Clear();
            _notes.Clear();
            _headings.Clear();
            foreach (var line in lines)
            {
                if (line.Note is not null)
                {
                    var note = SmallLabel();
                    var box = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
                    box.AddChild(note);
                    _notes.Add((note, box));
                    _rows.AddChild(box);
                    continue;
                }

                // Labels and bars take their own columns, so wrapped rows keep their bars lined up.
                var grid = new GridContainer { Columns = UiCalloutGrid.Columns(withColumn), MouseFilter = MouseFilterEnum.Ignore };
                grid.AddThemeConstantOverride("h_separation", UiSize.Space.S1);
                grid.AddThemeConstantOverride("v_separation", 0);
                UiLabel? label = null;
                foreach (var cell in UiCalloutGrid.Cells(line, withColumn))
                {
                    switch (cell)
                    {
                        case UiCalloutGrid.Cell.Heading:
                            var heading = HeadingLabel();
                            _headings.Add(heading);
                            grid.AddChild(heading);
                            break;
                        case UiCalloutGrid.Cell.Blank:
                            grid.AddChild(new Control { MouseFilter = MouseFilterEnum.Ignore });
                            break;
                        case UiCalloutGrid.Cell.Label:
                            label = SmallLabel();
                            grid.AddChild(label);
                            break;
                        case UiCalloutGrid.Cell.SpacedLabel:
                            // The gap before the next meter is S2, wider than the S1 between a label and its bar.
                            label = SmallLabel();
                            var gap = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
                            gap.AddThemeConstantOverride("margin_left", UiSize.Space.S2 - UiSize.Space.S1);
                            gap.AddChild(label);
                            grid.AddChild(gap);
                            break;
                        case UiCalloutGrid.Cell.Bar:
                            var bar = new UiMeterBar();
                            grid.AddChild(bar);
                            _meters.Add((label!, bar));
                            break;
                    }
                }

                _rows.AddChild(grid);
            }

            _builtLines = lines;
            _linesBuilt = true;
            _builtWithColumn = withColumn;
        }

        // A field label's style (#1150): it names the line's group beside its colour.
        private static UiLabel HeadingLabel() => new()
        {
            TextStyle = UiTokens.Typography.Overline,
            TextColor = UiTokens.Color.Muted,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled,
        };

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
}
