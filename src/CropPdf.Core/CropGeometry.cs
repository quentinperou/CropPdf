namespace CropPdf.Core;

/// <summary>
/// Rectangle in PDF user space: points (1/72 inch), origin at the bottom-left, Y pointing up.
/// </summary>
public readonly record struct PdfBox(double Left, double Bottom, double Right, double Top)
{
    public double Width => Right - Left;
    public double Height => Top - Bottom;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static PdfBox FromCorners(double x1, double y1, double x2, double y2) =>
        new(Math.Min(x1, x2), Math.Min(y1, y2), Math.Max(x1, x2), Math.Max(y1, y2));

    public PdfBox Intersect(PdfBox other) => new(
        Math.Max(Left, other.Left),
        Math.Max(Bottom, other.Bottom),
        Math.Min(Right, other.Right),
        Math.Min(Top, other.Top));
}

/// <summary>
/// Rectangle relative to the page as it is displayed on screen (after /Rotate is applied):
/// every value is a fraction of the displayed page size, origin at the top-left, Y pointing down.
/// Using fractions keeps the selection independent from the zoom level and render resolution.
/// </summary>
public readonly record struct NormalizedRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;

    public static NormalizedRect FromCorners(double x1, double y1, double x2, double y2) =>
        new(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));

    /// <summary>Clips the rectangle to the page (0..1 on both axes).</summary>
    public NormalizedRect ClampToPage()
    {
        double x1 = Math.Clamp(X, 0, 1), y1 = Math.Clamp(Y, 0, 1);
        double x2 = Math.Clamp(Right, 0, 1), y2 = Math.Clamp(Bottom, 0, 1);
        return FromCorners(x1, y1, x2, y2);
    }
}

public static class CropGeometry
{
    /// <summary>Normalizes a /Rotate value to 0, 90, 180 or 270.</summary>
    public static int NormalizeRotation(int rotate)
    {
        int r = ((rotate % 360) + 360) % 360;
        return (int)(Math.Round(r / 90.0) * 90) % 360;
    }

    /// <summary>Size of the page as displayed (rotation applied), in points.</summary>
    public static (double Width, double Height) DisplaySize(PdfBox visible, int rotate) =>
        NormalizeRotation(rotate) is 90 or 270
            ? (visible.Height, visible.Width)
            : (visible.Width, visible.Height);

    /// <summary>
    /// Converts a point of the displayed page (u, v in 0..1, origin top-left) to PDF user space.
    /// <paramref name="visible"/> is the area the viewer shows (CropBox clipped to the MediaBox).
    /// /Rotate turns the page clockwise for display, so each case undoes that rotation.
    /// </summary>
    public static (double X, double Y) ToPdfPoint(double u, double v, PdfBox visible, int rotate)
    {
        var b = visible;
        return NormalizeRotation(rotate) switch
        {
            90 => (b.Left + v * b.Width, b.Bottom + u * b.Height),
            180 => (b.Right - u * b.Width, b.Bottom + v * b.Height),
            270 => (b.Right - v * b.Width, b.Top - u * b.Height),
            _ => (b.Left + u * b.Width, b.Top - v * b.Height),
        };
    }

    /// <summary>Converts a selection made on the displayed page to a box in PDF user space.</summary>
    public static PdfBox ToPdfBox(NormalizedRect selection, PdfBox visible, int rotate)
    {
        var s = selection.ClampToPage();
        var (x1, y1) = ToPdfPoint(s.X, s.Y, visible, rotate);
        var (x2, y2) = ToPdfPoint(s.Right, s.Bottom, visible, rotate);
        return PdfBox.FromCorners(x1, y1, x2, y2);
    }
}
