using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace CropPdf;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly bool _initialized;

    public SettingsWindow()
    {
        InitializeComponent();

        LanguageBox.Items.Add(new LanguageOption(AppSettings.SystemLanguage, Strings.Get("LanguageSystem")));
        foreach (string code in AppSettings.SupportedLanguages)
            LanguageBox.Items.Add(new LanguageOption(code, NativeName(code)));
        LanguageBox.SelectedItem = LanguageBox.Items.Cast<LanguageOption>().First(o => o.Code == _settings.Language);

        var version = typeof(SettingsWindow).Assembly.GetName().Version ?? new Version(0, 0, 0);
        VersionText.Text = Strings.Format("AboutVersion", version.ToString(3));
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
}