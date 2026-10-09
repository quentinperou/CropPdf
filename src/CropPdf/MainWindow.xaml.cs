using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CropPdf.Core;
using Microsoft.Win32;

namespace CropPdf;

public partial class MainWindow : Window
{
    private const double MinZoom = 0.1;
    private const double MaxZoom = 6;
    private const double ZoomStep = 1.2;
    private const int MaxRenderedPages = 24;

    // Space taken around each page by the item template (margins and the page label).
    private const double PageChromeWidth = 48;
    private const double PageChromeHeight = 56;

    private const int ErrorSharingViolation = unchecked((int)0x80070020);
    private const int ErrorLockViolation = unchecked((int)0x80070021);
    private const int ErrorWrongPassword = unchecked((int)0x8007052B);

    private enum Severity { Success, Warning, Error }

    private readonly ObservableCollection<PageItem> _pages = [];
    private readonly List<PageItem> _renderQueue = [];
    private readonly LinkedList<PageItem> _renderedPages = new();
    private readonly DispatcherTimer _rerenderTimer;

    private PdfPageRenderer? _renderer;
    private ScrollViewer? _scrollViewer;
    private string? _path;
    private string? _resultPath;
    private double _zoom = 1;
    private bool _fitToWindow = true;
    private bool _rendering;
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
        PagesList.ItemsSource = _pages;

        // Re-rendering at a new resolution waits until zooming or resizing pauses.
        _rerenderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _rerenderTimer.Tick += (_, _) =>
        {
            _rerenderTimer.Stop();
            QueueRealizedPages();
        };

        SizeChanged += (_, _) =>
        {
            if (_fitToWindow && _pages.Count > 0)
                SetZoom(FitZoom(), fitToWindow: true);
        };

        // The startup check usually ends after the window is shown; a rebuilt window shows its result at once.
        Updater.Checked += Updater_Checked;
        ShowUpdateBar();

        UpdateUi();
    }

    // The list stays collapsed until a document opens, so its template (and scroll viewer) is created late.
    private ScrollViewer? Scroll => _scrollViewer ??= FindScrollViewer();

    private ScrollViewer? FindScrollViewer()
    {
        PagesList.ApplyTemplate();
        return PagesList.Template.FindName("PART_ScrollViewer", PagesList) as ScrollViewer;
    }

    /// <summary>Full path of the open PDF, if any.</summary>
    public string? DocumentPath => _path;

    private PageItem? SelectedPage => _pages.FirstOrDefault(p => p.Selection is not null);

    // ---------------------------------------------------------------- Opening

    public async Task OpenAsync(string path)
    {
        if (_busy)
            return;
        SetBusy(true);
        HideInfo();
        try
        {
            await LoadDocumentAsync(path, keepView: false);
        }
        catch (Exception ex)
        {
            ShowInfo(Severity.Error, Strings.Format("ErrorOpen", Path.GetFileName(path), Describe(ex)));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task LoadDocumentAsync(string path, bool keepView)
    {
        string fullPath = Path.GetFullPath(path);
        var renderer = await PdfPageRenderer.LoadAsync(fullPath);
        double verticalOffset = Scroll?.VerticalOffset ?? 0;
        double horizontalOffset = Scroll?.HorizontalOffset ?? 0;

        CloseDocument();
        _renderer = renderer;
        _path = fullPath;
        Title = Strings.Format("WindowTitleWithFile", Path.GetFileName(fullPath));

        uint count = renderer.PageCount;
        for (uint i = 0; i < count; i++)
        {
            var (width, height) = renderer.GetPageSize(i);
            var page = new PageItem((int)i, (int)count, width, height) { Zoom = _zoom };
            page.PropertyChanged += Page_PropertyChanged;
            _pages.Add(page);
        }

        // The list was collapsed until now: lay it out so that the fit-to-window zoom can be measured.
        UpdateUi();
        UpdateLayout();

        if (keepView && !_fitToWindow)
        {
            Scroll?.UpdateLayout();
            Scroll?.ScrollToVerticalOffset(verticalOffset);
            Scroll?.ScrollToHorizontalOffset(horizontalOffset);
        }
        else
        {
            if (!keepView)
                Scroll?.ScrollToHome();
            SetZoom(FitZoom(), fitToWindow: true);
        }
        UpdateUi();
    }

    protected override void OnClosed(EventArgs e)
    {
        Updater.Checked -= Updater_Checked;
        CloseDocument();
        base.OnClosed(e);
    }

    private void CloseDocument()
    {
        foreach (var page in _pages)
        {
            page.PropertyChanged -= Page_PropertyChanged;
            page.IsRealized = false;
        }
        _pages.Clear();
        _renderQueue.Clear();
        _renderedPages.Clear();
        _renderer?.Dispose();
        _renderer = null;
        _path = null;
        Title = Strings.Get("AppTitle");
    }

    private async Task OpenWithDialogAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.Get("OpenDialogTitle"),
            Filter = Strings.Get("OpenDialogFilter"),
            InitialDirectory = _path is null ? null : Path.GetDirectoryName(_path),
        };
        if (dialog.ShowDialog(this) == true)
            await OpenAsync(dialog.FileName);
    }

    // ---------------------------------------------------------------- Saving

    private async Task SaveAsync(bool overwrite)
    {
        if (_path is null || _busy)
            return;
        if (SelectedPage is not { Selection: { } selection } page)
        {
            ShowInfo(Severity.Warning, Strings.Get("WarningDrawFirst"));
            return;
        }

        string source = _path;
        if (overwrite && _pages.Count > 1 && !ConfirmOverwrite(page, source))
            return;

        string destination = overwrite ? source : OutputPath.GetCropCopyPath(source);
        SetBusy(true);
        try
        {
            await Task.Run(() => PdfCropper.Crop(source, destination, page.Index, selection));

            if (overwrite)
            {
                // Show the result: the open document is now the cropped one.
                await LoadDocumentAsync(source, keepView: true);
                ShowInfo(Severity.Success, Strings.Format("SuccessOverwrite", Path.GetFileName(source), page.Index + 1), source);
            }
            else
            {
                string message = _pages.Count > 1
                    ? Strings.Format("SuccessCopyOnePage", Path.GetFileName(destination), page.Index + 1)
                    : Strings.Format("SuccessCopy", Path.GetFileName(destination));
                ShowInfo(Severity.Success, message, destination);
            }
        }
        catch (Exception ex)
        {
            ShowInfo(Severity.Error, Strings.Format("ErrorSave", Describe(ex)));
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ---------------------------------------------------------------- Printing

    /// <summary>
    /// Opens the print window for the selected area: nothing is saved, the area is printed as it is displayed.
    /// </summary>
    private void OpenPrintWindow()
    {
        if (_renderer is null || _path is null || _busy)
            return;
        if (SelectedPage is not { Selection: { } selection } page)
        {
            ShowInfo(Severity.Warning, Strings.Get("WarningDrawFirst"));
            return;
        }

        var request = new PrintRequest(_renderer, page.Index, selection,
            selection.Width * page.BaseWidth, selection.Height * page.BaseHeight,
            Strings.Format("PrintJobName", Path.GetFileName(_path), page.Index + 1));
        var window = new PrintWindow(request) { Owner = this };
        if (window.ShowDialog() == true && window.PrintedOn is { } printer)
            ShowInfo(Severity.Success, Strings.Format("SuccessPrint", printer));
    }

    private void Print_Click(object sender, RoutedEventArgs e) => OpenPrintWindow();

    // Overwriting a multi-page document deletes the other pages for good: ask first.
    private bool ConfirmOverwrite(PageItem page, string path)
    {
        int others = _pages.Count - 1;
        string message = Strings.Format(others == 1 ? "ConfirmOverwriteOne" : "ConfirmOverwriteMany",
            Path.GetFileName(path), page.Index + 1, others);
        return MessageBox.Show(this, message, Strings.Get("Overwrite"), MessageBoxButton.YesNo,
            MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    private static string Describe(Exception ex) => ex switch
    {
        CropAreaTooSmallException => Strings.Get("ErrorAreaTooSmall"),
        FileNotFoundException => Strings.Get("ErrorFileNotFound"),
        UnauthorizedAccessException => Strings.Get("ErrorAccessDenied"),
        IOException io when io.HResult is ErrorSharingViolation or ErrorLockViolation => Strings.Get("ErrorFileInUse"),
        _ when ex.HResult == ErrorWrongPassword => Strings.Get("ErrorPassword"),
        _ => ex.Message,
    };

    // ---------------------------------------------------------------- Updates

    // Closing the bar hides that version for the rest of the run, also after a language change.
    private static SemanticVersion? s_dismissedUpdate;

    private bool _installingUpdate;

    private void Updater_Checked(object? sender, EventArgs e) => ShowUpdateBar();

    private void ShowUpdateBar()
    {
        if (_installingUpdate)
            return;
        if (Updater.Available is not { } release || release.Version == s_dismissedUpdate)
        {
            UpdateBar.Visibility = Visibility.Collapsed;
            return;
        }
        UpdateText.Text = Strings.Format("UpdateAvailableBar", release.Version);
        UpdateActions.Visibility = Visibility.Visible;
        UpdateCloseButton.Visibility = Visibility.Visible;
        UpdateProgress.Visibility = Visibility.Collapsed;
        UpdateBar.Visibility = Visibility.Visible;
    }

    private async void UpdateInstall_Click(object sender, RoutedEventArgs e)
    {
        if (Updater.Available is not { } release)
            return;

        UpdateActions.Visibility = Visibility.Collapsed;
        UpdateCloseButton.Visibility = Visibility.Collapsed;
        UpdateProgress.Value = 0;
        UpdateProgress.IsIndeterminate = false;
        UpdateProgress.Visibility = Visibility.Visible;
        UpdateText.Text = Strings.Get("UpdateDownloading");
        var progress = new Progress<double>(p =>
        {
            UpdateProgress.Value = p;
            if (p < 1)
                return;
            // Downloaded: the installer now waits for the UAC prompt, then installs.
            UpdateProgress.IsIndeterminate = true;
            UpdateText.Text = Strings.Get("UpdateStarting");
        });

        _installingUpdate = true;
        try
        {
            if (await Updater.InstallAsync(release, progress))
                return; // the app is closing, the installer takes over
            ShowInfo(Severity.Warning, Strings.Get("UpdateCancelled"));
        }
        catch (Exception ex)
        {
            ShowInfo(Severity.Error, Strings.Format("ErrorUpdate", Updater.Describe(ex)));
        }
        _installingUpdate = false;
        ShowUpdateBar();
    }

    private void UpdateNotes_Click(object sender, RoutedEventArgs e)
    {
        if (Updater.Available is { } release)
            Updater.OpenReleasePage(release);
    }

    private void CloseUpdate_Click(object sender, RoutedEventArgs e)
    {
        s_dismissedUpdate = Updater.Available?.Version;
        UpdateBar.Visibility = Visibility.Collapsed;
    }

    // ---------------------------------------------------------------- Selection

    private void Page_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PageItem.Selection) || sender is not PageItem page)
            return;

        // Only one page is cropped: drawing on a page removes the rectangle from the others.
        if (page.Selection is not null)
        {
            foreach (var other in _pages)
            {
                if (!ReferenceEquals(other, page))
                    other.Selection = null;
            }
        }
        UpdateUi();
    }

    private void ClearSelection()
    {
        foreach (var page in _pages)
            page.Selection = null;
    }

    // ---------------------------------------------------------------- Zoom

    private double FitZoom()
    {
        if (_pages.Count == 0 || Scroll is null || Scroll.ActualWidth <= 0)
            return _zoom;

        double width = Scroll.ActualWidth - SystemParameters.VerticalScrollBarWidth - PageChromeWidth;
        double height = Scroll.ActualHeight - PageChromeHeight;
        double zoom = Math.Min(width / _pages.Max(p => p.BaseWidth), height / _pages.Max(p => p.BaseHeight));
        return Math.Clamp(zoom, MinZoom, MaxZoom);
    }

    /// <summary>Changes the zoom, keeping the content under <paramref name="anchor"/> (viewport coordinates) in place.</summary>
    private void SetZoom(double zoom, bool fitToWindow, Point? anchor = null)
    {
        zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        _fitToWindow = fitToWindow;

        if (Scroll is not null && Math.Abs(zoom - _zoom) > 0.0001)
        {
            double ratio = zoom / _zoom;
            var a = anchor ?? new Point(Scroll.ViewportWidth / 2, Scroll.ViewportHeight / 2);
            double horizontal = (Scroll.HorizontalOffset + a.X) * ratio - a.X;
            double vertical = (Scroll.VerticalOffset + a.Y) * ratio - a.Y;
            _zoom = zoom;
            foreach (var page in _pages)
                page.Zoom = zoom;
            Scroll.UpdateLayout();
            Scroll.ScrollToHorizontalOffset(horizontal);
            Scroll.ScrollToVerticalOffset(vertical);
        }
        else
        {
            _zoom = zoom;
            foreach (var page in _pages)
                page.Zoom = zoom;
        }

        ZoomText.Text = $"{Math.Round(_zoom * 100)} %";
        _rerenderTimer.Stop();
        _rerenderTimer.Start();
    }

    private void ZoomBy(double factor, Point? anchor = null)
    {
        if (_pages.Count > 0)
            SetZoom(_zoom * factor, fitToWindow: false, anchor);
    }

    private void PagesList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control || Scroll is null)
            return;
        ZoomBy(e.Delta > 0 ? ZoomStep : 1 / ZoomStep, e.GetPosition(Scroll));
        e.Handled = true;
    }

    // ---------------------------------------------------------------- Rendering

    private void PageContainer_Loaded(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not PageItem page)
            return;
        page.IsRealized = true;
        QueueRender(page);
    }

    private void PageContainer_Unloaded(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is PageItem page)
            page.IsRealized = false;
    }

    private void QueueRealizedPages()
    {
        foreach (var page in _pages)
        {
            if (page.IsRealized)
                QueueRender(page);
        }
    }

    private void QueueRender(PageItem page)
    {
        if (!_renderQueue.Contains(page))
            _renderQueue.Add(page);
        if (!_rendering)
            _ = ProcessRenderQueueAsync();
    }

    // Pages are rendered one at a time, only while they are on screen, at the resolution the zoom needs.
    private async Task ProcessRenderQueueAsync()
    {
        _rendering = true;
        try
        {
            while (_renderQueue.Count > 0)
            {
                var page = _renderQueue[0];
                _renderQueue.RemoveAt(0);

                var renderer = _renderer;
                if (renderer is null || !page.IsRealized)
                    continue;

                int width = TargetPixelWidth(page);
                if (page.Image is not null && page.RenderedWidth >= width * 0.9 && page.RenderedWidth <= width * 1.5)
                    continue;

                try
                {
                    var bitmap = await renderer.RenderAsync((uint)page.Index, (uint)width);
                    if (!ReferenceEquals(renderer, _renderer))
                        continue; // another document was opened meanwhile
                    page.Image = bitmap;
                    page.RenderedWidth = width;
                    _renderedPages.Remove(page);
                    _renderedPages.AddLast(page);
                    EvictOffscreenRenders();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Rendering page {page.Index + 1} failed: {ex}");
                }
            }
        }
        finally
        {
            _rendering = false;
        }
    }

    private int TargetPixelWidth(PageItem page)
    {
        double pixels = page.DisplayWidth * VisualTreeHelper.GetDpi(this).DpiScaleX;
        // Cap the bitmap at about 16 megapixels (64 MB) whatever the zoom.
        double maxPixels = Math.Sqrt(16_000_000 * page.BaseWidth / page.BaseHeight);
        pixels = Math.Min(pixels, maxPixels);
        // Round up to a multiple of 128 so small zoom changes reuse the current bitmap.
        return Math.Max(128, (int)Math.Ceiling(pixels / 128) * 128);
    }

    // Keeps memory bounded on long documents: bitmaps of pages scrolled away are dropped first.
    private void EvictOffscreenRenders()
    {
        var node = _renderedPages.First;
        while (_renderedPages.Count > MaxRenderedPages && node is not null)
        {
            var next = node.Next;
            if (!node.Value.IsRealized)
            {
                node.Value.Image = null;
                node.Value.RenderedWidth = 0;
                _renderedPages.Remove(node);
            }
            node = next;
        }
    }

    // ---------------------------------------------------------------- UI state

    private void SetBusy(bool busy)
    {
        _busy = busy;
        BusyBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        UpdateUi();
    }

    private void UpdateUi()
    {
        bool hasDocument = _renderer is not null;
        var page = SelectedPage;

        EmptyState.Visibility = hasDocument ? Visibility.Collapsed : Visibility.Visible;
        PagesList.Visibility = hasDocument ? Visibility.Visible : Visibility.Collapsed;

        DocumentCommands.Visibility = hasDocument ? Visibility.Visible : Visibility.Collapsed;
        OpenButton.IsEnabled = !_busy;
        SaveCopyButton.IsEnabled = OverwriteButton.IsEnabled = page is not null && !_busy;
        ClearButton.Visibility = page is not null ? Visibility.Visible : Visibility.Collapsed;

        PageCountText.Text = !hasDocument ? ""
            : _pages.Count == 1 ? Strings.Get("PageCountOne")
            : Strings.Format("PageCountMany", _pages.Count);
        StatusText.Text = !hasDocument
            ? ""
            : page is { Selection: { } s }
                ? Strings.Format("StatusSelection", page.Index + 1,
                      Millimeters(s.Width * page.BaseWidth), Millimeters(s.Height * page.BaseHeight))
                  + (_pages.Count > 1 ? Strings.Get("StatusSelectionOnlyPage") : "")
                : Strings.Get("StatusHint");
    }

    private static string Millimeters(double wpfUnits) =>
        (wpfUnits / 96 * 25.4).ToString("0", CultureInfo.CurrentCulture);

    private void ShowInfo(Severity severity, string message, string? resultPath = null)
    {
        (string glyph, string background, string foreground) = severity switch
        {
            Severity.Success => ("\uE930", "SystemFillColorSuccessBackgroundBrush", "SystemFillColorSuccessBrush"),
            Severity.Warning => ("\uE7BA", "SystemFillColorCautionBackgroundBrush", "SystemFillColorCautionBrush"),
            _ => ("\uEA39", "SystemFillColorCriticalBackgroundBrush", "SystemFillColorCriticalBrush"),
        };
        InfoIcon.Text = glyph;
        InfoIcon.SetResourceReference(TextBlock.ForegroundProperty, foreground);
        InfoBar.SetResourceReference(Border.BackgroundProperty, background);
        InfoText.Text = message;
        _resultPath = resultPath;
        InfoActions.Visibility = resultPath is null ? Visibility.Collapsed : Visibility.Visible;
        InfoBar.Visibility = Visibility.Visible;
    }

    private void HideInfo() => InfoBar.Visibility = Visibility.Collapsed;

    // ---------------------------------------------------------------- Input

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers == ModifierKeys.Control;
        switch (e.Key)
        {
            case Key.Escape when SelectedPage is not null:
                ClearSelection();
                break;
            case Key.O when ctrl:
                _ = OpenWithDialogAsync();
                break;
            case Key.OemComma when ctrl:
                OpenSettings();
                break;
            case Key.S when ctrl:
                _ = SaveAsync(overwrite: false);
                break;
            case Key.P when ctrl:
                OpenPrintWindow();
                break;
            case Key.OemPlus or Key.Add when ctrl:
                ZoomBy(ZoomStep);
                break;
            case Key.OemMinus or Key.Subtract when ctrl:
                ZoomBy(1 / ZoomStep);
                break;
            case Key.D0 or Key.NumPad0 when ctrl && _pages.Count > 0:
                SetZoom(FitZoom(), fitToWindow: true);
                break;
            default:
                base.OnPreviewKeyDown(e);
                return;
        }
        e.Handled = true;
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        e.Effects = DroppedPdf(e) is not null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    protected override async void OnDrop(DragEventArgs e)
    {
        e.Handled = true;
        if (DroppedPdf(e) is { } file)
            await OpenAsync(file);
    }

    private static string? DroppedPdf(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] files
            ? files.FirstOrDefault(f => string.Equals(Path.GetExtension(f), ".pdf", StringComparison.OrdinalIgnoreCase))
            : null;

    private async void Open_Click(object sender, RoutedEventArgs e) => await OpenWithDialogAsync();
    private async void SaveCopy_Click(object sender, RoutedEventArgs e) => await SaveAsync(overwrite: false);
    private async void Overwrite_Click(object sender, RoutedEventArgs e) => await SaveAsync(overwrite: true);
    private void ClearSelection_Click(object sender, RoutedEventArgs e) => ClearSelection();
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => ZoomBy(ZoomStep);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => ZoomBy(1 / ZoomStep);
    private void Fit_Click(object sender, RoutedEventArgs e) => SetZoom(FitZoom(), fitToWindow: true);
    private void CloseInfo_Click(object sender, RoutedEventArgs e) => HideInfo();
    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();

    public void OpenSettings()
    {
        var settings = new SettingsWindow { Owner = this };
        settings.ShowDialog();
        if (settings.LanguageChanged)
            ((App)Application.Current).ReloadMainWindow(this);
    }

    private void OpenResult_Click(object sender, RoutedEventArgs e)
    {
        if (_resultPath is not null)
            Process.Start(new ProcessStartInfo(_resultPath) { UseShellExecute = true });
    }

    private void ShowInFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_resultPath is not null)
            Process.Start("explorer.exe", $"/select,\"{_resultPath}\"");
    }
}