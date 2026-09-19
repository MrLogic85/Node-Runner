using Godot;

namespace NodeRunner.Ui.Lib;

/// <summary>
/// App-agnostic design tokens consumed by reusable UI controls. Screens may
/// swap the complete set for paper or effects-lite presentation.
/// </summary>
public sealed class UiTokens
{
    public Color Background { get; init; }
    public Color Panel { get; init; }
    public Color PanelRaised { get; init; }
    public Color Line { get; init; }
    public Color LineStrong { get; init; }
    public Color Edge { get; init; }
    public Color Ink { get; init; }
    public Color Muted { get; init; }
    public Color Accent { get; init; }
    public Color AccentSoft { get; init; }
    public Color AccentGlow { get; init; }
    public Color Halo { get; init; }
    public Color OnAccent { get; init; }
    public Color Danger { get; init; }
    public float Radius { get; init; } = 4;
    public float TouchTarget { get; init; } = 48;
    public float LabelFontSize { get; init; } = 16;
    public bool EffectsEnabled { get; init; } = true;

    public static UiTokens Neon { get; } = new()
    {
        Background = new Color(0.02f, 0.03f, 0.09f),
        Panel = new Color(0.03f, 0.04f, 0.11f),
        PanelRaised = new Color(0.05f, 0.06f, 0.15f),
        Line = new Color(0.05f, 0.22f, 0.32f, 0.55f),
        LineStrong = new Color(0.00f, 0.82f, 1.00f),
        Edge = new Color(0.00f, 0.82f, 1.00f, 0.65f),
        Ink = new Color(0.92f, 0.95f, 1.00f),
        Muted = new Color(0.56f, 0.63f, 0.72f),
        Accent = new Color(0.00f, 0.95f, 0.82f),
        AccentSoft = new Color(0.00f, 0.95f, 0.82f, 0.18f),
        AccentGlow = new Color(0.00f, 0.95f, 0.82f, 0.35f),
        Halo = new Color(1.00f, 0.90f, 0.15f, 0.72f),
        OnAccent = new Color(0.01f, 0.04f, 0.06f),
        Danger = new Color(1.00f, 0.30f, 0.38f),
    };

    public static UiTokens Paper { get; } = new()
    {
        Background = new Color(0.94f, 0.95f, 0.93f),
        Panel = new Color(1.00f, 1.00f, 0.98f),
        PanelRaised = new Color(0.88f, 0.90f, 0.87f),
        Line = new Color(0.75f, 0.78f, 0.75f),
        LineStrong = new Color(0.12f, 0.20f, 0.22f),
        Edge = new Color(0.35f, 0.40f, 0.39f),
        Ink = new Color(0.06f, 0.08f, 0.09f),
        Muted = new Color(0.30f, 0.36f, 0.36f),
        Accent = new Color(0.00f, 0.42f, 0.38f),
        AccentSoft = new Color(0.00f, 0.42f, 0.38f, 0.14f),
        AccentGlow = new Color(0.00f, 0.42f, 0.38f, 0.20f),
        Halo = new Color(0.72f, 0.52f, 0.00f),
        OnAccent = new Color(1.00f, 1.00f, 0.98f),
        Danger = new Color(0.68f, 0.10f, 0.14f),
        EffectsEnabled = false,
    };

    public UiTokens WithEffects(bool enabled) => new()
    {
        Background = Background,
        Panel = Panel,
        PanelRaised = PanelRaised,
        Line = Line,
        LineStrong = LineStrong,
        Edge = Edge,
        Ink = Ink,
        Muted = Muted,
        Accent = Accent,
        AccentSoft = AccentSoft,
        AccentGlow = AccentGlow,
        Halo = Halo,
        OnAccent = OnAccent,
        Danger = Danger,
        Radius = Radius,
        TouchTarget = TouchTarget,
        LabelFontSize = LabelFontSize,
        EffectsEnabled = enabled,
    };
}
