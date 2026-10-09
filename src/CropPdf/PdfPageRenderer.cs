using System.IO;
using System.Windows.Media.Imaging;
using CropPdf.Core;
using WinImaging = Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WinPdf = Windows.Data.Pdf;

namespace CropPdf;

/// <summary>Renders pages with the PDF engine built into Windows (Windows.Data.Pdf).</summary>
public sealed class PdfPageRenderer : IDisposable
{
    private readonly WinPdf.PdfDocument _document;
    private readonly IRandomAccessStream _source;

    private PdfPageRenderer(WinPdf.PdfDocument document, IRandomAccessStream source)
    {
        _document = document;
        _source = source;
    }

    public uint PageCount => _document.PageCount;

    public static async Task<PdfPageRenderer> LoadAsync(string path)
    {
        // Work on an in-memory copy: the file stays free to be overwritten while it is displayed.
        byte[] bytes = await File.ReadAllBytesAsync(path);
        var source = new MemoryStream(bytes).AsRandomAccessStream();
        try
        {
            var document = await WinPdf.PdfDocument.LoadFromStreamAsync(source);
            return new PdfPageRenderer(document, source);
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    /// <summary>Displayed size of a page (CropBox and rotation applied), in WPF units.</summary>
    public (double Width, double Height) GetPageSize(uint index)
    {
        using var page = _document.GetPage(index);
        return (page.Size.Width, page.Size.Height);
    }

    public Task<BitmapSource> RenderAsync(uint index, uint pixelWidth) =>
        RenderAsync(index, new WinPdf.PdfPageRenderOptions { DestinationWidth = pixelWidth });

    /// <summary>Renders only <paramref name="area"/> of the page (as displayed), e.g. to print a crop.</summary>
    public Task<BitmapSource> RenderAreaAsync(uint index, NormalizedRect area, uint pixelWidth)
    {
        using var page = _document.GetPage(index);
        // SourceRect is in displayed page units (rotation applied), like NormalizedRect.
        var size = page.Size;
        var source = new Windows.Foundation.Rect(area.X * size.Width, area.Y * size.Height, area.Width * size.Width, area.Height * size.Height);
        uint pixelHeight = (uint)Math.Max(1, Math.Round(pixelWidth * source.Height / source.Width));
        return RenderAsync(index, new WinPdf.PdfPageRenderOptions
        {
            SourceRect = source,
            DestinationWidth = pixelWidth,
            DestinationHeight = pixelHeight,
        });
    }

    private async Task<BitmapSource> RenderAsync(uint index, WinPdf.PdfPageRenderOptions options)
    {
        using var page = _document.GetPage(index);
        using var output = new InMemoryRandomAccessStream();
        // BMP is much faster to encode and decode than the default PNG.
        options.BitmapEncoderId = WinImaging.BitmapEncoder.BmpEncoderId;
        await page.RenderToStreamAsync(output, options);
        output.Seek(0);

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = output.AsStream();
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    public void Dispose() => _source.Dispose();
}
