using Iskra.Maui.Services;
using Microsoft.Extensions.Logging;
using ShortP2P.Client.Routing;

namespace Iskra.Maui;

public partial class App : Application
{
    private int _permissionsBootstrapped;
    private int _deferredStart;

    public App()
    {
        InitializeComponent();
        ThemeService.LoadAndApply();
        UiInteractionLog.HookApplication(this);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var login = MauiProgram.Services.GetRequiredService<LoginPage>();
        var logger = MauiProgram.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Application window created");
        if (Interlocked.Exchange(ref _permissionsBootstrapped, 1) == 0)
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await AppPermissionsBootstrapper.EnsureRequestedAsync(logger).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Permission bootstrap failed");
                }
            });
        if (Interlocked.Exchange(ref _deferredStart, 1) == 0)
            MainThread.BeginInvokeOnMainThread(() => _ = ApplyBluetoothSettingsAsync(logger));
        return new Window(new NavigationPage(login));
    }

    private static async Task ApplyBluetoothSettingsAsync(ILogger logger)
    {
        try
        {
            var store = MauiProgram.Services.GetRequiredService<P2pRoutingSettingsStore>();
            var routing = await store.LoadAsync().ConfigureAwait(false);
            await Task.Run(() =>
                MauiProgram.Services.GetRequiredService<MauiBluetoothTransportRegistration>().ApplySettings(routing)
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Deferred Bluetooth settings apply failed");
        }
    }
}
