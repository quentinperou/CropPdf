namespace CropPdf.Core;

public enum PrintScaling
{
    /// <summary>Fill the printable area, keeping the proportions; turned a quarter if it fits better.</summary>
    FitToPaper,

    /// <summary>Real size (100 %), only reduced when larger than the printable area.</summary>
    ActualSize,
}

/// <summary>
/// Where the cropped area goes on the sheet: its printed size (before rotation) and whether it is turned a
/// quarter. All sizes are in the same unit (WPF units, 1/96 inch, in the app).
/// </summary>
public readonly record struct PrintLayout(double Width, double Height, bool Rotate)
{
    public static PrintLayout Compute(double width, double height, double areaWidth, double areaHeight, PrintScaling scaling)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "The area to print is empty.");
        if (areaWidth <= 0 || areaHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(areaWidth), "The printable area is empty.");

        double straight = Math.Min(areaWidth / width, areaHeight / height);
        if (scaling == PrintScaling.ActualSize)
        {
            double scale = Math.Min(1, straight);
            return new PrintLayout(width * scale, height * scale, Rotate: false);
        }

        // Turning only makes sense when it gives a bigger print, e.g. a landscape label on portrait label stock.
        double turned = Math.Min(areaWidth / height, areaHeight / width);
        bool rotate = turned > straight * 1.0001;
        double fit = rotate ? turned : straight;
        return new PrintLayout(width * fit, height * fit, rotate);
    }
}