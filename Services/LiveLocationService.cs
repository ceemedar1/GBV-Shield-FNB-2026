using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;
using GBV_Shield.Config;

namespace GBV_Shield.Services;

public sealed class LiveLocationService
{
    private const string SessionTokenKey = "LiveLocationSessionToken";
    private const string SessionCreatedKey = "LiveLocationSessionCreatedAt";
    private const string SessionExpiresKey = "LiveLocationSessionExpiresAt";
    private const string SessionWriteTokenKey = "LiveLocationSessionWriteToken";
    private const string SessionViewerUrlKey = "LiveLocationSessionViewerUrl";
    private static readonly TimeSpan SessionDuration = TimeSpan.FromHours(8);
    // The currently deployed Firebase rules cap each remote expiry at eight hours
    // from the write. Renew a shorter lease on every location update so a
    // eight-hour session remains compatible with the existing rules.
    private static readonly TimeSpan FirebaseRemoteLease = TimeSpan.FromHours(7);
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(10);

    private readonly FirebaseService _firebase;
    private readonly AuthService _auth;
    private readonly AzureLiveLocationClient _azure;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly object _trackingLock = new();
    private CancellationTokenSource? _trackingCts;
    private Task? _trackingTask;

    public LiveLocationService(FirebaseService firebase, AuthService auth, AzureLiveLocationClient azure)
    {
        _firebase = firebase;
        _auth = auth;
        _azure = azure;
    }

    public bool IsActive
    {
        get
        {
            try
            {
                return !string.IsNullOrWhiteSpace(Preferences.Default.Get(SessionTokenKey, string.Empty)) &&
                       Preferences.Default.Get(SessionExpiresKey, 0L) > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LiveLocation] Could not read session state: {ex.Message}");
                return false;
            }
        }
    }

    public async Task<string?> StartOrRefreshSessionAsync(Location? initialLocation = null)
    {
        await _stateLock.WaitAsync();
        try
        {
            var uid = await _auth.GetLocalIdAsync();
            var idToken = await _auth.GetIdTokenAsync();
            if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(idToken))
                return null;

            var now = DateTimeOffset.UtcNow;
            var sessionToken = Preferences.Default.Get(SessionTokenKey, string.Empty);
            var writeToken = Preferences.Default.Get(SessionWriteTokenKey, string.Empty);
            var expiresAt = Preferences.Default.Get(SessionExpiresKey, 0L);
            var createdAt = Preferences.Default.Get(SessionCreatedKey, 0L);

            if (UseAzure && (string.IsNullOrWhiteSpace(sessionToken) ||
                             string.IsNullOrWhiteSpace(writeToken) ||
                             expiresAt <= now.ToUnixTimeMilliseconds()))
            {
                var azureSession = await _azure.CreateSessionAsync(idToken);
                if (azureSession is null) return null;
                sessionToken = azureSession.ViewerToken;
                writeToken = azureSession.WriteToken;
                createdAt = now.ToUnixTimeMilliseconds();
                expiresAt = azureSession.ExpiresAt.ToUnixTimeMilliseconds();
                Preferences.Default.Set(SessionTokenKey, sessionToken);
                Preferences.Default.Set(SessionWriteTokenKey, writeToken);
                Preferences.Default.Set(SessionViewerUrlKey, azureSession.ViewerUrl);
                Preferences.Default.Set(SessionCreatedKey, createdAt);
                Preferences.Default.Set(SessionExpiresKey, expiresAt);
                if (initialLocation != null)
                    await _azure.PublishAsync(sessionToken, writeToken, initialLocation);
                StartTrackingLoop(sessionToken, uid, createdAt, expiresAt);
                EnsureAndroidTrackingService();
                return azureSession.ViewerUrl;
            }

            if (UseAzure)
            {
                if (initialLocation != null)
                    await _azure.PublishAsync(sessionToken, writeToken, initialLocation);
                StartTrackingLoop(sessionToken, uid, createdAt, expiresAt);
                EnsureAndroidTrackingService();
                return Preferences.Default.Get(SessionViewerUrlKey, string.Empty);
            }

            if (string.IsNullOrWhiteSpace(sessionToken) || expiresAt <= now.ToUnixTimeMilliseconds())
            {
                sessionToken = CreateSessionToken();
                createdAt = now.ToUnixTimeMilliseconds();
            }

            expiresAt = now.Add(SessionDuration).ToUnixTimeMilliseconds();
            Preferences.Default.Set(SessionTokenKey, sessionToken);
            Preferences.Default.Set(SessionCreatedKey, createdAt);
            Preferences.Default.Set(SessionExpiresKey, expiresAt);

            var sessionPublished = await PublishAsync(
                sessionToken, uid, createdAt, expiresAt, initialLocation, true, idToken);
            if (!sessionPublished)
            {
                System.Diagnostics.Debug.WriteLine("[LiveLocation] Firebase rejected the initial session write.");
                ClearSession();
                return null;
            }
            StartTrackingLoop(sessionToken, uid, createdAt, expiresAt);
            EnsureAndroidTrackingService();
            return UseAzure
                ? Preferences.Default.Get(SessionViewerUrlKey, string.Empty)
                : $"https://YOUR_HOST/track.html?s={Uri.EscapeDataString(sessionToken)}";
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task ResumeActiveSessionAsync()
    {
        if (!IsActive) return;

        var uid = await _auth.GetLocalIdAsync();
        if (string.IsNullOrWhiteSpace(uid)) return;

        var sessionToken = Preferences.Default.Get(SessionTokenKey, string.Empty);
        var createdAt = Preferences.Default.Get(SessionCreatedKey, 0L);
        var expiresAt = Preferences.Default.Get(SessionExpiresKey, 0L);
        StartTrackingLoop(sessionToken, uid, createdAt, expiresAt);
    }

    public async Task StopSessionAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            CancelTrackingLoop();
            var sessionToken = Preferences.Default.Get(SessionTokenKey, string.Empty);
            var writeToken = Preferences.Default.Get(SessionWriteTokenKey, string.Empty);
            var uid = await _auth.GetLocalIdAsync();
            var idToken = await _auth.GetIdTokenAsync();
            var createdAt = Preferences.Default.Get(SessionCreatedKey, 0L);
            var expiresAt = Preferences.Default.Get(SessionExpiresKey, 0L);

            try
            {
                if (UseAzure && !string.IsNullOrWhiteSpace(sessionToken) && !string.IsNullOrWhiteSpace(writeToken))
                    await _azure.StopAsync(sessionToken, writeToken);
                else if (!string.IsNullOrWhiteSpace(sessionToken) && !string.IsNullOrWhiteSpace(uid) && !string.IsNullOrWhiteSpace(idToken))
                    await PublishAsync(sessionToken, uid, createdAt, expiresAt, null, false, idToken);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LiveLocation] Remote stop failed: {ex.Message}");
            }
            finally
            {
                ClearSession();
                StopAndroidTrackingService();
            }
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private void StartTrackingLoop(string sessionToken, string uid, long createdAt, long expiresAt)
    {
        lock (_trackingLock)
        {
            CancelTrackingLoopLocked();
            _trackingCts = new CancellationTokenSource();
            _trackingTask = TrackSessionAsync(sessionToken, uid, createdAt, expiresAt, _trackingCts.Token);
            _trackingTask.Forget("LiveLocation.TrackingLoop");
        }
    }

    private async Task TrackSessionAsync(string sessionToken, string uid, long createdAt, long expiresAt,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested &&
               DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < expiresAt)
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(8));
                var location = await Geolocation.Default.GetLocationAsync(request, cancellationToken);
                var idToken = UseAzure ? "write-capability" : await _auth.GetIdTokenAsync();
                if (location != null && !string.IsNullOrWhiteSpace(idToken))
                {
                    var published = await PublishAsync(
                        sessionToken, uid, createdAt, expiresAt, location, true, idToken, cancellationToken);
                    if (!published)
                        System.Diagnostics.Debug.WriteLine("[LiveLocation] Location update was rejected; retrying.");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LiveLocation] Update failed; will retry: {ex.Message}");
            }

            try { await Task.Delay(UpdateInterval, cancellationToken); }
            catch (OperationCanceledException) { break; }
        }

        if (cancellationToken.IsCancellationRequested) return;
        try
        {
            var idToken = await _auth.GetIdTokenAsync();
            if (!string.IsNullOrWhiteSpace(idToken))
                await PublishAsync(sessionToken, uid, createdAt, expiresAt, null, false, idToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LiveLocation] Expiry update failed: {ex.Message}");
        }
        finally
        {
            ClearSession();
            StopAndroidTrackingService();
        }
    }

    private void CancelTrackingLoop()
    {
        lock (_trackingLock) CancelTrackingLoopLocked();
    }

    private void CancelTrackingLoopLocked()
    {
        try { _trackingCts?.Cancel(); }
        catch (ObjectDisposedException) { }
        _trackingCts?.Dispose();
        _trackingCts = null;
        _trackingTask = null;
    }

    private Task<bool> PublishAsync(
        string sessionToken,
        string uid,
        long createdAt,
        long expiresAt,
        Location? location,
        bool active,
        string idToken,
        CancellationToken cancellationToken = default)
    {
        if (UseAzure)
        {
            var writeToken = Preferences.Default.Get(SessionWriteTokenKey, string.Empty);
            return location == null || string.IsNullOrWhiteSpace(writeToken)
                ? Task.FromResult(false)
                : _azure.PublishAsync(sessionToken, writeToken, location, cancellationToken);
        }

        var payload = new
        {
            ownerUid = uid,
            active,
            createdAt,
            expiresAt = Math.Min(expiresAt,
                DateTimeOffset.UtcNow.Add(FirebaseRemoteLease).ToUnixTimeMilliseconds()),
            updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            hasLocation = location != null,
            latitude = location?.Latitude ?? 0,
            longitude = location?.Longitude ?? 0,
            accuracy = location?.Accuracy ?? 0,
            speed = location?.Speed ?? 0
        };
        return _firebase.SendDataAsync(
            $"liveLocations/{sessionToken}",
            JsonSerializer.Serialize(payload),
            idToken,
            cancellationToken);
    }

    private static string CreateSessionToken()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return token.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static void ClearSession()
    {
        try
        {
            Preferences.Default.Remove(SessionTokenKey);
            Preferences.Default.Remove(SessionCreatedKey);
            Preferences.Default.Remove(SessionExpiresKey);
            Preferences.Default.Remove(SessionWriteTokenKey);
            Preferences.Default.Remove(SessionViewerUrlKey);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LiveLocation] Could not clear session state: {ex.Message}");
        }
    }

    private static bool UseAzure =>
        string.Equals(AppConfig.LiveLocationProvider, "Azure", StringComparison.OrdinalIgnoreCase);

    private static void EnsureAndroidTrackingService()
    {
#if ANDROID
        try { GBV_Shield.Platforms.Android.LiveLocationForegroundService.EnsureRunning(); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LiveLocation] Could not start Android tracking service: {ex.Message}");
        }
#endif
    }

    private static void StopAndroidTrackingService()
    {
#if ANDROID
        try { GBV_Shield.Platforms.Android.LiveLocationForegroundService.RequestStop(); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LiveLocation] Could not stop Android tracking service: {ex.Message}");
        }
#endif
    }
}
