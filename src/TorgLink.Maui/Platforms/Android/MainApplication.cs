using Android.App;
using Android.Runtime;
using Android.Util;

namespace TorgLink.Maui;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    public override void OnCreate()
    {
        Log.Info("TorgLink", "MainApplication.OnCreate");
        AndroidEnvironment.UnhandledExceptionRaiser += (_, args) =>
        {
            Log.Error("TorgLink", "UnhandledExceptionRaiser: " + args.Exception);
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
            Log.Error("TorgLink", "OnCreate failed: " + ex);
            throw;
        }
    }

    protected override Microsoft.Maui.Hosting.MauiApp CreateMauiApp()
    {
        Log.Info("TorgLink", "CreateMauiApp");
        try
        {
            return MauiProgram.CreateMauiApp();
        }
        catch (Exception ex)
        {
            Log.Error("TorgLink", "CreateMauiApp failed: " + ex);
            throw;
        }
    }
}
