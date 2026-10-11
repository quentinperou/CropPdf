using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using CropPdf.Core;

namespace CropPdf;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly bool _initialized;
    private bool _updating; // an update check or download is running

    public SettingsWindow()
    {
        InitializeComponent();

        LanguageBox.Items.Add(new LanguageOption(AppSettings.SystemLanguage, Strings.Get("LanguageSystem")));
        foreach (string code in AppSettings.SupportedLanguages)
            LanguageBox.Items.Add(new LanguageOption(code, NativeName(code)));
        LanguageBox.SelectedItem = LanguageBox.Items.Cast<LanguageOption>().First(o => o.Code == _settings.Language);

        ThemeBox.Items.Add(new ThemeOption(AppTheme.System, Strings.Get("ThemeSystem")));
        ThemeBox.Items.Add(new ThemeOption(AppTheme.Light, Strings.Get("ThemeLight")));
        ThemeBox.Items.Add(new ThemeOption(AppTheme.Dark, Strings.Get("ThemeDark")));
        ThemeBox.SelectedItem = ThemeBox.Items.Cast<ThemeOption>().First(o => o.Theme == _settings.Theme);

        ChannelBox.Items.Add(new ChannelOption(UpdateChannel.Stable, Strings.Get("UpdateChannelStable")));
        ChannelBox.Items.Add(new ChannelOption(UpdateChannel.Beta, Strings.Get("UpdateChannelBeta")));
        ChannelBox.SelectedItem = ChannelBox.Items.Cast<ChannelOption>().First(o => o.Channel == _settings.UpdateChannel);
        CheckAtStartupBox.IsChecked = _settings.CheckForUpdatesAtStartup;
        ShowUpdateState();
        Updater.Checked += Updater_Checked;
        Closed += (_, _) => Updater.Checked -= Updater_Checked;

        VersionText.Text = Strings.Format("AboutVersion", Updater.CurrentVersion);
        _initialized = true;
    }

    /// <summary>True when the user picked another language: the main window must be rebuilt.</summary>
    public bool LanguageChanged { get; private set; }

    // Languages are listed in their own language ("English", "Français"), whatever the current UI language.
    private static string NativeName(string code)
    {
        var culture = CultureInfo.GetCultureInfo(code);
        return culture.TextInfo.ToUpper(culture.NativeName[0]) + culture.NativeName[1..];
    }

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || LanguageBox.SelectedItem is not LanguageOption option || option.Code == _settings.Language)
            return;

        _settings.Language = option.Code;
        _settings.Save();
        LanguageChanged = true;
        // The main window reloads in the new language and reopens this page.
        Close();
    }

    // Unlike the language, the theme switches live: no window needs to be rebuilt.
    private void ThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || ThemeBox.SelectedItem is not ThemeOption option || option.Theme == _settings.Theme)
            return;

        _settings.Theme = option.Theme;
        _settings.Save();
        App.ApplyTheme(option.Theme);
    }

    // ---------------------------------------------------------------- Updates

    private void Updater_Checked(object? sender, EventArgs e) => ShowUpdateState();

    /// <summary>Shows what the last check found, or <paramref name="status"/> (progress, error) instead.</summary>
    private void ShowUpdateState(string? status = null)
    {
        var available = Updater.Available;
        UpdateStatusText.Text = status
            ?? (available is not null ? Strings.Format("UpdateStatusAvailable", Updater.CurrentVersion, available.Version)
                : Updater.HasChecked ? Strings.Format("UpdateStatusUpToDate", Updater.CurrentVersion)
                : Strings.Format("UpdateStatusIdle", Updater.CurrentVersion));
        UpdateNotesLink.Visibility = available is not null && status is null ? Visibility.Visible : Visibility.Collapsed;

        UpdateButton.Content = available is null ? Strings.Get("UpdateCheck") : Strings.Get("UpdateInstall");
        if (available is null)
            UpdateButton.ClearValue(StyleProperty);
        else
            UpdateButton.SetResourceReference(StyleProperty, "AccentButtonStyle");
        UpdateButton.IsEnabled = !_updating;
    }

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (Updater.Available is { } release)
            await InstallUpdateAsync(release);
        else
            await CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        _updating = true;
        ShowUpdateState(Strings.Get("UpdateStatusChecking"));
        string? error = null;
        try
        {
            await Updater.CheckAsync(_settings.UpdateChannel);
        }
        catch (Exception ex)
        {
            error = Strings.Format("UpdateStatusFailed", Updater.CurrentVersion, Updater.Describe(ex));
        }
        _updating = false;
        ShowUpdateState(error);
    }

    private async Task InstallUpdateAsync(ReleaseInfo release)
    {
        _updating = true;
        ShowUpdateState(Strings.Get("UpdateDownloading"));
        UpdateProgress.Value = 0;
        UpdateProgress.IsIndeterminate = false;
        UpdateProgress.Visibility = Visibility.Visible;
        var progress = new Progress<double>(p =>
        {
            UpdateProgress.Value = p;
            if (p < 1)
                return;
            // Downloaded: the installer now waits for the UAC prompt, then installs.
            UpdateProgress.IsIndeterminate = true;
            UpdateStatusText.Text = Strings.Get("UpdateStarting");
        });

        string status;
        try
        {
            if (await Updater.InstallAsync(release, progress))
                return; // the app is closing, the installer takes over
            status = Strings.Get("UpdateCancelled");
        }
        catch (Exception ex)
        {
            status = Strings.Format("ErrorUpdate", Updater.Describe(ex));
        }
        _updating = false;
        UpdateProgress.Visibility = Visibility.Collapsed;
        ShowUpdateState(status);
    }

    private void UpdateNotes_Click(object sender, RoutedEventArgs e)
    {
        if (Updater.Available is { } release)
            Updater.OpenReleasePage(release);
    }

    private void CheckAtStartup_Click(object sender, RoutedEventArgs e)
    {
        _settings.CheckForUpdatesAtStartup = CheckAtStartupBox.IsChecked == true;
        _settings.Save();
    }

    private async void ChannelBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || ChannelBox.SelectedItem is not ChannelOption option || option.Channel == _settings.UpdateChannel)
            return;

        _settings.UpdateChannel = option.Channel;
        _settings.Save();
        // What is offered depends on the channel: look again.
        if (!_updating)
            await CheckForUpdatesAsync();
    }

    // ---------------------------------------------------------------- Other

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private sealed record LanguageOption(string Code, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record ChannelOption(UpdateChannel Channel, string Name)
    {
        public override string ToString() => Name;
    }

    private sealed record ThemeOption(AppTheme Theme, string Name)
    {
        public override string ToString() => Name;
    }
}
