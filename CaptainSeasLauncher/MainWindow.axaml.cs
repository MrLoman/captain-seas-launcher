using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CaptainSeasLauncher.Services;
using Velopack;

namespace CaptainSeasLauncher;

public partial class MainWindow : Window
{
    private readonly GameUpdateService _gameUpdateService = new();
    private readonly LauncherUpdateService _launcherUpdateService = new();

    private UpdateInfo? _pendingLauncherUpdate;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            // Twee onafhankelijke checks: de game-update mag niet blokkeren op de
            // launcher-update en andersom (los van elkaar, met hun eigen UI).
            await CheckForGameUpdateAsync();
            await CheckForLauncherUpdateAsync();
        };
    }

    // ------------------------------------------------------------------
    //  Game-update (Unity-build) — zie GameUpdateService.cs
    // ------------------------------------------------------------------

    private async Task CheckForGameUpdateAsync()
    {
        try
        {
            SetStatus("Checking for updates...");
            var check = await _gameUpdateService.CheckForGameUpdateAsync();

            if (check.UpdateAvailable)
            {
                SetStatus($"New version found: {check.LatestVersion} — downloading...");
                Progress.IsVisible = true;

                var progress = new Progress<double>(p =>
                    Dispatcher.UIThread.Post(() => Progress.Value = p));

                await _gameUpdateService.DownloadAndInstallAsync(check, progress);

                Progress.IsVisible = false;
                SetStatus($"Ready! Installed version: {check.LatestVersion}");
            }
            else
            {
                SetStatus(check.LocalVersion is null
                    ? "Game not installed yet. Click PLAY to download it."
                    : $"You're playing the latest version ({check.LocalVersion}).");
            }

            PlayButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            SetStatus($"Couldn't check for updates: {ex.Message}");
            // Toch spelen toestaan als er al een lokale installatie staat.
            PlayButton.IsEnabled = _gameUpdateService.HasLocalInstall;
        }
    }

    private async void OnPlayClicked(object? sender, RoutedEventArgs e)
    {
        PlayButton.IsEnabled = false;

        if (!_gameUpdateService.HasLocalInstall)
        {
            await CheckForGameUpdateAsync();
            if (!_gameUpdateService.HasLocalInstall)
            {
                SetStatus("Couldn't download the game.");
                PlayButton.IsEnabled = true;
                return;
            }
        }

        try
        {
            var exePath = _gameUpdateService.GetLaunchExecutablePath();
            Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
            Close();
        }
        catch (Exception ex)
        {
            SetStatus($"Couldn't start the game: {ex.Message}");
            PlayButton.IsEnabled = true;
        }
    }

    // ------------------------------------------------------------------
    //  Launcher-update (de launcher zelf) — zie LauncherUpdateService.cs
    //  Toont alleen een banner + knop, update NOOIT stilzwijgend vanzelf.
    // ------------------------------------------------------------------

    private async Task CheckForLauncherUpdateAsync()
    {
        try
        {
            var info = await _launcherUpdateService.CheckForUpdatesAsync();
            if (info is null) return; // niet geïnstalleerd via Velopack (dev-run), of al up-to-date

            _pendingLauncherUpdate = info;
            LauncherUpdateBanner.IsVisible = true;
        }
        catch
        {
            // Geen launcher-update kunnen ophalen (bv. geen internet, of repo nog niet aangemaakt) —
            // dit mag de rest van de launcher niet blokkeren, dus stil negeren.
        }
    }

    private async void OnUpdateLauncherClicked(object? sender, RoutedEventArgs e)
    {
        if (_pendingLauncherUpdate is null) return;

        UpdateLauncherButton.IsEnabled = false;
        LauncherUpdateText.Text = "Downloading...";

        try
        {
            await _launcherUpdateService.DownloadAsync(_pendingLauncherUpdate, percent =>
                Dispatcher.UIThread.Post(() => LauncherUpdateText.Text = $"Downloading... {percent}%"));

            // Herstart de launcher meteen met de nieuwe versie.
            _launcherUpdateService.ApplyAndRestart(_pendingLauncherUpdate);
        }
        catch (Exception ex)
        {
            LauncherUpdateText.Text = $"Update failed: {ex.Message}";
            UpdateLauncherButton.IsEnabled = true;
        }
    }

    // ------------------------------------------------------------------
    //  Verwijderen (launcher + gedownloade game-bestanden) — zie UninstallService.cs
    // ------------------------------------------------------------------

    private async void OnUninstallClicked(object? sender, RoutedEventArgs e)
    {
        var confirmed = await new ConfirmDialog(
            "Are you sure you want to uninstall Captain of the Seas?\n\n" +
            "This removes both the launcher and the downloaded game files.")
            .ShowDialog<bool>(this);

        if (!confirmed) return;

        try
        {
            UninstallButton.IsEnabled = false;
            SetStatus("Uninstalling...");
            UninstallService.UninstallAndExit();
        }
        catch (Exception ex)
        {
            SetStatus($"Uninstall failed: {ex.Message}");
            UninstallButton.IsEnabled = true;
        }
    }

    private void SetStatus(string text) => StatusText.Text = text;
}
