using Godot;
using NodeRunner.App.ViewModels;
using NodeRunner.Ui.Lib;

namespace NodeRunner.Ui.Widgets;

/// <summary>
/// The Brain setup picture: one column of neurons per layer, senses first and outputs last, with
/// every connection between neighbouring layers. Columns share the width equally, so the scene's
/// header and count rows line up with them when they split the same width the same way.
/// </summary>
public partial class BrainSetupNetwork : Control
{
    private BrainSetupPresentation? _setup;

    public BrainSetupNetwork() => MouseFilter = MouseFilterEnum.Ignore;

    public BrainSetupPresentation? Setup
    {
        get => _setup;
        set
        {
            _setup = value;
            QueueRedraw();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationThemeChanged)
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (_setup is not { HasAnatomy: true } setup)
        {
            return;
        }

        var columns = setup.Columns.Select((column, index) => NeuronPoints(index, setup.Columns.Count, column.Shown)).ToArray();
        var line = UiThemeLookup.Color(this, UiTokens.Color.Line) with { A = 0.55f };
        for (var index = 0; index < columns.Length - 1; index++)
        {
            foreach (var from in columns[index])
            {
                foreach (var to in columns[index + 1])
                {
                    DrawLine(from, to, line, UiSize.Stroke.Hair, antialiased: false);
                }
            }
        }

        var radius = UiSize.Space.S1 + 1;
        var glows = UiThemeLookup.EffectsEnabled(this);
        for (var index = 0; index < columns.Length; index++)
        {
            var fill = index == 0
                ? UiThemeLookup.Color(this, UiTokens.Color.Accent)
                : index == columns.Length - 1
                    ? UiThemeLookup.Color(this, UiTokens.Color.Output)
                    : (Color?)null;
            foreach (var point in columns[index])
            {
                if (fill is { } color)
                {
                    DrawCircle(point, radius * 2, UiGlow.FromBase(color, glows), antialiased: false);
                    DrawCircle(point, radius, color, antialiased: false);
                    continue;
                }

                DrawCircle(point, radius, UiThemeLookup.Color(this, UiTokens.Color.Panel), antialiased: false);
                DrawArc(point, radius, 0, Mathf.Tau, 24, UiThemeLookup.Color(this, UiTokens.Color.LineStrong), 1.5f, antialiased: false);
            }
        }
    }

    private Vector2[] NeuronPoints(int column, int columnCount, int shown)
    {
        var x = Size.X * (column + 0.5f) / columnCount;
        return Enumerable.Range(0, shown)
            .Select(neuron => new Vector2(x, Size.Y * (neuron + 0.5f) / shown))
            .ToArray();
    }
}
