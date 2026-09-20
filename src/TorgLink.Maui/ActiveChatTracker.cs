namespace TorgLink.Maui;

/// <summary>Tracks which chat detail is currently visible (suppress duplicate toasts).</summary>
internal static class ActiveChatTracker
{
    private static int _chatId;

    public static void Set(int chatId) => Interlocked.Exchange(ref _chatId, chatId);

    public static void Clear(int chatId) => Interlocked.CompareExchange(ref _chatId, 0, chatId);

    public static bool IsViewing(int chatId) => Volatile.Read(ref _chatId) == chatId;
}