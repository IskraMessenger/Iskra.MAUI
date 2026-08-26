using Android.App;
using Android.Runtime;
using Android.Util;

namespace Iskra.Maui;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    public override void OnCreate()
    {
        Log.Info("Iskra", "MainApplication.OnCreate");
        AndroidEnvironment.UnhandledExceptionRaiser += (_, args) =>
        {
            Log.Error("Iskra", "UnhandledExceptionRaiser: " + args.Exception);
            try
            {
                NLog.LogManager.GetLogger("GlobalExceptions")
                    .Error(args.Exception, "Android UnhandledExceptionRaiser");
                NLog.LogManager.Flush();
            }
            catch
            {
                // NLog may not be ready yet
            }
        };
        try
        {
            base.OnCreate();
        }
        catch (Exception ex)
        {
            Log.Error("Iskra", "OnCreate failed: " + ex);
            throw;
        }
    }

    protected override Microsoft.Maui.Hosting.MauiApp CreateMauiApp()
    {
        Log.Info("Iskra", "CreateMauiApp");
        try
        {
            return MauiProgram.CreateMauiApp();
        }
        catch (Exception ex)
        {
            Log.Error("Iskra", "CreateMauiApp failed: " + ex);
            throw;
        }
    }
}
