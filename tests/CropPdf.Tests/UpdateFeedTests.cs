using System.Net;
using System.Security.Cryptography;
using CropPdf.Core;

namespace CropPdf.Tests;

public class SemanticVersionTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.1")]
    [InlineData("1.0.9", "1.1.0")]
    [InlineData("1.9.0", "1.10.0")]
    [InlineData("1.10.0", "2.0.0")]
    [InlineData("1.1.0-beta", "1.1.0-beta.1")]
    [InlineData("1.1.0-beta.1", "1.1.0-beta.2")]
    [InlineData("1.1.0-beta.2", "1.1.0-beta.10")]
    [InlineData("1.1.0-beta.10", "1.1.0-rc.1")]
    [InlineData("1.1.0-rc.1", "1.1.0")]
    [InlineData("1.0.0", "1.1.0-beta.1")]
    [InlineData("1.1.0-1", "1.1.0-alpha")]
    public void Versions_AreOrdered(string lower, string higher)
    {
        Assert.True(SemanticVersion.Parse(lower) < SemanticVersion.Parse(higher));
        Assert.True(SemanticVersion.Parse(higher) > SemanticVersion.Parse(lower));
    }

    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3+8f4e2b1", "1.2.3")]
    [InlineData("V1.3.0-beta.2+abc", "1.3.0-beta.2")]
    public void PrefixAndBuildMetadata_AreIgnored(string text, string expected)
    {
        Assert.Equal(SemanticVersion.Parse(expected), SemanticVersion.Parse(text));
        Assert.Equal(expected, SemanticVersion.Parse(text).ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("1.2.x")]
    [InlineData("1.2.3-")]
    [InlineData("1.2.3-beta..1")]
    [InlineData("latest")]
    public void InvalidText_IsRejected(string text)
    {
        Assert.False(SemanticVersion.TryParse(text, out _));
    }

    [Fact]
    public void PreRelease_IsDetected()
    {
        Assert.True(SemanticVersion.Parse("1.3.0-beta.1").IsPreRelease);
        Assert.Equal("beta.1", SemanticVersion.Parse("1.3.0-beta.1").PreRelease);
        Assert.False(SemanticVersion.Parse("1.3.0").IsPreRelease);
    }
}

public class UpdateFeedTests
{
    private static readonly SemanticVersion V100 = SemanticVersion.Parse("1.0.0");

    // Trimmed copy of the GitHub "list releases" response, newest first.
    private const string ReleasesJson = """
        [
          {
            "tag_name": "v1.2.0-beta.1", "draft": false, "prerelease": true,
            "html_url": "https://github.com/quentinperou/CropPdf/releases/tag/v1.2.0-beta.1",
            "assets": [ { "name": "CropPdf-Setup-1.2.0-beta.1.exe", "size": 6500000, "digest": "sha256:AB12",
                          "browser_download_url": "https://github.com/quentinperou/CropPdf/releases/download/v1.2.0-beta.1/CropPdf-Setup-1.2.0-beta.1.exe" } ]
          },
          {
            "tag_name": "v1.3.0", "draft": true, "prerelease": false,
            "html_url": "https://github.com/quentinperou/CropPdf/releases/tag/untagged-1",
            "assets": [ { "name": "CropPdf-Setup-1.3.0.exe", "size": 1,
                          "browser_download_url": "https://github.com/quentinperou/CropPdf/releases/download/untagged-1/CropPdf-Setup-1.3.0.exe" } ]
          },
          {
            "tag_name": "v1.1.1", "draft": false, "prerelease": false,
            "html_url": "https://github.com/quentinperou/CropPdf/releases/tag/v1.1.1",
            "assets": [ { "name": "notes.txt", "size": 10,
                          "browser_download_url": "https://github.com/quentinperou/CropPdf/releases/download/v1.1.1/notes.txt" } ]
          },
          {
            "tag_name": "v1.1.0", "draft": false, "prerelease": false,
            "html_url": "https://github.com/quentinperou/CropPdf/releases/tag/v1.1.0",
            "assets": [ { "name": "CropPdf-Setup-1.1.0.exe", "size": 6400000, "digest": null,
                          "browser_download_url": "https://github.com/quentinperou/CropPdf/releases/download/v1.1.0/CropPdf-Setup-1.1.0.exe" } ]
          },
          {
            "tag_name": "v1.0.0", "draft": false, "prerelease": false,
            "html_url": "https://github.com/quentinperou/CropPdf/releases/tag/v1.0.0",
            "assets": [ { "name": "CropPdf-Setup-1.0.0.exe", "size": 6427955,
                          "digest": "sha256:6653e4a2e43f752b57fed3b09482157144a74b0ced309692e190cc37e3a5e02a",
                          "browser_download_url": "https://github.com/quentinperou/CropPdf/releases/download/v1.0.0/CropPdf-Setup-1.0.0.exe" } ]
          }
        ]
        """;

    private static IReadOnlyList<ReleaseInfo> Releases => UpdateFeed.Parse(ReleasesJson);

    [Fact]
    public void Parse_SkipsDraftsAndReleasesWithoutInstaller()
    {
        Assert.Equal(["1.2.0-beta.1", "1.1.0", "1.0.0"], Releases.Select(r => r.Version.ToString()));
    }

    [Fact]
    public void Parse_ReadsTheInstallerAndItsChecksum()
    {
        var release = Releases.Single(r => r.Version == V100);
        Assert.False(release.IsPreRelease);
        Assert.Equal("CropPdf-Setup-1.0.0.exe", release.InstallerName);
        Assert.Equal(6427955, release.InstallerSize);
        Assert.Equal("6653e4a2e43f752b57fed3b09482157144a74b0ced309692e190cc37e3a5e02a", release.InstallerSha256);
        Assert.Equal("https://github.com/quentinperou/CropPdf/releases/tag/v1.0.0", release.PageUrl.ToString());

        Assert.Equal("ab12", Releases[0].InstallerSha256);
        Assert.True(Releases[0].IsPreRelease);
        Assert.Null(Releases.Single(r => r.Version.ToString() == "1.1.0").InstallerSha256);
    }

    [Fact]
    public void Parse_RejectsInstallerNamesWithAPath()
    {
        const string json = """
            [ { "tag_name": "v9.0.0", "html_url": "https://github.com/x/y/releases/tag/v9.0.0",
                "assets": [ { "name": "CropPdf-Setup-..\\..\\evil.exe",
                              "browser_download_url": "https://github.com/x/y/releases/download/v9.0.0/a.exe" } ] } ]
            """;
        Assert.Empty(UpdateFeed.Parse(json));
    }

    [Fact]
    public void Stable_IgnoresBetas()
    {
        Assert.Equal("1.1.0", UpdateFeed.FindUpdate(Releases, V100, UpdateChannel.Stable)?.Version.ToString());
    }

    [Fact]
    public void Beta_OffersTheNewestVersion_BetaOrNot()
    {
        Assert.Equal("1.2.0-beta.1", UpdateFeed.FindUpdate(Releases, V100, UpdateChannel.Beta)?.Version.ToString());
        Assert.Equal("1.1.0", UpdateFeed.FindUpdate(Releases.Skip(1), V100, UpdateChannel.Beta)?.Version.ToString());
    }

    [Fact]
    public void BackToStable_KeepsTheBetaUntilANewerStableVersion()
    {
        var beta = SemanticVersion.Parse("1.2.0-beta.1");
        Assert.Null(UpdateFeed.FindUpdate(Releases, beta, UpdateChannel.Stable));
        Assert.Null(UpdateFeed.FindUpdate(Releases, beta, UpdateChannel.Beta));
    }

    [Fact]
    public void UpToDate_GivesNoUpdate()
    {
        Assert.Null(UpdateFeed.FindUpdate(Releases, SemanticVersion.Parse("1.1.0"), UpdateChannel.Stable));
    }

    [Fact]
    public void VerifyInstaller_ChecksThePublishedChecksum()
    {
        byte[] content = "installer"u8.ToArray();
        var release = Releases[0] with { InstallerSha256 = Convert.ToHexStringLower(SHA256.HashData(content)) };

        UpdateFeed.VerifyInstaller(new MemoryStream(content), release);
        Assert.Throws<UpdateIntegrityException>(() => UpdateFeed.VerifyInstaller(new MemoryStream("altered"u8.ToArray()), release));
        UpdateFeed.VerifyInstaller(new MemoryStream(content), release with { InstallerSha256 = null });
    }

    [Fact]
    public async Task Download_WritesTheInstallerAndReportsProgress()
    {
        byte[] content = new byte[200_000];
        Random.Shared.NextBytes(content);
        using var http = new HttpClient(new StaticHandler(content));
        string path = Path.Combine(Path.GetTempPath(), $"croppdf-download-{Guid.NewGuid():N}.exe");
        var reported = new List<double>();
        try
        {
            await UpdateFeed.DownloadAsync(http, Releases[0], path, new SyncProgress(reported.Add), CancellationToken.None);

            Assert.Equal(content, File.ReadAllBytes(path));
            Assert.Equal(1, reported[^1]);
            Assert.Equal(reported.Order(), reported);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class StaticHandler(byte[] content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
    }

    // Progress<T> posts to the thread pool: reports could arrive after the download returns.
    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
