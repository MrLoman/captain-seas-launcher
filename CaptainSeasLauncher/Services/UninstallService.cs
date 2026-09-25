using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace CaptainSeasLauncher.Services;

/// <summary>
/// "Verwijderen"-functie vanuit de launcher zelf (een knop in de UI, geen Windows
/// Configuratiescherm nodig). Verwijdert eerst de gedownloade game-bestanden
/// (<see cref="GameUpdateService"/>'s GameData-map), en de-installeert daarna de launcher
/// zelf op een manier die per platform verschilt:
///
///   Windows: roept "Update.exe -s uninstall" aan — dat is Velopack's eigen de-installatie
///            (dezelfde die ook via "Programma's en onderdelen" gebruikt wordt), en ruimt
///            snelkoppelingen, bestanden en registervermeldingen netjes op.
///
///   macOS:   de launcher wordt hier bewust als portable .app gepubliceerd (--noInst, zie
///            README.md — de macOS .pkg-installer van Velopack loopt op dit moment vast op
///            nieuwere macOS-versies). Een portable .app kent geen systeem-uninstaller, dus
///            "verwijderen" betekent hier: de .app-bundle naar de Prullenbak verplaatsen.
/// </summary>
public static class UninstallService
{
    public static void UninstallAndExit()
    {
        TryDeleteGameData();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            UninstallWindows();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            UninstallMac();
        }
        else
        {
            throw new PlatformNotSupportedException("Verwijderen wordt alleen ondersteund op Windows en macOS.");
        }
    }

    private static void TryDeleteGameData()
    {
        try
        {
            var gameDir = Path.Combine(AppContext.BaseDirectory, "GameData");
            if (Directory.Exists(gameDir))
                Directory.Delete(gameDir, recursive: true);
        }
        catch
        {
            // Niet fataal — ga gewoon door met het de-installeren van de launcher zelf.
        }
    }

    private static void UninstallWindows()
    {
        // Velopack-conventie: Update.exe staat één map boven de huidige, versie-specifieke
        // app-map (dezelfde map waar ook de snelkoppelingen naar wijzen).
        var updateExe = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Update.exe"));

        if (!File.Exists(updateExe))
        {
            throw new FileNotFoundException(
                "Kan Update.exe niet vinden. Draait de launcher wel als geïnstalleerde versie " +
                "(via de Setup.exe), en niet via 'dotnet run'?", updateExe);
        }

        Process.Start(new ProcessStartInfo(updateExe, "-s uninstall")
        {
            UseShellExecute = true,
        });

        Environment.Exit(0);
    }

    private static void UninstallMac()
    {
        // .app-bundle-structuur: .../CaptainSeasLauncher.app/Contents/MacOS/CaptainSeasLauncher
        var exeDir = AppContext.BaseDirectory; // .../Contents/MacOS/
        var appBundle = Path.GetFullPath(Path.Combine(exeDir, "..", "..", ".."));

        if (!appBundle.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Kon de .app-bundle niet vinden. Draait de launcher wel vanuit een geïnstalleerde " +
                ".app, en niet via 'dotnet run'?");
        }

        var script = $"tell application \"Finder\" to move POSIX file \"{appBundle}\" to trash";
        Process.Start(new ProcessStartInfo("osascript", $"-e '{script}'")
        {
            UseShellExecute = true,
        });

        Environment.Exit(0);
    }
}
