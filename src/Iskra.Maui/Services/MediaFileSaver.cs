namespace Iskra.Maui.Services;

/// <summary>
/// Сохранение медиафайла через системный диалог:
/// WinUI — FileSavePicker, Android — Storage Access Framework (ACTION_CREATE_DOCUMENT).
/// Возвращает true, если пользователь выбрал файл и запись удалась.
/// </summary>
public static class MediaFileSaver
{
    public static async Task<bool> SaveAsync(string sourcePath, string suggestedName)
    {
#if WINDOWS
        return await SaveWindowsAsync(sourcePath, suggestedName).ConfigureAwait(false);
#elif ANDROID
        return await SaveAndroidAsync(sourcePath, suggestedName).ConfigureAwait(false);
#else
        await Task.CompletedTask;
        return false;
#endif
    }

    private static string ExtensionOf(string name)
    {
        var ext = Path.GetExtension(name);
        return string.IsNullOrWhiteSpace(ext) ? ".bin" : ext.ToLowerInvariant();
    }

    private static bool IsVideoExt(string ext) =>
        ext is ".mp4" or ".mov" or ".mkv" or ".webm" or ".avi" or ".3gp" or ".m4v";

    private static bool IsImageExt(string ext) =>
        ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" or ".heic";

#if WINDOWS
    private static async Task<bool> SaveWindowsAsync(string sourcePath, string suggestedName)
    {
        var window = Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
        if (window == null)
            return false;

        var ext = ExtensionOf(suggestedName);
        var picker = new Windows.Storage.Pickers.FileSavePicker
        {
            SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedName),
            DefaultFileExtension = ext,
            SuggestedStartLocation = IsVideoExt(ext)
                ? Windows.Storage.Pickers.PickerLocationId.VideosLibrary
                : Windows.Storage.Pickers.PickerLocationId.PicturesLibrary
        };
        var kind = IsVideoExt(ext) ? "Video" : IsImageExt(ext) ? "Image" : "File";
        picker.FileTypeChoices.Add(kind, new List<string> { ext });

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var target = await picker.PickSaveFileAsync();
        if (target == null)
            return false;

        var bytes = await File.ReadAllBytesAsync(sourcePath);
        await Windows.Storage.FileIO.WriteBytesAsync(target, bytes);
        return true;
    }
#endif

#if ANDROID
    private static async Task<bool> SaveAndroidAsync(string sourcePath, string suggestedName)
    {
        var ext = ExtensionOf(suggestedName);
        var uri = await MainActivity.CreateDocumentAsync(MimeTypeOf(ext), suggestedName);
        if (uri == null)
            return false;

        var resolver = Platform.CurrentActivity?.ContentResolver;
        if (resolver == null)
            return false;

        var bytes = await File.ReadAllBytesAsync(sourcePath);
        await using var stream = resolver.OpenOutputStream(uri);
        if (stream == null)
            return false;
        await stream.WriteAsync(bytes);
        return true;
    }

    private static string MimeTypeOf(string ext) => ext switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".mp4" => "video/mp4",
        ".mov" => "video/quicktime",
        ".mkv" => "video/x-matroska",
        ".webm" => "video/webm",
        ".avi" => "video/x-msvideo",
        ".3gp" => "video/3gpp",
        ".m4v" => "video/x-m4v",
        _ => "application/octet-stream"
    };
#endif
}
