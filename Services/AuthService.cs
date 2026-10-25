using Firebase.Auth;
using Firebase.Auth.Providers;
using GBV_Shield.Models;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;

namespace GBV_Shield.Services
{
    public class AuthService
    {
        private readonly FirebaseAuthClient _authClient;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        private const string KEY_ID_TOKEN = "firebase_idToken";
        private const string KEY_REFRESH = "firebase_refreshToken";
        private const string KEY_LOCAL_ID = "firebase_localId";
        private const string KEY_PROFILE_JSON = "user_profile_json";

        public AuthService()
        {
            var config = new FirebaseAuthConfig
            {
                ApiKey = Constants.FirebaseApiKey,
                AuthDomain = $"{Constants.FirebaseProjectId}.firebaseapp.com",
                Providers = new FirebaseAuthProvider[] { new EmailProvider() }
            };
            _authClient = new FirebaseAuthClient(config);
        }

        // --- Retry wrapper ---
        private async Task<T?> RetryAsync<T>(Func<Task<T>> action, string operationName)
        {
            int retries = 0;
            int maxRetries = 3;
            int delay = 500;

            while (true)
            {
                try { return await action(); }
                catch (HttpRequestException) when (retries < maxRetries) { retries++; await Task.Delay(delay); delay *= 2; }
                catch (TaskCanceledException) when (retries < maxRetries) { retries++; await Task.Delay(delay); delay *= 2; }
                catch { return default; }
            }
        }

        // 🔐 Register
        public async Task<UserProfile?> RegisterAsync(string fullName, string phone, string email, string password)
        {
            return await RetryAsync(async () =>
            {
                var userCred = await _authClient.CreateUserWithEmailAndPasswordAsync(email, password, fullName);
                var idToken = await userCred.User.GetIdTokenAsync();
                var localId = userCred.User.Uid;

                var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var profile = new UserProfile
                {
                    Uid = localId,
                    Email = email.Trim(),
                    FullName = fullName?.Trim() ?? "",
                    Phone = phone?.Trim() ?? "",
                    CreatedAt = nowMs,
                    UpdatedAt = nowMs
                };

                await PutProfileToDatabaseAsync(profile, idToken);
                await PersistAuthAsync(idToken ?? "", userCred.User.Credential?.RefreshToken ?? "", localId ?? "", profile);
                return profile;
            }, "Register");
        }

        // 🔑 Login
        public async Task<UserProfile?> LoginAsync(string email, string password)
        {
            return await RetryAsync(async () =>
            {
                var userCred = await _authClient.SignInWithEmailAndPasswordAsync(email, password);
                var idToken = await userCred.User.GetIdTokenAsync();
                var localId = userCred.User.Uid;

                var profile = await GetProfileFromDatabaseAsync(localId, idToken) ?? new UserProfile
                {
                    Uid = localId,
                    Email = email.Trim(),
                    FullName = userCred.User.Info.DisplayName ?? "",
                    Phone = "",
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                await PersistAuthAsync(idToken ?? "", userCred.User.Credential?.RefreshToken ?? "", localId ?? "", profile);
                return profile;
            }, "Login");
        }

        // 🚪 Logout
        public async Task LogoutAsync()
        {
            TryRemoveSecure(KEY_ID_TOKEN);
            TryRemoveSecure(KEY_REFRESH);
            TryRemoveSecure(KEY_LOCAL_ID);
            TryRemoveSecure(KEY_PROFILE_JSON);
            await Task.CompletedTask;
        }

        public async Task<UserProfile?> GetCachedProfileAsync()
        {
            var json = await ReadSecureAsync(KEY_PROFILE_JSON);
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonSerializer.Deserialize<UserProfile>(json, JsonOpts); }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[Auth.Cache] Invalid cached profile: {ex.Message}");
                TryRemoveSecure(KEY_PROFILE_JSON);
                return null;
            }
        }

        public async Task<string?> GetIdTokenAsync()
        {
            var token = await ReadSecureAsync(KEY_ID_TOKEN);
            var refresh = await ReadSecureAsync(KEY_REFRESH);
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(refresh)) return null;

            return await RetryAsync(async () =>
            {
                using var client = new HttpClient();
                var url = $"https://securetoken.googleapis.com/v1/token?key={Constants.FirebaseApiKey}";
                var payload = new Dictionary<string, string> { { "grant_type", "refresh_token" }, { "refresh_token", refresh } };

                var resp = await client.PostAsync(url, new FormUrlEncodedContent(payload));
                if (!resp.IsSuccessStatusCode) return token;

                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var newToken = doc.RootElement.GetProperty("id_token").GetString();
                var newRefresh = doc.RootElement.GetProperty("refresh_token").GetString();
                var userId = doc.RootElement.GetProperty("user_id").GetString();

                await SecureStorage.SetAsync(KEY_ID_TOKEN, newToken ?? "");
                await SecureStorage.SetAsync(KEY_REFRESH, newRefresh ?? "");
                if (!string.IsNullOrEmpty(userId)) await SecureStorage.SetAsync(KEY_LOCAL_ID, userId);

                return newToken;
            }, "TokenRefresh");
        }

        public Task<string?> GetLocalIdAsync() => ReadSecureAsync(KEY_LOCAL_ID);
        public async Task<bool> IsLoggedInAsync() => !string.IsNullOrEmpty(await GetIdTokenAsync());

        private static async Task PersistAuthAsync(string idToken, string refreshToken, string localId, UserProfile profile)
        {
            await SecureStorage.SetAsync(KEY_ID_TOKEN, idToken ?? "");
            await SecureStorage.SetAsync(KEY_REFRESH, refreshToken ?? "");
            await SecureStorage.SetAsync(KEY_LOCAL_ID, localId ?? "");
            var json = JsonSerializer.Serialize(profile, JsonOpts);
            await SecureStorage.SetAsync(KEY_PROFILE_JSON, json);
        }

        private static async Task<string?> ReadSecureAsync(string key)
        {
            try { return await SecureStorage.GetAsync(key); }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Auth.Storage] Could not read {key}: {ex.Message}");
                return null;
            }
        }

        private static void TryRemoveSecure(string key)
        {
            try { SecureStorage.Remove(key); }
            catch (Exception ex) { Debug.WriteLine($"[Auth.Storage] Could not remove {key}: {ex.Message}"); }
        }

        private static HttpClient NewClient() => new() { Timeout = TimeSpan.FromSeconds(20) };
        private static string DbUserUrl(string uid) => $"{Constants.FirebaseDatabaseUrl.TrimEnd('/')}/users/{uid}.json";

        private async Task PutProfileToDatabaseAsync(UserProfile profile, string idToken)
        {
            await RetryAsync(async () =>
            {
                using var client = NewClient();
                var url = $"{DbUserUrl(profile.Uid)}?auth={idToken}";
                var json = JsonSerializer.Serialize(profile, JsonOpts);
                var resp = await client.PutAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
                resp.EnsureSuccessStatusCode();
                return true;
            }, "PutProfile");
        }

        private async Task<UserProfile?> GetProfileFromDatabaseAsync(string uid, string idToken)
        {
            return await RetryAsync(async () =>
            {
                using var client = NewClient();
                var url = $"{DbUserUrl(uid)}?auth={idToken}";
                var resp = await client.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return null;

                var json = await resp.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json) || json == "null") return null;

                return JsonSerializer.Deserialize<UserProfile>(json, JsonOpts);
            }, "GetProfile");
        }
    }
}
