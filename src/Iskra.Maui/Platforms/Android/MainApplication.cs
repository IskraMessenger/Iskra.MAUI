using Android.App;
using Android.Runtime;

namespace Iskra.Maui;

[Application]
public class MainApplication(IntPtr handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
{
    public override void OnCreate()
    {
        base.OnCreate();
        AndroidEnvironment.UnhandledExceptionRaiser += (_, args) =>
        {
            NLog.LogManager.GetLogger("GlobalExceptions")
                .Error(args.Exception, "Android UnhandledExceptionRaiser");
            NLog.LogManager.Flush();
        };
    }

    protected override Microsoft.Maui.Hosting.MauiApp CreateMauiApp()
    {
        return MauiProgram.CreateMauiApp();
    }
}