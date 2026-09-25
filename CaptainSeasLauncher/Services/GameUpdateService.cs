using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CaptainSeasLauncher.Services;

/// <summary>
/// Haalt de laatste Unity-build van "Captain of the Seas" op via de GitHub Releases API
/// en zet 'm klaar naast de launcher. Dit is BEWUST losse logica van Velopack: Velopack
/// (zie Program.cs, VelopackApp.Build().Run()) update de LAUNCHER zelf, deze service
/// update het SPEL (de Unity-build) — dat is een los stuk software met een eigen
/// release-repo en release-tempo. Zie README.md in de Launcher-map voor de volledige uitleg.
/// </summary>
public sealed class GameUpdateService
{
    // TODO: zet hier de eigenaar en repo-naam van de GitHub-repo waar de Unity-builds
    // als Release-assets in staan (zie README.md voor hoe je die repo opzet).
    private const string GitHubOwner = "MrLoman";
    private const string GitHubRepo = "captain-seas-builds";

    // TODO: pas dit aan naar hoe de asset-bestanden in je Release heten.
    private const string WindowsAssetName = "CaptainSeas-Win64.zip";
    private const string MacAssetName = "CaptainSeas-macOS.zip";

    // TODO: de naam van het uitvoerbare bestand zodra de zip is uitgepakt.
    // Windows: de .exe naam uit Unity Build Settings (Product Name + ".exe").
    // macOS: de naam van de .app-bundle.
    private const string WindowsExeName = "CaptainSeas.exe";
    private const string MacAppName = "CaptainSeas.app";

    private static readonly string GameDir =
        Path.Combine(AppContext.BaseDirectory, "GameData");
    private static readonly string VersionFile =
        Path.Combine(GameDir, "version.txt");

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "CaptainSeasLauncher");
        return client;
    }

    public bool HasLocalInstall => File.Exists(VersionFile);

    public string? GetLocalVersion() =>
        HasLocalInstall ? File.ReadAllText(VersionFile).Trim() : null;

    public async Task<GameUpdateCheck> CheckForGameUpdateAsync()
    {
        var release = await Http.GetFromJsonAsync<GitHubRelease>(
            $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest");

        if (release is null)
            throw new InvalidOperationException("Kon geen release-informatie ophalen van GitHub.");

        var localVersion = GetLocalVersion();
        var updateAvailable = localVersion != release.TagName;

        return new GameUpdateCheck(release, localVersion, updateAvailable);
    }

    public async Task DownloadAndInstallAsync(GameUpdateCheck check, IProgress<double>? progress = null)
    {
        var assetName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? WindowsAssetName
            : MacAssetName;

        var asset = Array.Find(check.Release.Assets, a => a.Name == assetName);
        if (asset is null)
        {
            throw new InvalidOperationException(
                $"Geen asset genaamd '{assetName}' gevonden in release {check.Release.TagName}. " +
                "Controleer de bestandsnamen in GameUpdateService.cs en in je GitHub Release.");
        }

        Directory.CreateDirectory(GameDir);
        var zipPath = Path.Combine(Path.GetTempPath(), assetName);

        await using (var httpStream = await Http.GetStreamAsync(asset.BrowserDownloadUrl))
        await using (var fileStream = File.Create(zipPath))
        {
            await CopyWithProgressAsync(httpStream, fileStream, asset.Size, progress);
        }

        // Oude installatie weg (behalve version.txt, die zetten we hierna opnieuw), dan verse build uitpakken.
        if (Directory.Exists(GameDir))
        {
            foreach (var entry in Directory.GetFileSystemEntries(GameDir))
            {
                if (string.Equals(entry, VersionFile, StringComparison.OrdinalIgnoreCase)) continue;
                if (Directory.Exists(entry)) Directory.Delete(entry, true);
                else File.Delete(entry);
            }
        }

        ZipFile.ExtractToDirectory(zipPath, GameDir, overwriteFiles: true);
        File.Delete(zipPath);
        File.WriteAllText(VersionFile, check.Release.TagName);
    }

    public string GetLaunchExecutablePath()
    {
        var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? WindowsExeName
            : MacAppName;

        var path = Path.Combine(GameDir, exeName);
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new FileNotFoundException(
                $"Kan '{exeName}' niet vinden in {GameDir}. " +
                "Controleer of de zip-structuur overeenkomt met WindowsExeName/MacAppName in GameUpdateService.cs.");
        }

        return path;
    }

    private static async Task CopyWithProgressAsync(
        Stream source, Stream destination, long totalBytes, IProgress<double>? progress)
    {
        var buffer = new byte[81920];
        long totalRead = 0;
        int read;
        while ((read = await source.ReadAsync(buffer)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read));
            totalRead += read;
            if (totalBytes > 0)
                progress?.Report(totalRead * 100.0 / totalBytes);
        }
    }
}

public sealed record GameUpdateCheck(GitHubRelease Release, string? LocalVersion, bool UpdateAvailable)
{
    public string LatestVersion => Release.TagName;
}

public sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = "";

    [JsonPropertyName("assets")]
    public GitHubAsset[] Assets { get; set; } = Array.Empty<GitHubAsset>();
}

public sealed class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
