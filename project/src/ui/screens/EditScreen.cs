using Godot;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Screens;

/// <summary>Sample Edit state showing the safe Move-only training contract.</summary>
public partial class EditScreen : Control
{
    private UiTokens _tokens = UiTokens.Neon;
    private string _creationName = "First Walker";

    [Export]
    public string CreationName
    {
        get => _creationName;
        set
        {
            _creationName = value;
            if (IsInsideTree())
            {
                Rebuild();
            }
        }
    }

    [Signal]
    public delegate void DoneRequestedEventHandler();

    [Signal]
    public delegate void RebuildRequestedEventHandler();

    public UiTokens Tokens
    {
        get => _tokens;
        set
        {
            _tokens = value;
            if (IsInsideTree())
            {
                Rebuild();
            }
        }
    }

    public override void _Ready()
    {
        Name = nameof(EditScreen);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Size = GetViewportRect().Size;
        Rebuild();
    }

    private void Rebuild()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        AddChild(new ColorRect
        {
            Color = _tokens.Background,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorRight = 1,
            AnchorBottom = 1,
        });
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        AddChild(margin);
        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 12);
        margin.AddChild(layout);

        var header = new HBoxContainer();
        header.AddChild(CreateLabel("Edit " + _creationName, 22, _tokens.Ink, true));
        var done = CreateButton("Done", UiButtonKind.Primary);
        done.Pressed += () => EmitSignal(SignalName.DoneRequested);
        header.AddChild(done);
        layout.AddChild(header);
        layout.AddChild(CreateLabel("Move-only edit · training kept · generation 18", 14, _tokens.Accent));

        var content = new HBoxContainer();
        content.AddThemeConstantOverride("separation", 14);
        content.SizeFlagsVertical = SizeFlags.ExpandFill;
        content.AddChild(CreateCanvas());
        content.AddChild(CreateSafetyPanel());
        layout.AddChild(content);
    }

    private Control CreateCanvas()
    {
        var panel = new UiCard
        {
            Tokens = _tokens,
            Kind = UiCard.CardVariant.Frame,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        var canvas = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        canvas.Draw += () =>
        {
            var center = canvas.Size / 2;
            canvas.DrawLine(center + new Vector2(-100, 20), center + new Vector2(0, -30), _tokens.Accent, 6);
            canvas.DrawLine(center + new Vector2(0, -30), center + new Vector2(100, 16), _tokens.Accent, 6);
            // Purely decorative illustration -- effects-lite drops the glow
            // treatment for flat schematic dots instead of hiding them (#134).
            canvas.DrawCircle(center + new Vector2(0, -30), _tokens.EffectsEnabled ? 25 : 10, _tokens.EffectsEnabled ? _tokens.Halo : _tokens.LineStrong);
            canvas.DrawCircle(center + new Vector2(-100, 20), _tokens.EffectsEnabled ? 18 : 8, _tokens.EffectsEnabled ? _tokens.AccentGlow : _tokens.Line);
            canvas.DrawCircle(center + new Vector2(100, 16), _tokens.EffectsEnabled ? 18 : 8, _tokens.EffectsEnabled ? _tokens.AccentGlow : _tokens.Line);
            canvas.DrawString(ThemeDB.FallbackFont, new Vector2(16, 28), "Ghosted original position stays visible while Move is active.", HorizontalAlignment.Left, -1, 13, _tokens.Muted);
        };
        panel.AddChild(canvas);
        return panel;
    }

    private Control CreateSafetyPanel()
    {
        var panel = new UiCard { Tokens = _tokens, CustomMinimumSize = new Vector2(300, 0) };
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        panel.AddChild(margin);
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 8);
        margin.AddChild(stack);
        stack.AddChild(CreateLabel("Edit safely", 19, _tokens.Ink));
        stack.AddChild(CreateLabel("Only Move is active. Beam, Core, and Delete stay visible so you know what is protected.", 14, _tokens.Muted));
        foreach (var tool in new[] { "Beam · Move only · training kept", "Core · Move only · training kept", "Delete · Move only · training kept" })
        {
            var locked = new UiButton
            {
                Tokens = _tokens,
                LabelText = $"{tool} · Move only · training kept",
                IconId = UiIconId.Move,
                Enabled = false,
                TooltipText = "Move only · training kept",
            };
            stack.AddChild(locked);
        }
        var rebuild = new UiButton
        {
            Tokens = _tokens,
            LabelText = "Rebuild body",
            Kind = UiButtonKind.Tertiary,
        };
        rebuild.Pressed += () => EmitSignal(SignalName.RebuildRequested);
        stack.AddChild(rebuild);
        return panel;
    }

    private Label CreateLabel(string text, int size, Color color, bool expand = false)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.SizeFlagsHorizontal = expand ? SizeFlags.ExpandFill : SizeFlags.Fill;
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private UiButton CreateButton(string text, UiButtonKind kind)
    {
        return new UiButton
        {
            Tokens = _tokens,
            LabelText = text,
            Kind = kind,
            CustomMinimumSize = new Vector2(110, _tokens.TouchTarget),
        };
    }
}
