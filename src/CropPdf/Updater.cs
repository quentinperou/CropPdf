using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using CropPdf.Core;

namespace CropPdf;

/// <summary>
/// Finds and installs new versions from GitHub Releases. The installer is downloaded to the temp folder,
/// checked against the SHA-256 published by GitHub, then run silently. It asks for administrator rights
/// itself (it starts unelevated, so the UAC answer is not known here), closes this app through the Restart
/// Manager when it replaces the files, and starts the new version when done.
/// </summary>
public static class Updater
{
    private const int ErrorCancelled = 1223; // UAC prompt declined, should the installer ever require elevation

    // No wizard pages, only the progress window. The app has no unsaved state, so force-closing it is safe if
    // it does not close in time. /RELAUNCH=1 asks installer/CropPdf.iss to start the app again.
    private const string InstallerArguments =
        "/SP- /SILENT /NORESTART /CLOSEAPPLICATIONS /FORCECLOSEAPPLICATIONS /RELAUNCH=1";

    private static readonly string DownloadDirectory = Path.Combine(Path.GetTempPath(), "CropPdf-Update");

    /// <summary>Version of the running app, without the "+commit" suffix: "1.2.0", "1.3.0-beta.1"...</summary>
    public static SemanticVersion CurrentVersion { get; } = ReadCurrentVersion();

    // After CurrentVersion: the User-Agent uses it.
    private static readonly HttpClient Http = CreateHttpClient();

    /// <summary>Newer version found by the last successful check, if any.</summary>
    public static ReleaseInfo? Available { get; private set; }

    /// <summary>True once a check has succeeded during this run.</summary>
    public static bool HasChecked { get; private set; }

    /// <summary>Raised after each successful check, on the thread that started it (the UI thread).</summary>
    public static event EventHandler? Checked;

    public static async Task<ReleaseInfo?> CheckAsync(UpdateChannel channel)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var releases = await UpdateFeed.FetchAsync(Http, timeout.Token);
        Available = UpdateFeed.FindUpdate(releases, CurrentVersion, channel);
        HasChecked = true;
        Checked?.Invoke(null, EventArgs.Empty);
        return Available;
    }

    /// <summary>Background check when the app starts: a failure (offline, rate limit) only means no offer.</summary>
    public static async Task CheckAtStartupAsync()
    {
        DeleteOldDownloads();
        var settings = AppSettings.Load();
        if (!settings.CheckForUpdatesAtStartup)
            return;
        try
        {
            await CheckAsync(settings.UpdateChannel);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            Debug.WriteLine($"Update check failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Downloads and runs the installer of <paramref name="release"/>, which normally closes the app on its way.
    /// Returns false if the installer ended without installing (UAC prompt declined, error): the app keeps running.
    /// </summary>
    public static async Task<bool> InstallAsync(ReleaseInfo release, IProgress<double>? progress)
    {
        Directory.CreateDirectory(DownloadDirectory);
        string path = Path.Combine(DownloadDirectory, release.InstallerName);
        using (var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10)))
            await UpdateFeed.DownloadAsync(Http, release, path, progress, timeout.Token);

        // The file stays open (readable, not writable by others) until the installer has started (a running
        // executable cannot be changed), so the file that gets administrator rights is the one that was checked.
        Process? installer;
        using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            UpdateFeed.VerifyInstaller(file, release);
            try
            {
                installer = Process.Start(new ProcessStartInfo(path, InstallerArguments) { UseShellExecute = true });
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
            {
                return false;
            }
        }
        if (installer is null)
            return false;

        using (installer)
        {
            await installer.WaitForExitAsync();
            if (installer.ExitCode != 0)
                return false;
        }

        // Installed without closing this instance (it runs from another folder, e.g. a dev build): the new
        // version has been started by the installer, make room for it.
        Application.Current.Shutdown();
        return true;
    }

    public static void OpenReleasePage(ReleaseInfo release) =>
        Process.Start(new ProcessStartInfo(release.PageUrl.AbsoluteUri) { UseShellExecute = true });

    public static string Describe(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests } =>
            Strings.Get("ErrorUpdateRateLimited"),
        HttpRequestException { StatusCode: null } or OperationCanceledException => Strings.Get("ErrorUpdateOffline"),
        UpdateIntegrityException => Strings.Get("ErrorUpdateIntegrity"),
        _ => ex.Message,
    };

    // The installer of the previous update stays in the temp folder (it cannot delete itself while running).
    private static void DeleteOldDownloads()
    {
        try
        {
            if (Directory.Exists(DownloadDirectory))
                Directory.Delete(DownloadDirectory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static SemanticVersion ReadCurrentVersion()
    {
        string? version = typeof(Updater).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return SemanticVersion.TryParse(version, out var parsed) ? parsed : SemanticVersion.Parse("0.0.0");
    }

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient();
        // GitHub's API rejects requests without a User-Agent.
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"CropPdf/{CurrentVersion}");
        return http;
    }
}
