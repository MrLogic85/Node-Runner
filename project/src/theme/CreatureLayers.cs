namespace NodeRunner.Theme;

/// <summary>
/// The named draw layers inside one drawn creature (#767), as ZIndex values relative to the
/// creature's root. Each part visual puts itself on its layer, so drawing never depends on the
/// order parts are added. A part draws its selection mark with itself, and a selected part moves
/// up to the selected layer of its kind (#766): it stays whole, mark and all, yet no part of the
/// same kind can hide it, as a Piston hid a crossed beam's lines.
/// </summary>
public static class CreatureLayers
{
    /// <summary>The rigid hatch inside closed triangles, under everything else.</summary>
    public const int Hatch = 0;

    /// <summary>What a view draws under the links, such as Build's placing feedback on a beam (#769).</summary>
    public const int Underlays = 1;

    /// <summary>Beams.</summary>
    public const int Beams = 2;

    /// <summary>Pistons, over the beams they cross.</summary>
    public const int Pistons = 3;

    /// <summary>A selected beam or Piston, over every other link it crosses.</summary>
    public const int SelectedLinks = 4;

    /// <summary>Sensor pictures on their beams.</summary>
    public const int Sensors = 5;

    /// <summary>A selected sensor picture.</summary>
    public const int SelectedSensors = 6;

    /// <summary>Joint rings, over the link ends and sensors they cover.</summary>
    public const int Joints = 7;

    /// <summary>A selected joint with its halo ring.</summary>
    public const int SelectedJoints = 8;

    /// <summary>What a view draws over the whole creature, such as Training's camera rays and Build's Select frame.</summary>
    public const int Overlays = 9;

    /// <summary>How many ZIndex steps one creature takes, so drawn creatures can be stacked without mixing.</summary>
    public const int Count = Overlays + 1;
}
