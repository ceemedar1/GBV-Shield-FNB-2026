using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GBV_Shield.Config;

namespace GBV_Shield.Services;

public sealed class StoryService(StoriesAuthService storiesAuth)
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<Story>> GetStoriesAsync(CancellationToken cancellationToken = default)
    {
        var (uid, token) = await storiesAuth.GetIdentityAsync(cancellationToken);
        using var ownershipResponse = await _http.GetAsync(
            $"{AppConfig.StoriesDatabaseUrl}/userStories/{Uri.EscapeDataString(uid)}.json?auth={Uri.EscapeDataString(token)}",
            cancellationToken);
        EnsureSuccess(ownershipResponse);
        var ownershipJson = await ownershipResponse.Content.ReadAsStringAsync(cancellationToken);
        var ownedIds = string.IsNullOrWhiteSpace(ownershipJson) || ownershipJson == "null"
            ? new HashSet<string>()
            : (JsonSerializer.Deserialize<Dictionary<string, bool>>(ownershipJson, JsonOptions) ?? [])
                .Where(pair => pair.Value).Select(pair => pair.Key).ToHashSet();
        var orderBy = Uri.EscapeDataString("\"createdAtMs\"");
        using var response = await _http.GetAsync(
            $"{AppConfig.StoriesDatabaseUrl}/stories.json?auth={Uri.EscapeDataString(token)}&orderBy={orderBy}&limitToLast=100",
            cancellationToken);
        EnsureSuccess(response);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(json) || json == "null") return [];
        var items = JsonSerializer.Deserialize<Dictionary<string, Story>>(json, JsonOptions) ?? [];
        return items.Select(pair =>
            {
                pair.Value.Id = pair.Key;
                pair.Value.Author = "Anonymous";
                pair.Value.IsOwner = ownedIds.Contains(pair.Key);
                return pair.Value;
            })
            .Where(story => !string.IsNullOrWhiteSpace(story.Content) && story.CreatedAtMs > 0)
            .OrderByDescending(story => story.CreatedAtMs)
            .ToList();
    }

    public async Task AddStoryAsync(string content, CancellationToken cancellationToken = default)
    {
        var (uid, token) = await storiesAuth.GetIdentityAsync(cancellationToken);
        var storyId = Guid.NewGuid().ToString("N");
        var story = new Story
        {
            Author = "Anonymous",
            Content = content.Trim(),
            CreatedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        var update = new Dictionary<string, object>
        {
            [$"stories/{storyId}"] = story,
            [$"userStories/{uid}/{storyId}"] = true
        };
        using var body = new StringContent(JsonSerializer.Serialize(update, JsonOptions), Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Patch,
            $"{AppConfig.StoriesDatabaseUrl}/.json?auth={Uri.EscapeDataString(token)}") { Content = body };
        using var response = await _http.SendAsync(request, cancellationToken);
        EnsureSuccess(response);
    }

    public async Task DeleteStoryAsync(string storyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storyId)) throw new ArgumentException("Story ID is required.", nameof(storyId));
        var (uid, token) = await storiesAuth.GetIdentityAsync(cancellationToken);
        var update = new Dictionary<string, object?>
        {
            [$"stories/{storyId}"] = null,
            [$"userStories/{uid}/{storyId}"] = null
        };
        using var body = new StringContent(JsonSerializer.Serialize(update, JsonOptions), Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Patch,
            $"{AppConfig.StoriesDatabaseUrl}/.json?auth={Uri.EscapeDataString(token)}") { Content = body };
        using var response = await _http.SendAsync(request, cancellationToken);
        EnsureSuccess(response);
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new InvalidOperationException("Your session expired or Stories access is not enabled. Please sign in again.");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Stories service returned {(int)response.StatusCode}.");
    }
}
