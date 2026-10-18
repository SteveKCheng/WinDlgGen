namespace WinDlgGen;

// All geometry is in dialog units (DLUs), as integers. Dialog units are
// relative to the dialog's font, so a layout computed once stays correct
// at any DPI: Windows converts to pixels when it creates the dialog.

/// <summary>The direction in which a stack places its children.</summary>
public enum Orientation
{
    Vertical,
    Horizontal,
}

/// <summary>
/// How a child is placed across its container's direction: horizontally
/// in a vertical stack, vertically in a horizontal one.
/// </summary>
public enum Alignment
{
    Start,
    Center,
    End,

    /// <summary>
    /// Take the whole space across the container. A control with an explicit
    /// size in that direction keeps it and is placed as for
    /// <see cref="Start"/>.
    /// </summary>
    Stretch,
}

/// <summary>A width and a height.</summary>
public readonly record struct Size(int Width, int Height)
{
    /// <summary>The size along <paramref name="o"/>.</summary>
    public int Along(Orientation o) => o == Orientation.Vertical ? Height : Width;

    /// <summary>The size across <paramref name="o"/>.</summary>
    public int Across(Orientation o) => o == Orientation.Vertical ? Width : Height;

    /// <summary>
    /// Builds a size from its extent along and across <paramref name="o"/>.
    /// </summary>
    public static Size FromAxes(Orientation o, int along, int across)
        => o == Orientation.Vertical ? new(across, along) : new(along, across);
}

/// <summary>A rectangle by its top-left corner and size.</summary>
public readonly record struct Rect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public Size Size => new(Width, Height);

    /// <summary>
    /// The rectangle with <paramref name="t"/> removed from each side.
    /// </summary>
    /// <remarks>
    /// Precondition: the rectangle is at least as large as the thickness,
    /// which holds for every rectangle the layout gives a node, since it is
    /// never smaller than the node's measured size.
    /// </remarks>
    public Rect Deflate(Thickness t)
        => new(X + t.Left, Y + t.Top, Width - t.Horizontal, Height - t.Vertical);

    /// <summary>
    /// Builds a rectangle from its position and size along and across
    /// <paramref name="o"/>.
    /// </summary>
    public static Rect FromAxes(Orientation o, int alongPos, int acrossPos, int along, int across)
        => o == Orientation.Vertical
            ? new(acrossPos, alongPos, across, along)
            : new(alongPos, acrossPos, along, across);
}

/// <summary>
/// The widths of the four sides of a margin or padding, all non-negative.
/// </summary>
public readonly record struct Thickness(int Top, int Right, int Bottom, int Left)
{
    public static Thickness Zero => default;

    public static Thickness Uniform(int value) => new(value, value, value, value);

    public int Horizontal => Left + Right;
    public int Vertical => Top + Bottom;

    /// <summary>The sum of both sides along <paramref name="o"/>.</summary>
    public int Along(Orientation o) => o == Orientation.Vertical ? Vertical : Horizontal;

    /// <summary>The sum of both sides across <paramref name="o"/>.</summary>
    public int Across(Orientation o) => o == Orientation.Vertical ? Horizontal : Vertical;

    /// <summary>The side at the start along <paramref name="o"/>: top or left.</summary>
    public int StartAlong(Orientation o) => o == Orientation.Vertical ? Top : Left;

    /// <summary>The side at the start across <paramref name="o"/>: left or top.</summary>
    public int StartAcross(Orientation o) => o == Orientation.Vertical ? Left : Top;

    /// <summary>
    /// Parses one to four non-negative integers separated by spaces, in the
    /// order CSS uses: "all", "vertical horizontal",
    /// "top horizontal bottom" or "top right bottom left".
    /// </summary>
    /// <returns>Whether <paramref name="s"/> had that form.</returns>
    public static bool TryParse(string s, out Thickness result)
    {
        result = default;
        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var values = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!Units.TryParse(parts[i], out values[i]))
                return false;
        }

        switch (values)
        {
            case [var a]: result = new(a, a, a, a); return true;
            case [var v, var h]: result = new(v, h, v, h); return true;
            case [var t, var h, var b]: result = new(t, h, b, h); return true;
            case [var t, var r, var b, var l]: result = new(t, r, b, l); return true;
            default: return false;
        }
    }
}

/// <summary>Limits on dialog-unit values.</summary>
public static class Units
{
    /// <summary>
    /// The largest coordinate or size a dialog template can hold: the
    /// fields of <c>DLGTEMPLATEEX</c> and <c>DLGITEMTEMPLATEEX</c> are
    /// signed 16-bit.
    /// </summary>
    public const int Max = short.MaxValue;

    /// <summary>
    /// Parses a non-negative decimal integer no greater than
    /// <see cref="Max"/>.
    /// </summary>
    public static bool TryParse(string s, out int value)
        => int.TryParse(s, System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out value)
           && value <= Max;
}
