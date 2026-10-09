using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CropPdf.Core;

namespace CropPdf;

/// <summary>User preferences, saved in %LOCALAPPDATA%\CropPdf\settings.json.</summary>
public sealed class AppSettings
{
    /// <summary>Follow the Windows display language.</summary>
    public const string SystemLanguage = "system";

    /// <summary>UI languages with a translation (Properties/Strings*.resx). English is the default.</summary>
    public static readonly string[] SupportedLanguages = ["en", "fr"];

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CropPdf", "settings.json");

    /// <summary><see cref="SystemLanguage"/> or one of <see cref="SupportedLanguages"/>.</summary>
    public string Language { get; set; } = SystemLanguage;

    /// <summary>Print mode used last, preselected in the print window.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<PrintScaling>))]
    public PrintScaling PrintScaling { get; set; } = PrintScaling.FitToPaper;

    /// <summary>Printer used last (full queue name), preselected in the print window.</summary>
    public string? PrintPrinter { get; set; }

    /// <summary>Paper used last on each printer (<see cref="PaperSize.Id"/>): a label printer keeps its label.</summary>
    public Dictionary<string, string> PrintPapers { get; set; } = [];

    /// <summary>Ask GitHub for a newer version when the app starts.</summary>
    public bool CheckForUpdatesAtStartup { get; set; } = true;

    /// <summary>Releases offered as updates: stable only, or betas too.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<UpdateChannel>))]
    public UpdateChannel UpdateChannel { get; set; } = UpdateChannel.Stable;

    // A missing or damaged file just means default settings: preferences are never worth an error message.
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath) && JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) is { } settings)
            {
                if (!SupportedLanguages.Contains(settings.Language))
                    settings.Language = SystemLanguage;
                return settings;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}