using TorgLink.Maui.Services;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Client.Services;

namespace TorgLink.Maui;

internal static class IncomingMessageSound
{
    private const string MessageSoundStem = "GChord";
    private const string NewChatSoundStem = "new_chat";

    private static int _hooked;
    private static int _newChatBusy;

#if WINDOWS
    private static global::Windows.Media.Playback.MediaPlayer? _windowsPlayer;
#elif ANDROID
    private static Android.Media.MediaPlayer? _androidPlayer;
#endif

    public static void EnsureHooked(ChatRepository repo, AuthService auth, PeerBlacklist blacklist, ILogger logger)
    {
        if (Interlocked.Exchange(ref _hooked, 1) != 0)
            return;

        repo.ChatMessageAppended += (_, e) =>
        {
            if (e.Outgoing)
                return;
            _ = PlayIncomingIfAllowedAsync(repo, auth, blacklist, e.ChatId, MessageSoundStem, logger, isNewChat: false);
        };

        // new_chat only when another client invited us (new DB row, remote: true).
        repo.ChatCreated += (_, e) =>
        {
            if (!e.Remote)
                return;
            _ = PlayIncomingIfAllowedAsync(repo, auth, blacklist, e.ChatId, NewChatSoundStem, logger, isNewChat: true);
        };
    }

    private static async Task PlayIncomingIfAllowedAsync(
        ChatRepository repo,
        AuthService auth,
        PeerBlacklist blacklist,
        int chatId,
        string stem,
        ILogger logger,
        bool isNewChat)
    {
        try
        {
            var user = auth.CurrentUser;
            if (user != null)
                await blacklist.EnsureLoadedAsync(user.Id).ConfigureAwait(false);
            var chat = await repo.GetChatAsync(chatId).ConfigureAwait(false);
            if (chat != null && blacklist.IsBlocked(user?.Id, chat.PeerNetworkIdShort))
                return;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Blacklist check before notification sound");
        }

        if (isNewChat)
            QueueNewChat(logger);
        else
            QueuePlay(stem, logger);
    }

    private static void QueueNewChat(ILogger logger)
    {
        if (Interlocked.CompareExchange(ref _newChatBusy, 1, 0) != 0)
            return;
        QueuePlay(NewChatSoundStem, logger, releaseNewChat: true);
    }

    private static void QueuePlay(string stem, ILogger logger, bool releaseNewChat = false)
    {
        void Run() => _ = PlayAndReleaseAsync(stem, logger, releaseNewChat);
        if (MainThread.IsMainThread)
            Run();
        else
            MainThread.BeginInvokeOnMainThread(Run);
    }

    private static async Task PlayAndReleaseAsync(string stem, ILogger logger, bool releaseNewChat)
    {
        try
        {
            await PlayAsync(stem, logger).ConfigureAwait(true);
        }
        finally
        {
            if (releaseNewChat)
            {
                await Task.Delay(750).ConfigureAwait(true);
                Interlocked.Exchange(ref _newChatBusy, 0);
            }
        }
    }

    private static async Task PlayAsync(string stem, ILogger logger)
    {
        try
        {
#if WINDOWS
            await PlayWindowsAsync(stem + ".wav").ConfigureAwait(true);
#elif ANDROID
            await PlayAndroidAsync(stem + ".ogg").ConfigureAwait(true);
#else
            await Task.CompletedTask.ConfigureAwait(false);
#endif
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Notification sound failed ({Stem})", stem);
        }
    }

    private static async Task<string> CopyPackageSoundToCacheAsync(string fileName)
    {
        var cache = Path.Combine(FileSystem.CacheDirectory, fileName);
        if (File.Exists(cache) && new FileInfo(cache).Length > 0)
            return cache;

        await using var src = await OpenPackageSoundAsync(fileName).ConfigureAwait(true);
        var tmp = cache + ".tmp";
        await using (var dst = File.Create(tmp))
            await src.CopyToAsync(dst).ConfigureAwait(true);
        File.Copy(tmp, cache, overwrite: true);
        try
        {
            File.Delete(tmp);
        }
        catch
        {
            // ignore
        }

        AppLog.BinaryLoaded("sound", fileName, new FileInfo(cache).Length);
        return cache;
    }

    private static async Task<Stream> OpenPackageSoundAsync(string fileName)
    {
        foreach (var name in new[] { fileName, "Resources/Raw/" + fileName })
        {
            try
            {
                if (await FileSystem.AppPackageFileExistsAsync(name).ConfigureAwait(true))
                    return await FileSystem.OpenAppPackageFileAsync(name).ConfigureAwait(true);
            }
            catch
            {
                // try next
            }

            try
            {
                return await FileSystem.OpenAppPackageFileAsync(name).ConfigureAwait(true);
            }
            catch
            {
                // try next
            }
        }

        throw new FileNotFoundException("Packaged notification sound not found: " + fileName);
    }

#if WINDOWS
    private static async Task PlayWindowsAsync(string fileName)
    {
        var cache = await CopyPackageSoundToCacheAsync(fileName).ConfigureAwait(true);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            try
            {
                _windowsPlayer?.Dispose();
            }
            catch
            {
                // ignore
            }

            var player = new global::Windows.Media.Playback.MediaPlayer
            {
                AudioCategory = global::Windows.Media.Playback.MediaPlayerAudioCategory.SoundEffects,
                Volume = 1
            };
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
            player.MediaFailed += (_, _) =>
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
            player.Source = global::Windows.Media.Core.MediaSource.CreateFromUri(new Uri(cache));
            player.Play();
        }).ConfigureAwait(true);
    }
#elif ANDROID
    private static async Task PlayAndroidAsync(string fileName)
    {
        var path = await CopyPackageSoundToCacheAsync(fileName).ConfigureAwait(true);
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
                    .SetUsage(Android.Media.AudioUsageKind.Media)
                    .SetContentType(Android.Media.AudioContentType.Sonification)
                    .Build();
                if (attrs != null)
                    player.SetAudioAttributes(attrs);
                player.SetVolume(1f, 1f);
                player.SetDataSource(path);
                player.Prepare();
                player.Completion += (_, _) => ReleaseAndroid(player);
                player.Error += (_, _) => ReleaseAndroid(player);
                player.Start();
            }
            catch
            {
                using var tone = new Android.Media.ToneGenerator(Android.Media.Stream.Music, 100);
                tone.StartTone(Android.Media.Tone.PropBeep, 220);
            }
        }).ConfigureAwait(true);
    }

    private static void ReleaseAndroid(Android.Media.MediaPlayer player)
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
    }
#endif
}
