using System.Globalization;
using System.IO;
using System.Printing;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using CropPdf.Core;

namespace CropPdf;

/// <summary>
/// Print window: printer, paper (the labels of a label printer included), fit or actual size, and a preview of the
/// sheet as it will be printed. The printer and paper used last are preselected: printing again is one click (Enter).
/// "More settings…" hands over to the Windows print dialog for the driver's own options.
/// </summary>
public partial class PrintWindow : Window
{
    // Enough for a sharp preview of a full page; printing renders again at the printer resolution.
    private const uint PreviewPixelWidth = 1200;

    private readonly PrintRequest _request;
    private readonly AppSettings _settings = AppSettings.Load();
    private BitmapSource? _preview;
    private PrintSheet? _sheet;
    private int _version; // a newer printer or paper choice makes the pending results obsolete
    private bool _fillingPapers;

    public PrintWindow(PrintRequest request)
    {
        InitializeComponent();
        _request = request;
        (_settings.PrintScaling == PrintScaling.ActualSize ? ActualSizeRadio : FitRadio).IsChecked = true;
        Loaded += async (_, _) => await LoadAsync();
    }

    /// <summary>Printer the page was sent to, once the window has closed after printing.</summary>
    public string? PrintedOn { get; private set; }

    private PrintScaling Scaling => ActualSizeRadio.IsChecked == true ? PrintScaling.ActualSize : PrintScaling.FitToPaper;
    private PrinterInfo? Printer => PrinterBox.SelectedItem as PrinterInfo;
    private PaperSize? Paper => (PaperBox.SelectedItem as PaperItem)?.Paper;

    private async Task LoadAsync()
    {
        SetBusy(Strings.Get("PrintLoading"));
        try
        {
            _preview = await _request.Renderer.RenderAreaAsync((uint)_request.PageIndex, _request.Area, PreviewPixelWidth);
            var printers = await Printing.GetPrintersAsync();
            if (printers.Count == 0)
            {
                ShowError(Strings.Get("PrintNoPrinter"));
                return;
            }
            PrinterBox.ItemsSource = printers;
            // Loads the papers through PrinterBox_SelectionChanged.
            PrinterBox.SelectedItem = printers.FirstOrDefault(p => p.Name == _settings.PrintPrinter)
                ?? printers.FirstOrDefault(p => p.IsDefault)
                ?? printers[0];
        }
        catch (Exception ex)
        {
            ShowError(Strings.Format("ErrorPrinter", ex.Message));
        }
    }

    private async void PrinterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Printer is not { } printer)
            return;

        int version = ++_version;
        _sheet = null;
        SetBusy(Strings.Get("PrintLoading"));
        try
        {
            var (papers, currentId) = await Printing.GetPapersAsync(printer.Name);
            if (version != _version)
                return;

            var items = papers.Select(p => new PaperItem(p)).ToList();
            string? wanted = _settings.PrintPapers.GetValueOrDefault(printer.Name) ?? currentId;
            _fillingPapers = true;
            PaperBox.ItemsSource = items;
            PaperBox.SelectedItem = items.FirstOrDefault(i => i.Paper.Id == wanted)
                ?? items.FirstOrDefault(i => i.Paper.Id == currentId)
                ?? items.FirstOrDefault();
            _fillingPapers = false;
            await LoadSheetAsync(version);
        }
        catch (Exception ex)
        {
            _fillingPapers = false;
            if (version == _version)
                ShowError(Strings.Format("ErrorPrinter", ex.Message));
        }
    }

    private async void PaperBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_fillingPapers)
            await LoadSheetAsync(++_version);
    }

    private async Task LoadSheetAsync(int version)
    {
        if (Printer is not { } printer)
            return;

        SetBusy(Strings.Get("PrintLoading"));
        try
        {
            var sheet = await Printing.GetSheetAsync(printer.Name, Paper);
            if (version != _version)
                return;
            _sheet = sheet;
            ShowPreview();
            SetReady();
        }
        catch (Exception ex)
        {
            if (version == _version)
                ShowError(Strings.Format("ErrorPrinter", ex.Message));
        }
    }

    private void Scaling_Checked(object sender, RoutedEventArgs e) => ShowPreview();

    private void ShowPreview()
    {
        if (_sheet is not { } sheet || _preview is null)
            return;

        var layout = PrintLayout.Compute(_request.Width, _request.Height, sheet.Imageable.Width, sheet.Imageable.Height, Scaling);
        var page = Printing.BuildPage(_preview, sheet, layout);

        // The printable part of the sheet (inside the driver's margins), in the preview only.
        double stroke = Math.Max(sheet.Size.Width, sheet.Size.Height) / 400;
        var printable = new Rectangle
        {
            Width = sheet.Imageable.Width,
            Height = sheet.Imageable.Height,
            Margin = new Thickness(sheet.Imageable.X, sheet.Imageable.Y, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            StrokeThickness = stroke,
            StrokeDashArray = [4, 3],
            Opacity = 0.6,
        };
        printable.SetResourceReference(Shape.StrokeProperty, "AccentFillColorDefaultBrush");
        page.Children.Add(printable);
        PaperSheet.Child = page;
        PaperSheet.Visibility = Visibility.Visible;

        // Size of the area as it lies on the sheet.
        double width = layout.Rotate ? layout.Height : layout.Width;
        double height = layout.Rotate ? layout.Width : layout.Height;
        SizeText.Text = Strings.Format("PrintSizeInfo", Millimeters(width / 96 * 25.4), Millimeters(height / 96 * 25.4),
                            Math.Round(layout.Width / _request.Width * 100))
            + (layout.Rotate ? Strings.Get("PrintTurnedInfo") : "");
    }

    private async void Print_Click(object sender, RoutedEventArgs e)
    {
        if (Printer is not { } printer || _sheet is not { } sheet)
            return;
        if (Paper is { } paper)
            _settings.PrintPapers[printer.Name] = paper.Id;
        await PrintAsync(printer.Name, sheet);
    }

    // The Windows print dialog, for what this window does not offer (copies, the driver's cutting options...).
    // Its paper is used as is; the orientation goes back to portrait since the layout turns the area itself.
    private async void MoreSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PrintDialog();
        if (Printer is { } printer)
        {
            try
            {
                dialog.PrintQueue = Printing.OpenQueue(printer.Name);
                if (_sheet is not null)
                    dialog.PrintTicket = Printing.LoadTicket(_sheet.TicketXml);
            }
            catch (PrintSystemException)
            {
                // The dialog then starts on the default printer.
            }
        }
        if (dialog.ShowDialog() != true)
            return;

        string chosen = dialog.PrintQueue.FullName;
        SetBusy(Strings.Get("PrintLoading"));
        PrintSheet sheet;
        try
        {
            using var xml = dialog.PrintTicket.GetXmlStream();
            using var reader = new StreamReader(xml);
            sheet = await Printing.GetSheetAsync(chosen, reader.ReadToEnd());
        }
        catch (Exception ex)
        {
            ShowError(Strings.Format("ErrorPrint", ex.Message));
            return;
        }
        await PrintAsync(chosen, sheet);
    }

    private async Task PrintAsync(string printer, PrintSheet sheet)
    {
        SetBusy(message: null);
        try
        {
            await Printing.PrintAsync(_request, printer, sheet, Scaling);
        }
        catch (Exception ex)
        {
            ShowError(Strings.Format("ErrorPrint", ex.Message));
            return;
        }

        _settings.PrintPrinter = printer;
        _settings.PrintScaling = Scaling;
        _settings.Save();
        PrintedOn = printer;
        DialogResult = true;
    }

    // ---------------------------------------------------------------- State

    private void SetBusy(string? message)
    {
        BusyBar.Visibility = Visibility.Visible;
        PrintButton.IsEnabled = false;
        MoreSettingsButton.IsEnabled = false;
        ShowStatus(message, error: false);
    }

    private void SetReady()
    {
        BusyBar.Visibility = Visibility.Collapsed;
        PrintButton.IsEnabled = _sheet is not null;
        MoreSettingsButton.IsEnabled = true;
        ShowStatus(null, error: false);
    }

    private void ShowError(string message)
    {
        SetReady();
        ShowStatus(message, error: true);
    }

    private void ShowStatus(string? message, bool error)
    {
        StatusText.Text = message ?? "";
        StatusText.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
        StatusText.SetResourceReference(TextBlock.ForegroundProperty,
            error ? "SystemFillColorCriticalBrush" : "TextFillColorSecondaryBrush");
    }

    private static string Millimeters(double value) => value.ToString("0.#", CultureInfo.CurrentCulture);

    private sealed partial record PaperItem(PaperSize Paper)
    {
        public override string ToString()
        {
            string size = Strings.Format("PrintPaperSize", Millimeters(Paper.WidthMm), Millimeters(Paper.HeightMm));
            // Many drivers already put the size in the name ("62mm x 100mm", "Letter (215.9 x 279.4 mm)").
            return Paper.DisplayName switch
            {
                null => size,
                { } name when Dimensions().IsMatch(name) && Unit().IsMatch(name) => name,
                { } name => Strings.Format("PrintPaperNamed", name, size),
            };
        }

        [GeneratedRegex(@"\d\s*[x×]\s*\d", RegexOptions.IgnoreCase)]
        private static partial Regex Dimensions();

        [GeneratedRegex(@"\d\s*(mm|cm|in\b|"")", RegexOptions.IgnoreCase)]
        private static partial Regex Unit();
    }
}
