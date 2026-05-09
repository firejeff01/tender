using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace Tender.Desktop.Services;

public sealed class GitHubUpdateChecker : IUpdateChecker
{
    // 對應使用者建立的 GitHub repo
    private const string Owner = "firejeff01";
    private const string Repo = "tender";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<UpdateInfo> CheckAsync(CancellationToken ct = default)
    {
        var current = GetCurrentVersion();

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.Add("User-Agent", "TenderSearch-UpdateChecker");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");

            var url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
            using var resp = await client.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                // 404 = 私 repo 或沒有 release；不丟例外
                return new UpdateInfo(current, null, false, null, null, null);
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            var release = JsonSerializer.Deserialize<GitHubRelease>(json, JsonOptions);
            if (release == null || string.IsNullOrEmpty(release.TagName))
                return new UpdateInfo(current, null, false, null, null, null);

            var latest = ParseVersion(release.TagName);
            if (latest == null)
                return new UpdateInfo(current, null, false, null, null, release.HtmlUrl);

            // 找 .msi asset
            var msiAsset = release.Assets?.FirstOrDefault(a =>
                a.Name?.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) == true);

            return new UpdateInfo(
                CurrentVersion: current,
                LatestVersion: latest,
                IsUpdateAvailable: latest > current,
                DownloadUrl: msiAsset?.BrowserDownloadUrl,
                ReleaseNotes: release.Body,
                ReleasePageUrl: release.HtmlUrl);
        }
        catch (Exception)
        {
            // 網路、超時、JSON 解析失敗等都靜默失敗
            return new UpdateInfo(current, null, false, null, null, null);
        }
    }

    private static Version GetCurrentVersion()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            return asm.GetName().Version ?? new Version(0, 0, 0, 0);
        }
        catch
        {
            return new Version(0, 0, 0, 0);
        }
    }

    /// <summary>把 GitHub tag（如 "v1.1.509.1711" 或 "1.1.509.1711"）解析成 Version。</summary>
    private static Version? ParseVersion(string tag)
    {
        var s = tag.TrimStart('v', 'V').Trim();
        return Version.TryParse(s, out var v) ? v : null;
    }

    private sealed record GitHubRelease
    {
        public string? TagName { get; init; }
        public string? Name { get; init; }
        public string? Body { get; init; }
        public string? HtmlUrl { get; init; }
        public List<GitHubAsset>? Assets { get; init; }
    }

    private sealed record GitHubAsset
    {
        public string? Name { get; init; }
        public string? BrowserDownloadUrl { get; init; }
    }
}
