using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;

namespace TorgLink.Maui.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
    public App()
    {
        // Required for classic tray toasts before any window is created.
        try
        {
            SetCurrentProcessExplicitAppUserModelID("com.torglink.maui");
        }
        catch
        {
            // ignore — toast layer will retry
        }

        this.InitializeComponent();
        UnhandledException += (_, e) =>
        {
            NLog.LogManager.GetLogger("GlobalExceptions").Error(e.Exception, "WinUI UnhandledException");
            NLog.LogManager.Flush();
        };
    }

    protected override global::Microsoft.Maui.Hosting.MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);
}