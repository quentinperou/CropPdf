using System.Globalization;
using System.Windows;
using System.Windows.Threading;

namespace CropPdf;

public partial class App : Application
{
    // Windows display language, kept to come back to it when the user picks "System language".
    private static readonly CultureInfo SystemUICulture = CultureInfo.CurrentUICulture;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // UI language: the one chosen in the settings, else the Windows one (French, otherwise English).
        // CROPPDF_LANG=en|fr forces one, handy to test both.
        ApplyLanguage(Environment.GetEnvironmentVariable("CROPPDF_LANG") is { Length: > 0 } forced
            ? forced
            : AppSettings.Load().Language);

        // The theme follows Windows (light/dark). CROPPDF_THEME=Light|Dark forces one, handy to test both.
        if (Environment.GetEnvironmentVariable("CROPPDF_THEME") is "Light" or "Dark")
            ThemeMode = new ThemeMode(Environment.GetEnvironmentVariable("CROPPDF_THEME")!);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, Strings.Get("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        var window = new CropPdf.MainWindow();
        MainWindow = window;
        window.Show();

        // Started from Explorer, the window may come up minimized or behind Explorer: bring it forward.
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();

        // Explorer's context menu passes the PDF path as the first argument.
        string? file = e.Args.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(file))
            _ = window.OpenAsync(file);
    }

    /// <summary>Sets the UI language: <see cref="AppSettings.SystemLanguage"/> or a culture name ("en", "fr").</summary>
    public static void ApplyLanguage(string language)
    {
        var culture = language == AppSettings.SystemLanguage ? SystemUICulture : CultureInfo.GetCultureInfo(language);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>
    /// Texts are read once, when a window is built: to switch language, the main window is replaced by a new
    /// one at the same place, showing the same document, with the settings page open again.
    /// </summary>
    public void ReloadMainWindow(MainWindow old)
    {
        ApplyLanguage(AppSettings.Load().Language);

        var bounds = old.WindowState == WindowState.Normal ? new Rect(old.Left, old.Top, old.Width, old.Height) : old.RestoreBounds;
        var window = new CropPdf.MainWindow
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            WindowState = old.WindowState == WindowState.Maximized ? WindowState.Maximized : WindowState.Normal,
        };
        string? document = old.DocumentPath;

        MainWindow = window;
        window.Show();
        old.Close();

        if (document is not null)
            _ = window.OpenAsync(document);
        window.Dispatcher.BeginInvoke(window.OpenSettings, DispatcherPriority.ApplicationIdle);
    }
}