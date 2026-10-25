using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GBV_Shield.Config;

namespace GBV_Shield.Services;
public sealed record AiChatTurn(string Role, string Text);
public sealed class AiChatService(AuthService auth)
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(50) };
    public bool IsConfigured => Uri.TryCreate(AppConfig.AiChatBaseUrl, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;
    public async Task<string> ReplyAsync(IReadOnlyList<AiChatTurn> messages, CancellationToken token)
    {
        if (!IsConfigured) throw new InvalidOperationException("AI support is not activated yet. Use SOS or Nearby Help if you need assistance.");
        var idToken = await auth.GetIdTokenAsync();
        if (string.IsNullOrEmpty(idToken)) throw new InvalidOperationException("Please sign in again to use AI support.");
        using var request = new HttpRequestMessage(HttpMethod.Post, AppConfig.AiChatBaseUrl.TrimEnd('/') + "/chat");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
        request.Content = new StringContent(JsonSerializer.Serialize(new { messages }), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, token);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("Please sign in again to use AI support.");
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new InvalidOperationException("AI support is busy. Retry in a minute. Don't wait for chat in an emergency.");
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("AI support is unavailable. No counsellor has been contacted. Use SOS or Nearby Help.");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var text = json.RootElement.GetProperty("text").GetString();
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("No AI reply received. Please try again.");
        return text;
    }
}
