using System.Security.Cryptography;
using System.Text.Json;

namespace CropPdf.Core;

/// <summary>Which releases are offered: final versions only, or pre-releases (betas) too.</summary>
public enum UpdateChannel
{
    Stable,
    Beta,
}

/// <summary>A published version and its installer, as listed by GitHub Releases.</summary>
/// <param name="InstallerSha256">Lowercase hex SHA-256 published by GitHub for the installer, if any.</param>
public sealed record ReleaseInfo(
    SemanticVersion Version,
    bool IsPreRelease,
    Uri PageUrl,
    Uri InstallerUrl,
    string InstallerName,
    long InstallerSize,
    string? InstallerSha256);

/// <summary>The downloaded installer does not match the checksum published with the release.</summary>
public sealed class UpdateIntegrityException()
    : IOException("The downloaded installer does not match the checksum published with the release.");

/// <summary>
/// Updates come from the GitHub Releases of the repository: every release published by the CI workflow has a
/// "v&lt;version&gt;" tag and a CropPdf-Setup-&lt;version&gt;.exe asset; betas are marked as pre-releases.
/// </summary>
public static class UpdateFeed
{
    public const string Repository = "quentinperou/CropPdf";

    // The 30 most recent releases (newest first) are plenty to find the latest stable and beta versions.
    public static readonly Uri ReleasesApiUrl = new($"https://api.github.com/repos/{Repository}/releases?per_page=30");

    private const string InstallerPrefix = "CropPdf-Setup-";

    public static async Task<IReadOnlyList<ReleaseInfo>> FetchAsync(HttpClient http, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ReleasesApiUrl);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    /// <summary>Reads the GitHub "list releases" JSON; drafts and releases without an installer are skipped.</summary>
    public static IReadOnlyList<ReleaseInfo> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var releases = new List<ReleaseInfo>();
        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (GetBool(release, "draft")
                || !SemanticVersion.TryParse(GetString(release, "tag_name"), out var version)
                || !Uri.TryCreate(GetString(release, "html_url"), UriKind.Absolute, out var page)
                || page.Scheme != Uri.UriSchemeHttps // opened in the browser by "What's new"
                || !release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var asset in assets.EnumerateArray())
            {
                if (GetString(asset, "name") is not { } name || !IsInstallerName(name)
                    || !Uri.TryCreate(GetString(asset, "browser_download_url"), UriKind.Absolute, out var url)
                    || url.Scheme != Uri.UriSchemeHttps)
                    continue;

                long size = asset.TryGetProperty("size", out var sizeValue) && sizeValue.TryGetInt64(out long n) ? n : 0;
                string? sha256 = GetString(asset, "digest") is { } digest && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                    ? digest["sha256:".Length..].ToLowerInvariant()
                    : null;
                bool preRelease = GetBool(release, "prerelease") || version.IsPreRelease;
                releases.Add(new ReleaseInfo(version, preRelease, page, url, name, size, sha256));
                break;
            }
        }
        return releases;
    }

    // The name becomes a file name in the temp folder: no path, and only the installers of this app.
    private static bool IsInstallerName(string name) =>
        name.StartsWith(InstallerPrefix, StringComparison.OrdinalIgnoreCase)
        && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
        && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    /// <summary>
    /// The newest release of <paramref name="channel"/> that is newer than <paramref name="current"/>, if any.
    /// Never a downgrade: a beta user who goes back to Stable keeps the beta until a newer stable version.
    /// </summary>
    public static ReleaseInfo? FindUpdate(IEnumerable<ReleaseInfo> releases, SemanticVersion current, UpdateChannel channel) =>
        releases
            .Where(r => channel == UpdateChannel.Beta || !r.IsPreRelease)
            .Where(r => r.Version > current)
            .MaxBy(r => r.Version);

    /// <summary>Downloads the installer of <paramref name="release"/> to <paramref name="path"/>; progress goes from 0 to 1.</summary>
    public static async Task DownloadAsync(HttpClient http, ReleaseInfo release, string path,
        IProgress<double>? progress, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(release.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        long total = response.Content.Headers.ContentLength ?? release.InstallerSize;

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        long received = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            received += read;
            if (total > 0)
                progress?.Report(Math.Min(1, (double)received / total));
        }
        progress?.Report(1);
    }

    /// <summary>
    /// Checks the installer against the SHA-256 published by GitHub (catches truncated or altered downloads).
    /// Releases published before GitHub added checksums have none: HTTPS is then the only guarantee.
    /// </summary>
    public static void VerifyInstaller(Stream installer, ReleaseInfo release)
    {
        if (release.InstallerSha256 is not { } expected)
            return;
        string actual = Convert.ToHexStringLower(SHA256.HashData(installer));
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new UpdateIntegrityException();
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool GetBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
