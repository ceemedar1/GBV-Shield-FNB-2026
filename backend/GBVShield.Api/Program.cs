using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<AiChatClient>(client => client.Timeout = TimeSpan.FromSeconds(40));
builder.Services.AddSingleton<TrackingStore>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("chat", o =>
    {
        o.PermitLimit = 20;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("location", o =>
    {
        o.PermitLimit = 12;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    });
});

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapAiChat();

app.MapPost("/api/tracking/sessions", async (
    HttpRequest request, TrackingStore store, IHttpClientFactory clients,
    IConfiguration config, CancellationToken cancellationToken) =>
{
    var uid = await FirebaseTokenValidator.ValidateAsync(request, clients, config, cancellationToken);
    if (uid is null) return Results.Unauthorized();

    var now = DateTimeOffset.UtcNow;
    var configuredHours = config.GetValue("Tracking:SessionHours", 8);
    var duration = TimeSpan.FromHours(Math.Clamp(configuredHours, 1, 8));
    var entity = TrackingEntity.Create(uid, now, now.Add(duration));
    await store.UpsertAsync(entity, cancellationToken);
    var publicBaseUrl = config["Tracking:PublicBaseUrl"]?.TrimEnd('/') ?? "";
    return Results.Ok(new SessionResponse(entity.RowKey, entity.WriteToken,
        $"{publicBaseUrl}?s={Uri.EscapeDataString(entity.RowKey)}", entity.ExpiresAt));
});

app.MapPut("/api/tracking/sessions/{viewerToken}/location", async (
    string viewerToken, LocationUpdate update, HttpRequest request,
    TrackingStore store, IConfiguration config, CancellationToken cancellationToken) =>
{
    var entity = await store.GetAsync(viewerToken, cancellationToken);
    if (entity is null || entity.ExpiresAt <= DateTimeOffset.UtcNow || !entity.Active)
        return Results.NotFound();
    if (!FixedTimeEquals(entity.WriteToken, request.Headers["X-Tracking-Key"].ToString()))
        return Results.Unauthorized();
    if (update.Latitude is < -90 or > 90 || update.Longitude is < -180 or > 180)
        return Results.BadRequest(new { error = "Invalid coordinates" });
    var maxAccuracy = config.GetValue("Tracking:MaximumAccuracyMetres", 500d);
    if (update.Accuracy is < 0 || update.Accuracy > maxAccuracy)
        return Results.BadRequest(new { error = "Invalid accuracy" });

    entity.Latitude = update.Latitude;
    entity.Longitude = update.Longitude;
    entity.Accuracy = update.Accuracy;
    entity.Speed = update.Speed;
    entity.HasLocation = true;
    entity.UpdatedAt = DateTimeOffset.UtcNow;
    await store.UpsertAsync(entity, cancellationToken);
    return Results.NoContent();
}).RequireRateLimiting("location");

app.MapDelete("/api/tracking/sessions/{viewerToken}", async (
    string viewerToken, HttpRequest request, TrackingStore store, CancellationToken cancellationToken) =>
{
    var entity = await store.GetAsync(viewerToken, cancellationToken);
    if (entity is null) return Results.NoContent();
    if (!FixedTimeEquals(entity.WriteToken, request.Headers["X-Tracking-Key"].ToString()))
        return Results.Unauthorized();
    entity.Active = false;
    entity.UpdatedAt = DateTimeOffset.UtcNow;
    await store.UpsertAsync(entity, cancellationToken);
    return Results.NoContent();
});

app.MapGet("/api/tracking/public/{viewerToken}", async (
    string viewerToken, TrackingStore store, CancellationToken cancellationToken) =>
{
    var entity = await store.GetAsync(viewerToken, cancellationToken);
    if (entity is null || !entity.Active || entity.ExpiresAt <= DateTimeOffset.UtcNow)
        return Results.NotFound();
    return Results.Ok(new PublicLocation(entity.HasLocation, entity.Latitude, entity.Longitude,
        entity.Accuracy, entity.Speed, entity.UpdatedAt, entity.ExpiresAt));
}).RequireRateLimiting("location");

app.Run();

static bool FixedTimeEquals(string expected, string supplied)
{
    var left = System.Text.Encoding.UTF8.GetBytes(expected);
    var right = System.Text.Encoding.UTF8.GetBytes(supplied);
    return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
}

record LocationUpdate(double Latitude, double Longitude, double Accuracy, double Speed);
record SessionResponse(string ViewerToken, string WriteToken, string ViewerUrl, DateTimeOffset ExpiresAt);
record PublicLocation(bool HasLocation, double Latitude, double Longitude, double Accuracy,
    double Speed, DateTimeOffset UpdatedAt, DateTimeOffset ExpiresAt);

sealed class TrackingEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "active";
    public string RowKey { get; set; } = "";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public string OwnerUid { get; set; } = "";
    public string WriteToken { get; set; } = "";
    public bool Active { get; set; }
    public bool HasLocation { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Accuracy { get; set; }
    public double Speed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    public static TrackingEntity Create(string uid, DateTimeOffset now, DateTimeOffset expires) => new()
    {
        RowKey = Token(), OwnerUid = uid, WriteToken = Token(), Active = true,
        CreatedAt = now, UpdatedAt = now, ExpiresAt = expires
    };

    private static string Token() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
}

sealed class TrackingStore(IConfiguration config)
{
    private readonly TableClient _table = new(config["Storage:ConnectionString"]
        ?? throw new InvalidOperationException("Storage:ConnectionString is required"),
        config["Storage:TableName"] ?? "LiveLocations");
    private int _initialized;

    private async Task InitializeAsync(CancellationToken token)
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 0)
            await _table.CreateIfNotExistsAsync(token);
    }

    public async Task<TrackingEntity?> GetAsync(string token, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        try { return (await _table.GetEntityAsync<TrackingEntity>("active", token, cancellationToken: cancellationToken)).Value; }
        catch (RequestFailedException ex) when (ex.Status == 404) { return null; }
    }

    public async Task UpsertAsync(TrackingEntity entity, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }
}

static class FirebaseTokenValidator
{
    public static async Task<string?> ValidateAsync(HttpRequest request, IHttpClientFactory clients,
        IConfiguration config, CancellationToken cancellationToken)
    {
        var header = request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        var apiKey = config["Firebase:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) return null;
        using var response = await clients.CreateClient().PostAsJsonAsync(
            $"https://identitytoolkit.googleapis.com/v1/accounts:lookup?key={WebUtility.UrlEncode(apiKey)}",
            new { idToken = header[7..].Trim() }, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        return json.RootElement.TryGetProperty("users", out var users) && users.GetArrayLength() > 0
            ? users[0].GetProperty("localId").GetString() : null;
    }
}

public partial class Program { }
