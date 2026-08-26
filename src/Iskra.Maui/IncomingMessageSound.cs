using Iskra.Maui.Services;
using Microsoft.Extensions.Logging;
using ShortP2P.Client.Services;

namespace Iskra.Maui;

internal static class IncomingMessageSound
{
    private const string MessageSoundFile = "GChord.ogg";
    private const string NewChatSoundFile = "new_chat.ogg";

    private static int _hooked;

#if WINDOWS
    private static global::Windows.Media.Playback.MediaPlayer? _windowsPlayer;
#elif ANDROID
    private static Android.Media.MediaPlayer? _androidPlayer;
#endif

    public static void EnsureHooked(ChatRepository repo, ILogger logger)
    {
        if (Interlocked.Exchange(ref _hooked, 1) != 0)
            return;
        repo.ChatMessageAppended += (_, e) =>
        {
            if (e.Outgoing)
                return;
            MainThread.BeginInvokeOnMainThread(() => _ = PlayAsync(MessageSoundFile, logger));
        };
        repo.ChatCreated += (_, e) =>
        {
            if (!e.Remote)
                return;
            MainThread.BeginInvokeOnMainThread(() => _ = PlayAsync(NewChatSoundFile, logger));
        };
    }

    private static async Task PlayAsync(string fileName, ILogger logger)
    {
        try
        {
#if WINDOWS
            await PlayWindowsAsync(fileName).ConfigureAwait(true);
#elif ANDROID
            await PlayAndroidAsync(fileName).ConfigureAwait(true);
#endif
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Notification sound failed ({File})", fileName);
        }
    }

#if WINDOWS
    private static async Task PlayWindowsAsync(string fileName)
    {
        var cache = Path.Combine(FileSystem.CacheDirectory, fileName);
        if (!File.Exists(cache))
        {
            await using var src = await FileSystem.OpenAppPackageFileAsync(fileName).ConfigureAwait(true);
            await using var dst = File.Create(cache);
            await src.CopyToAsync(dst).ConfigureAwait(true);
            AppLog.BinaryLoaded("sound", fileName, dst.Length);
        }

        var file = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(cache);
        _windowsPlayer?.Dispose();
        var player = new global::Windows.Media.Playback.MediaPlayer();
        _windowsPlayer = player;
        player.MediaEnded += (_, _) =>
        {
            try
            {
                player.Dispose();
            }
            catch
            {
                // ignore
            }

            if (ReferenceEquals(_windowsPlayer, player))
                _windowsPlayer = null;
        };
        player.Source = global::Windows.Media.Core.MediaSource.CreateFromStorageFile(file);
        player.Play();
    }
#elif ANDROID
    private static async Task PlayAndroidAsync(string fileName)
    {
        var cache = Path.Combine(FileSystem.CacheDirectory, fileName);
        if (!File.Exists(cache))
        {
            await using var src = await FileSystem.OpenAppPackageFileAsync(fileName).ConfigureAwait(true);
            await using var dst = File.Create(cache);
            await src.CopyToAsync(dst).ConfigureAwait(true);
            AppLog.BinaryLoaded("sound", fileName, dst.Length);
        }

        var path = cache;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            try
            {
                _androidPlayer?.Stop();
                _androidPlayer?.Release();
                _androidPlayer?.Dispose();

                var player = new Android.Media.MediaPlayer();
                _androidPlayer = player;
                var attrs = new Android.Media.AudioAttributes.Builder()
                    .SetUsage(Android.Media.AudioUsageKind.NotificationEvent)
                    .SetContentType(Android.Media.AudioContentType.Sonification)
                    .Build();
                if (attrs != null)
                    player.SetAudioAttributes(attrs);
                player.SetDataSource(path);
                player.Prepare();
                player.Completion += (_, _) =>
                {
                    try
                    {
                        player.Release();
                        player.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }

                    if (ReferenceEquals(_androidPlayer, player))
                        _androidPlayer = null;
                };
                player.Error += (_, _) =>
                {
                    try
                    {
                        player.Release();
                        player.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }

                    if (ReferenceEquals(_androidPlayer, player))
                        _androidPlayer = null;
                };
                player.Start();
            }
            catch
            {
                // Fallback, if MediaPlayer failed (codec/file race etc.)
                using var tone = new Android.Media.ToneGenerator(Android.Media.Stream.Notification, 90);
                tone.StartTone(Android.Media.Tone.PropBeep, 160);
            }
        }).ConfigureAwait(true);
    }
#endif
}
