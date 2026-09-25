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
            SetStatus("Bezig met controleren op updates...");
            var check = await _gameUpdateService.CheckForGameUpdateAsync();

            if (check.UpdateAvailable)
            {
                SetStatus($"Nieuwe versie gevonden: {check.LatestVersion} — downloaden...");
                Progress.IsVisible = true;

                var progress = new Progress<double>(p =>
                    Dispatcher.UIThread.Post(() => Progress.Value = p));

                await _gameUpdateService.DownloadAndInstallAsync(check, progress);

                Progress.IsVisible = false;
                SetStatus($"Klaar! Geïnstalleerde versie: {check.LatestVersion}");
            }
            else
            {
                SetStatus(check.LocalVersion is null
                    ? "Spel nog niet geïnstalleerd. Klik op SPEEL om te downloaden."
                    : $"Je speelt de laatste versie ({check.LocalVersion}).");
            }

            PlayButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            SetStatus($"Kon niet controleren op updates: {ex.Message}");
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
                SetStatus("Kon het spel niet downloaden.");
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
            SetStatus($"Kon het spel niet starten: {ex.Message}");
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
        LauncherUpdateText.Text = "Bezig met downloaden...";

        try
        {
            await _launcherUpdateService.DownloadAsync(_pendingLauncherUpdate, percent =>
                Dispatcher.UIThread.Post(() => LauncherUpdateText.Text = $"Downloaden... {percent}%"));

            // Herstart de launcher meteen met de nieuwe versie.
            _launcherUpdateService.ApplyAndRestart(_pendingLauncherUpdate);
        }
        catch (Exception ex)
        {
            LauncherUpdateText.Text = $"Update mislukt: {ex.Message}";
            UpdateLauncherButton.IsEnabled = true;
        }
    }

    // ------------------------------------------------------------------
    //  Verwijderen (launcher + gedownloade game-bestanden) — zie UninstallService.cs
    // ------------------------------------------------------------------

    private async void OnUninstallClicked(object? sender, RoutedEventArgs e)
    {
        var confirmed = await new ConfirmDialog(
            "Weet je zeker dat je Captain of the Seas wilt verwijderen?\n\n" +
            "Dit verwijdert zowel de launcher als de gedownloade game-bestanden.")
            .ShowDialog<bool>(this);

        if (!confirmed) return;

        try
        {
            UninstallButton.IsEnabled = false;
            SetStatus("Bezig met verwijderen...");
            UninstallService.UninstallAndExit();
        }
        catch (Exception ex)
        {
            SetStatus($"Verwijderen mislukt: {ex.Message}");
            UninstallButton.IsEnabled = true;
        }
    }

    private void SetStatus(string text) => StatusText.Text = text;
}
