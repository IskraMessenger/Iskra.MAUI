using ShortP2P.Auth;

namespace TorgLink.Maui.Services;

/// <summary>Maps MAUI secure storage to <see cref="ISessionStorage" />.</summary>
public sealed class MauiSecureStorage : ISessionStorage
{
    public void Remove(string key)
    {
        SecureStorage.Default.Remove(key);
    }

    public Task<string?> GetAsync(string key)
    {
        return SecureStorage.Default.GetAsync(key);
    }

    public Task SetAsync(string key, string value)
    {
        // Windows SecureStorage rejects null/empty (DataProtectionProvider). Optional
        // routing fields (Bluetooth adapter id/mac) are often empty — treat as delete.
        if (string.IsNullOrEmpty(value))
        {
            SecureStorage.Default.Remove(key);
            return Task.CompletedTask;
        }

        return SecureStorage.Default.SetAsync(key, value);
    }
}
