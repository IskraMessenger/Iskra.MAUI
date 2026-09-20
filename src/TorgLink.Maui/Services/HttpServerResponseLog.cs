using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace TorgLink.Maui.Services;

/// <summary>
/// Logs HTTPS messenger-server responses via DiagnosticListener.
/// Skips message send and delivery-receipt endpoints.
/// </summary>
internal static class HttpServerResponseLog
{
    private static int _hooked;

    private static readonly string[] SkipPathFragments =
    [
        "/api/v1/messages/receipts",
        "/api/v1/messages"
    ];

    public static void Hook()
    {
        if (Interlocked.Exchange(ref _hooked, 1) == 1)
            return;

        DiagnosticListener.AllListeners.Subscribe(new ListenerObserver());
    }

    private static bool ShouldSkip(Uri? uri)
    {
        if (uri == null)
            return true;
        var path = uri.AbsolutePath;
        foreach (var skip in SkipPathFragments)
        {
            if (path.Contains(skip, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private sealed class ListenerObserver : IObserver<DiagnosticListener>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(DiagnosticListener listener)
        {
            if (listener.Name is "HttpHandlerDiagnosticListener" or "System.Net.Http")
                listener.Subscribe(new HttpObserver());
        }
    }

    private sealed class HttpObserver : IObserver<KeyValuePair<string, object?>>
    {
        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Value == null)
                return;
            if (!value.Key.Contains("Stop", StringComparison.Ordinal) &&
                value.Key != "System.Net.Http.Response")
                return;

            try
            {
                LogPayload(value.Value);
            }
            catch (Exception ex)
            {
                AppLog.Server.LogDebug(ex, "HTTP response log failed");
            }
        }

        private static void LogPayload(object payload)
        {
            var type = payload.GetType();
            var request = GetMember<HttpRequestMessage>(type, payload, "Request");
            var response = GetMember<HttpResponseMessage>(type, payload, "Response");
            if (request == null && response == null)
                return;

            var uri = request?.RequestUri ?? response?.RequestMessage?.RequestUri;
            if (ShouldSkip(uri))
                return;

            var method = request?.Method.Method ?? response?.RequestMessage?.Method.Method ?? "?";
            var status = response != null ? ((int)response.StatusCode).ToString() : "?";
            var length = response?.Content.Headers.ContentLength;
            AppLog.ServerResponse(
                $"{method} {status}",
                uri?.GetLeftPart(UriPartial.Path),
                length is > 0 ? $"bytes={length}" : "");
        }

        private static T? GetMember<T>(Type type, object instance, string name) where T : class
        {
            var prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop?.GetValue(instance) is T fromProp)
                return fromProp;
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field?.GetValue(instance) as T;
        }
    }
}
