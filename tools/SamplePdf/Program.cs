using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

// Writes a 3-page A4 test PDF covering the tricky cases of cropping:
//   page 1: plain portrait page;
//   page 2: /Rotate 90 (displayed in landscape);
//   page 3: already has a CropBox smaller than its MediaBox.
// Each page has a 1 cm grid and two labelled boxes ("TOP LEFT", "BOTTOM RIGHT") to check where a crop lands.
//
//   dotnet run --project tools\SamplePdf -- C:\temp\sample.pdf

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: SamplePdf <output.pdf>");
    return 1;
}

GlobalFontSettings.UseWindowsFontsUnderWindows = true;
var title = new XFont("Arial", 28, XFontStyleEx.Bold);
var small = new XFont("Arial", 12);

using var document = new PdfDocument();
for (int i = 0; i < 3; i++)
{
    var page = document.AddPage();
    page.Width = XUnit.FromMillimeter(210);
    page.Height = XUnit.FromMillimeter(297);
    using (var g = XGraphics.FromPdfPage(page))
    {
        double w = page.Width.Point, h = page.Height.Point, step = XUnit.FromCentimeter(1).Point;
        for (double x = 0; x < w; x += step)
            g.DrawLine(XPens.LightGray, x, 0, x, h);
        for (double y = 0; y < h; y += step)
            g.DrawLine(XPens.LightGray, 0, y, w, y);

        g.DrawRectangle(XBrushes.SteelBlue, 60, 60, 200, 120);
        g.DrawString("TOP LEFT", small, XBrushes.White, new XRect(60, 60, 200, 120), XStringFormats.Center);
        g.DrawRectangle(XBrushes.IndianRed, w - 260, h - 180, 200, 120);
        g.DrawString("BOTTOM RIGHT", small, XBrushes.White, new XRect(w - 260, h - 180, 200, 120), XStringFormats.Center);

        string note = i switch { 1 => "/Rotate 90", 2 => "existing CropBox", _ => "A4 portrait" };
        g.DrawString($"Page {i + 1}", title, XBrushes.Black, new XRect(0, h / 2 - 40, w, 80), XStringFormats.Center);
        g.DrawString(note, small, XBrushes.DimGray, new XRect(0, h / 2 + 20, w, 30), XStringFormats.Center);
    }

    if (i == 1)
        page.Rotate = 90;
    if (i == 2)
        page.CropBox = new PdfRectangle(new XPoint(40, 40), new XPoint(555, 802));
}

document.Save(args[0]);
Console.WriteLine($"Written: {Path.GetFullPath(args[0])}");
return 0;