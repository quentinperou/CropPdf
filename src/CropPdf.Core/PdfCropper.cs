using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace CropPdf.Core;

public static class PdfCropper
{
    /// <summary>Smallest crop accepted, in points (about 1 mm).</summary>
    public const double MinimumSize = 3;

    /// <summary>
    /// Writes to <paramref name="destinationPath"/> (which may be the source itself) a PDF made of
    /// page <paramref name="pageIndex"/> only, cropped to <paramref name="selection"/>: the other pages are dropped.
    /// The file is written to a temporary file first, so a failure never leaves a half-written PDF.
    /// </summary>
    public static void Crop(string sourcePath, string destinationPath, int pageIndex, NormalizedRect selection)
    {
        // Read everything up front: the source is then free to be overwritten.
        using var input = new MemoryStream(File.ReadAllBytes(sourcePath));
        using var source = PdfReader.Open(input, PdfDocumentOpenMode.Import);

        if (pageIndex < 0 || pageIndex >= source.PageCount)
            throw new ArgumentOutOfRangeException(nameof(pageIndex), pageIndex, "The document has no such page.");

        // A new document holding the imported page only: its resources come along, the rest of the file does not.
        using var output = new PdfDocument();
        output.Version = Math.Max(output.Version, source.Version);
        CopyDocumentInfo(source, output);
        var page = output.AddPage(source.Pages[pageIndex]);
        CropPage(page, selection);

        string fullDestination = Path.GetFullPath(destinationPath);
        string directory = Path.GetDirectoryName(fullDestination)!;
        string temp = Path.Combine(directory, $"~{Path.GetFileNameWithoutExtension(fullDestination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            output.Save(temp);
            File.Move(temp, fullDestination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    private static void CopyDocumentInfo(PdfDocument source, PdfDocument output)
    {
        output.Info.Title = source.Info.Title;
        output.Info.Author = source.Info.Author;
        output.Info.Subject = source.Info.Subject;
        output.Info.Keywords = source.Info.Keywords;
        output.Info.Creator = source.Info.Creator;
    }

    /// <summary>
    /// Sets the MediaBox and CropBox of <paramref name="page"/> to the selected area, so every viewer,
    /// printer and converter sees the new page size. Content outside the box stays in the file (hidden).
    /// </summary>
    public static PdfBox CropPage(PdfPage page, NormalizedRect selection)
    {
        var visible = GetVisibleBox(page);
        var box = CropGeometry.ToPdfBox(selection, visible, page.Rotate).Intersect(visible);
        if (box.IsEmpty || box.Width < MinimumSize || box.Height < MinimumSize)
            throw new CropAreaTooSmallException();

        var rect = new PdfRectangle(new XPoint(box.Left, box.Bottom), new XPoint(box.Right, box.Top));
        page.MediaBox = rect;
        page.CropBox = rect;

        // These boxes must lie inside the MediaBox; once cropped they would just describe the old page.
        page.Elements.Remove("/BleedBox");
        page.Elements.Remove("/TrimBox");
        page.Elements.Remove("/ArtBox");
        return box;
    }

    /// <summary>Area shown by viewers: the CropBox clipped to the MediaBox (CropBox defaults to MediaBox).</summary>
    public static PdfBox GetVisibleBox(PdfPage page)
    {
        var media = ToBox(page.MediaBox);
        if (!page.HasCropBox)
            return media;

        var visible = ToBox(page.CropBox).Intersect(media);
        return visible.IsEmpty ? media : visible;
    }

    private static PdfBox ToBox(PdfRectangle r) => PdfBox.FromCorners(r.X1, r.Y1, r.X2, r.Y2);
}
