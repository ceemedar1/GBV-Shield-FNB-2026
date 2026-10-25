using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using GBV_Shield.Config;
using Microsoft.Maui.Devices.Sensors;

namespace GBV_Shield.Services;

public sealed class AzureLiveLocationClient
{
    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri(AppConfig.ApiBaseUrl),
        Timeout = TimeSpan.FromSeconds(12)
    };

    public async Task<AzureTrackingSession?> CreateSessionAsync(string idToken, CancellationToken token = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "tracking/sessions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
        using var response = await _http.SendAsync(request, token);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<AzureTrackingSession>(cancellationToken: token);
    }

    public async Task<bool> PublishAsync(string viewerToken, string writeToken, Location location,
        CancellationToken token = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put,
            $"tracking/sessions/{Uri.EscapeDataString(viewerToken)}/location");
        request.Headers.Add("X-Tracking-Key", writeToken);
        request.Content = JsonContent.Create(new
        {
            latitude = location.Latitude,
            longitude = location.Longitude,
            accuracy = location.Accuracy ?? 0,
            speed = location.Speed ?? 0
        });
        using var response = await _http.SendAsync(request, token);
        return response.IsSuccessStatusCode;
    }

    public async Task StopAsync(string viewerToken, string writeToken, CancellationToken token = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete,
            $"tracking/sessions/{Uri.EscapeDataString(viewerToken)}");
        request.Headers.Add("X-Tracking-Key", writeToken);
        using var response = await _http.SendAsync(request, token);
    }
}

public sealed record AzureTrackingSession(
    string ViewerToken,
    string WriteToken,
    string ViewerUrl,
    DateTimeOffset ExpiresAt);
