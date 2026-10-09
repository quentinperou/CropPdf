using CropPdf.Core;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace CropPdf.Tests;

public sealed class PdfCropperTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("croppdf-tests-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    /// <summary>Creates a 3-page, 600 x 800 pt PDF. Page 2 gets the given rotation and crop box.</summary>
    private string CreatePdf(int secondPageRotate = 0, PdfRectangle? secondPageCropBox = null)
    {
        string path = Path.Combine(_dir, $"doc-{Guid.NewGuid():N}.pdf");
        using var document = new PdfDocument();
        for (int i = 0; i < 3; i++)
        {
            var page = document.AddPage();
            page.Width = XUnit.FromPoint(600);
            page.Height = XUnit.FromPoint(800);
            using (var gfx = XGraphics.FromPdfPage(page))
                gfx.DrawRectangle(XBrushes.SteelBlue, 50, 50, 200, 100);
            if (i == 1)
            {
                page.Rotate = secondPageRotate;
                if (secondPageCropBox is not null)
                    page.CropBox = secondPageCropBox;
            }
        }
        document.Save(path);
        return path;
    }

    private static PdfBox Box(PdfRectangle r) => PdfBox.FromCorners(r.X1, r.Y1, r.X2, r.Y2);

    private static PdfRectangle Rect(double x1, double y1, double x2, double y2) =>
        new(new XPoint(x1, y1), new XPoint(x2, y2));

    [Fact]
    public void Crop_KeepsOnlyTheCroppedPage()
    {
        string source = CreatePdf();
        string destination = Path.Combine(_dir, "out.pdf");

        PdfCropper.Crop(source, destination, pageIndex: 1, new NormalizedRect(0.25, 0.25, 0.5, 0.5));

        using var result = PdfReader.Open(destination, PdfDocumentOpenMode.Import);
        Assert.Equal(1, result.PageCount);
        var expected = new PdfBox(150, 200, 450, 600);
        Assert.Equal(expected, Box(result.Pages[0].MediaBox));
        Assert.Equal(expected, Box(result.Pages[0].CropBox));
        Assert.NotNull(result.Pages[0].Elements["/Contents"]);
    }

    [Fact]
    public void Crop_LeavesTheSourceUntouched_WhenWritingACopy()
    {
        string source = CreatePdf();
        byte[] before = File.ReadAllBytes(source);

        PdfCropper.Crop(source, Path.Combine(_dir, "out.pdf"), 0, new NormalizedRect(0, 0, 0.5, 0.5));

        Assert.Equal(before, File.ReadAllBytes(source));
    }

    [Fact]
    public void Crop_KeepsTheDocumentTitle()
    {
        string source = CreatePdf();
        using (var doc = PdfReader.Open(source, PdfDocumentOpenMode.Modify))
        {
            doc.Info.Title = "Plan du magasin";
            doc.Save(source);
        }
        string destination = Path.Combine(_dir, "out.pdf");

        PdfCropper.Crop(source, destination, 2, new NormalizedRect(0, 0, 0.5, 0.5));

        using var result = PdfReader.Open(destination, PdfDocumentOpenMode.Import);
        Assert.Equal("Plan du magasin", result.Info.Title);
    }

    [Fact]
    public void Crop_RotatedPage_KeepsRotationAndMapsTheSelection()
    {
        string source = CreatePdf(secondPageRotate: 90);
        string destination = Path.Combine(_dir, "out.pdf");

        // Left half of the displayed (landscape) page = lower half of the PDF page.
        PdfCropper.Crop(source, destination, 1, new NormalizedRect(0, 0, 0.5, 1));

        using var result = PdfReader.Open(destination, PdfDocumentOpenMode.Import);
        Assert.Equal(1, result.PageCount);
        Assert.Equal(90, result.Pages[0].Rotate);
        Assert.Equal(new PdfBox(0, 0, 600, 400), Box(result.Pages[0].MediaBox));
    }

    [Fact]
    public void Crop_PageWithExistingCropBox_IsRelativeToTheVisibleArea()
    {
        string source = CreatePdf(secondPageCropBox: Rect(100, 100, 500, 700));
        string destination = Path.Combine(_dir, "out.pdf");

        PdfCropper.Crop(source, destination, 1, new NormalizedRect(0, 0, 0.5, 0.5));

        using var result = PdfReader.Open(destination, PdfDocumentOpenMode.Import);
        Assert.Equal(new PdfBox(100, 400, 300, 700), Box(result.Pages[0].CropBox));
    }

    [Fact]
    public void Crop_CanOverwriteTheSourceFile()
    {
        string source = CreatePdf();

        PdfCropper.Crop(source, source, 0, new NormalizedRect(0, 0, 0.5, 0.5));

        using var result = PdfReader.Open(source, PdfDocumentOpenMode.Import);
        Assert.Equal(1, result.PageCount);
        Assert.Equal(new PdfBox(0, 400, 300, 800), Box(result.Pages[0].MediaBox));
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void Crop_TinySelection_IsRejected_AndWritesNothing()
    {
        string source = CreatePdf();
        string destination = Path.Combine(_dir, "out.pdf");

        Assert.Throws<CropAreaTooSmallException>(() =>
            PdfCropper.Crop(source, destination, 0, new NormalizedRect(0.5, 0.5, 0.001, 0.001)));
        Assert.False(File.Exists(destination));
    }

    [Fact]
    public void Crop_RemovesStaleTrimAndBleedBoxes()
    {
        string source = CreatePdf();
        using (var doc = PdfReader.Open(source, PdfDocumentOpenMode.Modify))
        {
            doc.Pages[0].TrimBox = Rect(10, 10, 590, 790);
            doc.Pages[0].BleedBox = Rect(0, 0, 600, 800);
            doc.Save(source);
        }

        PdfCropper.Crop(source, source, 0, new NormalizedRect(0, 0, 0.5, 0.5));

        using var result = PdfReader.Open(source, PdfDocumentOpenMode.Import);
        Assert.False(result.Pages[0].Elements.ContainsKey("/TrimBox"));
        Assert.False(result.Pages[0].Elements.ContainsKey("/BleedBox"));
    }

    [Fact]
    public void GetCropCopyPath_AddsSuffix_AndNeverReusesAnExistingName()
    {
        string source = Path.Combine(_dir, "plan.pdf");
        Assert.Equal(Path.Combine(_dir, "plan_crop.pdf"), OutputPath.GetCropCopyPath(source));

        File.WriteAllText(Path.Combine(_dir, "plan_crop.pdf"), "");
        Assert.Equal(Path.Combine(_dir, "plan_crop_2.pdf"), OutputPath.GetCropCopyPath(source));

        File.WriteAllText(Path.Combine(_dir, "plan_crop_2.pdf"), "");
        Assert.Equal(Path.Combine(_dir, "plan_crop_3.pdf"), OutputPath.GetCropCopyPath(source));
    }
}
