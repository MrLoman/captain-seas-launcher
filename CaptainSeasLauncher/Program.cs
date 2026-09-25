using System;
using Avalonia;
using Velopack;

namespace CaptainSeasLauncher;

class Program
{
    // Avalonia configuration, don't remove; also used by visual designer.
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack MOET als allereerste in Main draaien, nog voor Avalonia start.
        // Dit vangt install/uninstall/update-hooks van Velopack af (bv. shortcuts aanmaken,
        // en het toepassen van een gedownloade update bij het opstarten).
        VelopackApp.Build().Run();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace()
            .WithInterFont();
}
