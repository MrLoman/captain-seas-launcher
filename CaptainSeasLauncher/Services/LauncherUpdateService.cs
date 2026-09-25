using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace CaptainSeasLauncher.Services;

/// <summary>
/// Update-kanaal voor de LAUNCHER zelf — los van <see cref="GameUpdateService"/>, dat het
/// SPEL (de Unity-build) update. Wijst naar een eigen GitHub-repo waar je met
/// "vpk upload github" nieuwe launcher-versies naartoe publiceert (zie README.md).
///
/// Update NOOIT stilzwijgend op de achtergrond: MainWindow toont alleen een banner +
/// "UPDATE LAUNCHER"-knop zodra CheckForUpdatesAsync() iets vindt, en download/herstart pas
/// na een klik van de speler.
/// </summary>
public sealed class LauncherUpdateService
{
    // TODO: zet hier de eigen GitHub-repo van de LAUNCHER (NIET dezelfde repo als de
    // game-builds — zie README.md, deel "Launcher-updates (los van de game)").
    // Voorbeeld: "https://github.com/jouwgebruikersnaam/captain-seas-launcher"
    private const string RepoUrl = "https://github.com/MrLoman/captain-seas-launcher";

    private readonly UpdateManager _manager = new(new GithubSource(RepoUrl, null, false));

    /// <summary>
    /// False zodra de launcher niet via de installer/vpk draait (bv. gestart met "dotnet run"
    /// tijdens development) — dan heeft zelf-updaten geen zin, dus slaan we de check dan over.
    /// </summary>
    public bool IsInstalled => _manager.IsInstalled;

    public string? CurrentVersion => _manager.CurrentVersion?.ToString();

    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        if (!_manager.IsInstalled) return null;
        return await _manager.CheckForUpdatesAsync();
    }

    public Task DownloadAsync(UpdateInfo info, Action<int>? progress = null) =>
        _manager.DownloadUpdatesAsync(info, progress);

    /// <summary>Sluit de launcher af, past de gedownloade update toe, en herstart 'm meteen.</summary>
    public void ApplyAndRestart(UpdateInfo info) => _manager.ApplyUpdatesAndRestart(info);
}
