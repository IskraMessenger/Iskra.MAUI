using Android.App;
using Android.Content;
using Android.Content.PM;

namespace TorgLink.Maui;

[Activity(Theme = "@style/TorgLink.MainTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int QrScanRequestCode = 0x51A7;
    private const int CreateDocumentRequestCode = 0x51A8;
    private static TaskCompletionSource<string?>? _scanTcs;
    private static TaskCompletionSource<Android.Net.Uri?>? _createDocumentTcs;

    public static Task<string?> TryScanQrWithSystemScannerAsync()
    {
        var activity = Platform.CurrentActivity;
        if (activity == null)
            return Task.FromResult<string?>(null);
        var packageManager = activity.PackageManager;
        if (packageManager == null)
            return Task.FromResult<string?>(null);

        var intent = new Intent("com.google.zxing.client.android.SCAN");
        intent.PutExtra("SCAN_MODE", "QR_CODE_MODE");
        if (intent.ResolveActivity(packageManager) == null)
            return Task.FromResult<string?>(null);

        _scanTcs?.TrySetCanceled();
        _scanTcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        activity.StartActivityForResult(intent, QrScanRequestCode);
        return _scanTcs.Task;
    }

    /// <summary>Системный диалог «Сохранить как» (SAF). Возвращает Uri выбранного файла или null при отмене.</summary>
    public static Task<Android.Net.Uri?> CreateDocumentAsync(string mimeType, string suggestedName)
    {
        var activity = Platform.CurrentActivity;
        if (activity == null)
            return Task.FromResult<Android.Net.Uri?>(null);

        var intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType(mimeType);
        intent.PutExtra(Intent.ExtraTitle, suggestedName);

        _createDocumentTcs?.TrySetCanceled();
        _createDocumentTcs = new TaskCompletionSource<Android.Net.Uri?>(TaskCreationOptions.RunContinuationsAsynchronously);
        activity.StartActivityForResult(intent, CreateDocumentRequestCode);
        return _createDocumentTcs.Task;
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode == QrScanRequestCode)
        {
            var tcs = _scanTcs;
            _scanTcs = null;
            if (tcs != null)
            {
                if (resultCode == Result.Ok)
                    tcs.TrySetResult(data?.GetStringExtra("SCAN_RESULT"));
                else
                    tcs.TrySetResult(null);
            }

            return;
        }

        if (requestCode == CreateDocumentRequestCode)
        {
            var tcs = _createDocumentTcs;
            _createDocumentTcs = null;
            tcs?.TrySetResult(resultCode == Result.Ok ? data?.Data : null);
            return;
        }

        base.OnActivityResult(requestCode, resultCode, data);
    }
}