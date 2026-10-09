using System.IO;
using System.Printing;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CropPdf.Core;

namespace CropPdf;

/// <summary>The selected area of a page, to print.</summary>
/// <param name="Width">Size of the area at 100 %, in WPF units (1/96 inch), rotation applied.</param>
public sealed record PrintRequest(PdfPageRenderer Renderer, int PageIndex, NormalizedRect Area,
    double Width, double Height, string JobName);

/// <summary>A printer of the print window.</summary>
public sealed record PrinterInfo(string Name, bool IsDefault)
{
    public override string ToString() => Name;
}

/// <summary>
/// The sheet a printer will use, in portrait and in WPF units: what the layout and the page need.
/// The ticket is kept as XML so that it can move between threads.
/// </summary>
public sealed record PrintSheet(string TicketXml, Size Size, Rect Imageable, int Dpi);

/// <summary>
/// Printing without the Windows print dialog. Driver calls (capabilities, ticket validation) can take a while,
/// network printers even more: they run on a background STA thread, with their own print queue objects.
/// </summary>
public static class Printing
{
    public static Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync() => RunStaAsync<IReadOnlyList<PrinterInfo>>(() =>
    {
        using var server = new LocalPrintServer();
        string? defaultName = null;
        try
        {
            defaultName = server.DefaultPrintQueue?.FullName;
        }
        catch (PrintSystemException)
        {
        }
        using var queues = server.GetPrintQueues([EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections]);
        return queues
            .Select(q => new PrinterInfo(q.FullName, q.FullName == defaultName))
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    });

    /// <summary>Paper sizes of a printer, and the one its current settings use.</summary>
    public static Task<(IReadOnlyList<PaperSize> Papers, string? CurrentId)> GetPapersAsync(string printer) => RunStaAsync(() =>
    {
        using var queue = OpenQueue(printer);
        using var capabilities = queue.GetPrintCapabilitiesAsXml();
        using var ticket = (queue.UserPrintTicket ?? queue.DefaultPrintTicket).GetXmlStream();
        return (PrintSchema.ReadPaperSizes(capabilities), PrintSchema.ReadPaperSizeId(ticket));
    });

    /// <summary>The sheet of <paramref name="printer"/> with its current settings and <paramref name="paper"/>.</summary>
    public static Task<PrintSheet> GetSheetAsync(string printer, PaperSize? paper) => RunStaAsync(() =>
    {
        using var queue = OpenQueue(printer);
        return DescribeSheet(queue, queue.UserPrintTicket ?? queue.DefaultPrintTicket, paper);
    });

    /// <summary>The sheet of a ticket chosen in the Windows print dialog (turned back to portrait).</summary>
    public static Task<PrintSheet> GetSheetAsync(string printer, string ticketXml) => RunStaAsync(() =>
    {
        using var queue = OpenQueue(printer);
        return DescribeSheet(queue, LoadTicket(ticketXml), paper: null);
    });

    /// <summary>Renders the area at the printer resolution and sends it as a one-page job.</summary>
    public static async Task PrintAsync(PrintRequest request, string printer, PrintSheet sheet, PrintScaling scaling)
    {
        var layout = PrintLayout.Compute(request.Width, request.Height, sheet.Imageable.Width, sheet.Imageable.Height, scaling);
        uint pixelWidth = (uint)Math.Clamp(Math.Ceiling(layout.Width / 96 * sheet.Dpi), 16, 12000);
        var bitmap = await request.Renderer.RenderAreaAsync((uint)request.PageIndex, request.Area, pixelWidth);

        var page = BuildPage(bitmap, sheet, layout);
        page.Measure(sheet.Size);
        page.Arrange(new Rect(sheet.Size));
        page.UpdateLayout();

        using var queue = OpenQueue(printer);
        queue.CurrentJobSettings.Description = request.JobName;
        PrintQueue.CreateXpsDocumentWriter(queue).Write(page, LoadTicket(sheet.TicketXml));
    }

    /// <summary>
    /// The printed sheet: the area laid out in the printable part of the paper. The print window shows the same
    /// element (with a preview bitmap), so the preview is what comes out of the printer.
    /// </summary>
    public static Grid BuildPage(ImageSource image, PrintSheet sheet, PrintLayout layout)
    {
        var picture = new Image
        {
            Source = image,
            Stretch = Stretch.Fill,
            Width = layout.Width,
            Height = layout.Height,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            LayoutTransform = layout.Rotate ? new RotateTransform(90) : Transform.Identity,
        };
        RenderOptions.SetBitmapScalingMode(picture, BitmapScalingMode.HighQuality);

        var printable = new Grid
        {
            Width = sheet.Imageable.Width,
            Height = sheet.Imageable.Height,
            Margin = new Thickness(sheet.Imageable.X, sheet.Imageable.Y, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        printable.Children.Add(picture);

        var page = new Grid { Width = sheet.Size.Width, Height = sheet.Size.Height };
        page.Children.Add(printable);
        return page;
    }

    // Selects the paper (by its driver name, labels included) and portrait, then lets the driver complete the ticket.
    private static PrintSheet DescribeSheet(PrintQueue queue, PrintTicket baseTicket, PaperSize? paper)
    {
        var delta = LoadTicket(PrintSchema.PaperTicket(paper?.Id));
        var ticket = queue.MergeAndValidatePrintTicket(baseTicket, delta).ValidatedPrintTicket;

        double width = paper is not null ? FromMillimeters(paper.WidthMm) : ticket.PageMediaSize?.Width ?? 0;
        double height = paper is not null ? FromMillimeters(paper.HeightMm) : ticket.PageMediaSize?.Height ?? 0;
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("The printer does not report the size of its paper.");
        var size = new Size(width, height);

        // Origin and extent of the printable part, from the driver's margins; the whole sheet if unknown.
        var area = queue.GetPrintCapabilities(ticket).PageImageableArea;
        var imageable = area is { ExtentWidth: > 0, ExtentHeight: > 0 }
            ? Rect.Intersect(new Rect(area.OriginWidth, area.OriginHeight, area.ExtentWidth, area.ExtentHeight), new Rect(size))
            : new Rect(size);
        if (imageable.IsEmpty)
            imageable = new Rect(size);

        using var xml = ticket.GetXmlStream();
        using var reader = new StreamReader(xml);
        return new PrintSheet(reader.ReadToEnd(), size, imageable, ticket.PageResolution?.X ?? 300);
    }

    private static double FromMillimeters(double millimeters) => millimeters / 25.4 * 96;

    public static PrintTicket LoadTicket(string xml) => new(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

    /// <summary>Opens a queue by the name <see cref="GetPrintersAsync"/> gives (\\server\printer for a shared one).</summary>
    public static PrintQueue OpenQueue(string name)
    {
        if (name.StartsWith(@"\\", StringComparison.Ordinal) && name.IndexOf('\\', 2) is > 2 and var slash)
            return new PrintServer(name[..slash]).GetPrintQueue(name[(slash + 1)..]);
        return new LocalPrintServer().GetPrintQueue(name);
    }

    private static Task<T> RunStaAsync<T>(Func<T> work)
    {
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                result.SetResult(work());
            }
            catch (Exception ex)
            {
                result.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return result.Task;
    }
}
