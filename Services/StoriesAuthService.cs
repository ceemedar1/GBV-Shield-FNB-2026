using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GBV_Shield.Config;
using Microsoft.Maui.Storage;

namespace GBV_Shield.Services;

// Separate, anonymous identity in the Stories project. It never replaces the app's
// original Firebase login, which SOS and live location still depend on.
public sealed class StoriesAuthService(AuthService appAuth)
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<(string Uid, string Token)> GetIdentityAsync(CancellationToken cancellationToken = default)
    {
        var appUid = await appAuth.GetLocalIdAsync();
        var appToken = await appAuth.GetIdTokenAsync();
        if (string.IsNullOrWhiteSpace(appUid) || string.IsNullOrWhiteSpace(appToken))
            throw new InvalidOperationException("Please sign in to use Community Stories.");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            // Keep separate Stories identities for separate app users on a shared phone.
            var uidKey = $"stories_uid_{appUid}";
            var refreshKey = $"stories_refresh_{appUid}";
            var uid = await ReadAsync(uidKey);
            var refreshToken = await ReadAsync(refreshKey);
            if (!string.IsNullOrWhiteSpace(uid) && !string.IsNullOrWhiteSpace(refreshToken))
            {
                using var refreshBody = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken
                });
                using var refreshed = await _http.PostAsync(
                    $"https://securetoken.googleapis.com/v1/token?key={AppConfig.FirebaseAiApiKey}",
                    refreshBody, cancellationToken);
                if (refreshed.IsSuccessStatusCode)
                {
                    using var data = JsonDocument.Parse(await refreshed.Content.ReadAsStringAsync(cancellationToken));
                    var root = data.RootElement;
                    var newUid = root.GetProperty("user_id").GetString();
                    var token = root.GetProperty("id_token").GetString();
                    var newRefresh = root.GetProperty("refresh_token").GetString();
                    if (!string.IsNullOrWhiteSpace(newUid) && !string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(newRefresh))
                    {
                        await SecureStorage.SetAsync(uidKey, newUid);
                        await SecureStorage.SetAsync(refreshKey, newRefresh);
                        return (newUid, token);
                    }
                }
                // A revoked or corrupt Stories credential can be replaced without
                // touching the user's main sign-in session.
                Remove(uidKey);
                Remove(refreshKey);
            }

            using var body = new StringContent("{\"returnSecureToken\":true}", Encoding.UTF8, "application/json");
            using var created = await _http.PostAsync(
                $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={AppConfig.FirebaseAiApiKey}",
                body, cancellationToken);
            if (!created.IsSuccessStatusCode)
                throw new InvalidOperationException("Stories sign-in is not available yet. Please try again later.");
            using var identity = JsonDocument.Parse(await created.Content.ReadAsStringAsync(cancellationToken));
            var result = identity.RootElement;
            uid = result.GetProperty("localId").GetString();
            var idToken = result.GetProperty("idToken").GetString();
            refreshToken = result.GetProperty("refreshToken").GetString();
            if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(idToken) || string.IsNullOrWhiteSpace(refreshToken))
                throw new InvalidOperationException("Stories sign-in returned an incomplete response.");
            await SecureStorage.SetAsync(uidKey, uid);
            await SecureStorage.SetAsync(refreshKey, refreshToken);
            return (uid, idToken);
        }
        finally { _gate.Release(); }
    }

    private static async Task<string?> ReadAsync(string key)
    {
        try { return await SecureStorage.GetAsync(key); }
        catch { return null; }
    }

    private static void Remove(string key)
    {
        try { SecureStorage.Remove(key); }
        catch { }
    }
}
