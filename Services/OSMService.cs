using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.Communication;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using System.Text.RegularExpressions;

namespace GBV_Shield.Services
{
    public enum SupportCategory
    {
        Hospital,
        Police,
        NgoNpo
    }

    public sealed class OSMService
    {
        private static readonly Uri[] OverpassEndpoints =
        {
            new("https://maps.mail.ru/osm/tools/overpass/api/interpreter"),
            new("https://overpass.private.coffee/api/interpreter"),
            new("https://overpass-api.de/api/interpreter")
        };
        private readonly HttpClient _httpClient;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Location> _officeLocations = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SearchCacheEntry> _searchCache = new();
        public string SearchNotice { get; private set; } = string.Empty;

        private sealed record SearchCacheEntry(DateTimeOffset CreatedAt, List<PoiResult> Results, string Notice);

        private const string SupportNames = @"\b(child[\s-]*line|child[\s-]*(?:(?:and|&)\s*family\s*)?welfare|child(?:ren(?:'s|s)?)?[\s-]*(protection|safeguarding|advocacy)|child(?:ren(?:'s|s)?)?\s*(and|&)\s*youth\s*care|children(?:'s|s)?\s*home|kidz?\s*clinic|teddy\s*bear\s*(clinic|foundation)|victim\s*(support|empowerment)|rape\s*(crisis|support|care)|sexual\s*(violence|assault|abuse)|domestic\s*(abuse|violence)|gender.based\s*violence|gbv|women.?s?\s*shelter|women\s*(and|&)\s*children|safe\s*house|place\s*of\s*safety|trauma\s*cent(re|er)|thuthuzela|people\s*opposing\s*women\s*abuse|powa)\b";

        internal static bool IsRelevantSupport(JsonElement tags)
        {
            var name = GetTag(tags, "name") ?? "";
            if (Regex.IsMatch(name, @"\b(scouts?|casino|arcade|cultural\s*precinct)\b", RegexOptions.IgnoreCase)) return false;
            if (Regex.IsMatch(name, SupportNames, RegexOptions.IgnoreCase)) return true;
            if (Regex.IsMatch(GetTag(tags, "social_work") ?? "", @"(^|;)\s*(child_protection|family_support|crisis_centre)\s*(;|$)", RegexOptions.IgnoreCase)) return true;
            var purpose = GetTag(tags, "social_facility:for") ?? "";
            return Regex.IsMatch(purpose, @"(^|;)\s*(abused|victims|domestic_violence|women|children|juvenile|orphans)\s*(;|$)", RegexOptions.IgnoreCase)
                && (GetTag(tags, "amenity") == "social_facility" || GetTag(tags, "office") is "ngo" or "charity" or "association"
                    || GetTag(tags, "social_facility") is "shelter" or "group_home" or "outreach" or "care_home");
        }

        public OSMService()
        {
            // Use the managed transport so Android socket failures have consistent
            // HttpRequestException semantics and can fall through to the next server.
            _httpClient = new HttpClient(new SocketsHttpHandler
            {
                ConnectTimeout = TimeSpan.FromSeconds(10),
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            }) { Timeout = TimeSpan.FromSeconds(15) };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("GBV-Shield/1.0 support-location-search");
        }

        internal OSMService(HttpClient httpClient) => _httpClient = httpClient;

        public async Task<List<PoiResult>> GetNearbySupportAsync(
            double latitude,
            double longitude,
            SupportCategory category = SupportCategory.Hospital,
            int? radiusMeters = null,
            CancellationToken cancellationToken = default,
            Action<List<PoiResult>>? onDirectoryReady = null)
        {
            SearchNotice = string.Empty;
            var radius = Math.Clamp(radiusMeters ?? (category == SupportCategory.NgoNpo ? 50_000 : 10_000), 500, 50_000);
            var cacheKey = $"{category}:{radius}:{Math.Round(latitude, 3)}:{Math.Round(longitude, 3)}";
            if (_searchCache.TryGetValue(cacheKey, out var cached) &&
                DateTimeOffset.UtcNow - cached.CreatedAt < TimeSpan.FromMinutes(5))
            {
                SearchNotice = "Updated recently • cached for faster access.";
                return cached.Results.ToList();
            }

            var saved = await GetDirectoryAsync(latitude, longitude, category, radius, cancellationToken);
            saved = saved.OrderBy(x => x.Distance).ToList();
            if (saved.Count > 0) onDirectoryReady?.Invoke(saved);
            using var liveTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            liveTimeout.CancelAfter(TimeSpan.FromSeconds(22));
            List<PoiResult> mapped;
            try { mapped = await GetMappedAsync(latitude, longitude, category, radius, liveTimeout.Token); }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested &&
                ex is HttpRequestException or JsonException or OperationCanceledException)
            {
                mapped = new();
                SearchNotice = "Live search unavailable. Showing saved directory contacts; coverage may be incomplete.";
            }
            // Keep every mapped hospital within 10 km, plus verified public
            // emergency hospitals from the saved directory up to 25 km.
            if (category == SupportCategory.Hospital && radius > 10_000)
                mapped = mapped.Where(x => x.Distance <= 10_000).ToList();
            if (mapped.Count == 0 && saved.Count == 0 && SearchNotice.Length > 0)
                throw new HttpRequestException("Live search is unavailable and no saved contacts could be located nearby.");
            var combined = new List<PoiResult>(saved);
            foreach (var item in mapped.OrderBy(x => x.Distance))
            {
                if (combined.Any(x => SamePlace(x, item))) continue;
                combined.Add(item);
            }
            if (category == SupportCategory.NgoNpo && SearchNotice.Length == 0)
                SearchNotice = "Child-protection and abuse-support directory; coverage may be incomplete. Call before visiting.";
            var finalResults = combined.Where(x => x.Distance <= radius).OrderBy(x => x.Distance).ToList();
            _searchCache[cacheKey] = new SearchCacheEntry(DateTimeOffset.UtcNow, finalResults, SearchNotice);
            return finalResults;
        }

        private static bool SamePlace(PoiResult a, PoiResult b)
        {
            static string Key(string name) => Regex.Replace(name.ToLowerInvariant(), @"\b(saps|police|station|south african|suid.afrikaanse|polisiediens)\b|[^a-z0-9]", "");
            return Key(a.Name) == Key(b.Name) && DistanceMeters(a.Latitude, a.Longitude, b.Latitude, b.Longitude) < 1000
                || a.Type == "Police Station" && b.Type == a.Type && DistanceMeters(a.Latitude, a.Longitude, b.Latitude, b.Longitude) < 100;
        }

        private async Task<List<PoiResult>> GetDirectoryAsync(double latitude, double longitude, SupportCategory category, int radius, CancellationToken cancellationToken)
        {
            using var geocodingSlots = new SemaphoreSlim(3);
            var missingLocations = 0;
            var tasks = SupportDirectory.Entries.Where(x => x.Category == category).Select(async entry =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Location? location = entry.Latitude is double lat && entry.Longitude is double lon ? new Location(lat, lon) : null;
                if (location is null && !_officeLocations.TryGetValue(entry.Address, out location))
                {
                    await geocodingSlots.WaitAsync(cancellationToken);
                    try
                    {
                        location = (await Geocoding.Default.GetLocationsAsync(entry.Address + ", South Africa")
                            .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken)).FirstOrDefault();
                        if (location != null) _officeLocations[entry.Address] = location;
                    }
                    catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
                    finally { geocodingSlots.Release(); }
                }
                if (location is null) { Interlocked.Increment(ref missingLocations); return null; }
                var distance = DistanceMeters(latitude, longitude, location.Latitude, location.Longitude);
                if (distance > radius) return null;
                return new PoiResult { Name = entry.Name, Address = entry.Address, Phone = entry.Phone,
                    Type = category switch
                    {
                        SupportCategory.Hospital => "Public Hospital • 24-hour emergency",
                        SupportCategory.Police => "Police Station",
                        _ => string.IsNullOrWhiteSpace(entry.ServiceLabel) ? "NGO / NPO" : entry.ServiceLabel
                    },
                    Latitude = location.Latitude, Longitude = location.Longitude, Distance = distance,
                    WebsiteUrl = entry.Source,
                    SourceLabel = category == SupportCategory.NgoNpo
                        ? $"Verified public contact point • checked {entry.CheckedOn} • call before visiting"
                        : $"Public directory • checked {entry.CheckedOn} • call before visiting",
                    MapsUrl = "https://www.google.com/maps/search/?api=1&query=" + Uri.EscapeDataString(entry.Name + ", " + entry.Address) };
            });
            var results = await Task.WhenAll(tasks);
            if (missingLocations > 0) SearchNotice = "Some directory offices could not be located; results are incomplete.";
            return results.Where(x => x != null).Select(x => x!).ToList();
        }

        private async Task<List<PoiResult>> GetMappedAsync(double latitude, double longitude, SupportCategory category, int? radiusMeters, CancellationToken cancellationToken)
        {
            var radius = Math.Clamp(radiusMeters ?? Constants.ShelterSearchRadius, 500, 50_000);
            var lat = latitude.ToString(CultureInfo.InvariantCulture);
            var lon = longitude.ToString(CultureInfo.InvariantCulture);
            // Bounding-box lookups avoid expensive around geometry scans at 50 km.
            // Enforce the circular radius on the returned coordinates below.
            var latSpan = radius / 111_000d;
            var lonSpan = latSpan / Math.Max(0.01, Math.Cos(latitude * Math.PI / 180));
            var bounds = FormattableString.Invariant($"{latitude - latSpan},{longitude - lonSpan},{latitude + latSpan},{longitude + lonSpan}");
            var categoryQuery = category switch
            {
                SupportCategory.Hospital =>
                    $"nwr(around:{radius},{lat},{lon})[\"amenity\"=\"hospital\"];",
                SupportCategory.Police =>
                    $"nwr(around:{radius},{lat},{lon})[\"amenity\"=\"police\"]; nwr(around:{radius},{lat},{lon})[\"police\"=\"station\"];",
                SupportCategory.NgoNpo => $$"""
                    nwr(around:{{radius}},{{lat}},{{lon}})["office"~"^(ngo|association|charity)$"];
                    nwr(around:{{radius}},{{lat}},{{lon}})["amenity"="social_facility"];
                    nwr(around:{{radius}},{{lat}},{{lon}})["social_facility"~"^(shelter|group_home|assisted_living|outreach|care_home)$"];
                    nwr(around:{{radius}},{{lat}},{{lon}})["social_work"~"^(yes|child_protection|family_support|crisis_centre)$"];
                    nwr(around:{{radius}},{{lat}},{{lon}})["amenity"~"^(clinic|doctors)$"]["name"~"(child|kidz|welfare|childline|gbv|abuse|victim|women|family|crisis|safe.?house|shelter)",i];
                    nwr(around:{{radius}},{{lat}},{{lon}})["name"~"(childline|child.?welfare|kidz.?clinic|safe.?house|place.?of.?safety|abuse.?shelter|victim.?support|women.?shelter|rape|sexual.?violence|thuthuzela|trauma.?cent|gbv)",i];
                    nwr(around:{{radius}},{{lat}},{{lon}})["name"~"(child.*(welfare|protection|safeguarding|advocacy|youth.?care|home)|teddy.?bear.?(clinic|foundation)|kid.?clinic|women.*children)",i];
                    nwr(around:{{radius}},{{lat}},{{lon}})["emergency"="social_facility"];
                    """,
                _ => throw new ArgumentOutOfRangeException(nameof(category))
            };
            categoryQuery = categoryQuery.Replace($"around:{radius},{lat},{lon}", bounds);
            var query = $$"""
                [out:json][timeout:7];
                (
                  {{categoryQuery}}
                );
                out center tags;
                """;

            HttpResponseMessage? response = null;
            Exception? lastError = null;
            foreach (var endpoint in OverpassEndpoints)
            {
                try
                {
                    using var attemptTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    attemptTimeout.CancelAfter(TimeSpan.FromSeconds(7));
                    using var content = new FormUrlEncodedContent(
                        new Dictionary<string, string> { ["data"] = query });
                    var attempt = await _httpClient.PostAsync(endpoint, content, attemptTimeout.Token);
                    if (attempt.IsSuccessStatusCode)
                    {
                        try
                        {
                            using var check = JsonDocument.Parse(await attempt.Content.ReadAsStringAsync(attemptTimeout.Token));
                            if (check.RootElement.TryGetProperty("remark", out _) ||
                                !check.RootElement.TryGetProperty("elements", out var list) || list.ValueKind != JsonValueKind.Array)
                                throw new JsonException("Incomplete map response");
                        }
                        catch (JsonException)
                        {
                            attempt.Dispose();
                            lastError = new HttpRequestException("Location directory returned incomplete results.");
                            continue;
                        }
                        response = attempt;
                        break;
                    }

                    lastError = new HttpRequestException(
                        $"Location directory returned {(int)attempt.StatusCode} ({attempt.ReasonPhrase}).");
                    attempt.Dispose();
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested &&
                    (ex is HttpRequestException or OperationCanceledException or System.IO.IOException))
                {
                    lastError = ex;
                }
            }

            if (response is null)
                throw new HttpRequestException("The location directory is temporarily unavailable.", lastError);

            using (response)
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (!document.RootElement.TryGetProperty("elements", out var elements))
                    return new List<PoiResult>();

                var results = new List<(PoiResult Result, bool NeedsAddress)>();
                foreach (var element in elements.EnumerateArray())
                {
                    if (!TryGetCoordinates(element, out var resultLatitude, out var resultLongitude)) continue;

                    var tags = element.TryGetProperty("tags", out var tagElement) ? tagElement : default;
                    var name = GetTag(tags, "name");
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var distance = DistanceMeters(latitude, longitude, resultLatitude, resultLongitude);
                    if (distance > radius) continue;
                    if (category == SupportCategory.NgoNpo && !IsRelevantSupport(tags)) continue;
                    if (GetTag(tags, "access") is "private" or "no" ||
                        GetTag(tags, "disused") == "yes" || GetTag(tags, "abandoned") == "yes") continue;

                    // Do not infer round-the-clock availability from the word hospital.
                    if (category == SupportCategory.Hospital &&
                        GetTag(tags, "opening_hours")?.Trim() != "24/7") continue;
                    if (category == SupportCategory.Police &&
                        (GetTag(tags, "police") is string policeType && policeType != "station" ||
                         name.Contains("Academy", StringComparison.OrdinalIgnoreCase) ||
                         name.Contains("Flying Squad", StringComparison.OrdinalIgnoreCase) ||
                         name.Contains("Immigration", StringComparison.OrdinalIgnoreCase))) continue;

                    var address = FormatAddress(tags);
                    var needsAddress = address == "Address not supplied";
                    if (needsAddress)
                        address = FormattableString.Invariant($"Map location: {resultLatitude:F5}, {resultLongitude:F5} (tap Directions)");

                    var website = GetTag(tags, "website") ?? GetTag(tags, "contact:website");
                    results.Add((new PoiResult
                    {
                        Name = name,
                        Address = address,
                        Type = GetTypeLabel(tags, category),
                        Phone = GetTag(tags, "phone") ?? GetTag(tags, "contact:phone"),
                        WebsiteUrl = NormalizeWebsite(website),
                        MapsUrl = $"https://www.openstreetmap.org/?mlat={resultLatitude.ToString(CultureInfo.InvariantCulture)}&mlon={resultLongitude.ToString(CultureInfo.InvariantCulture)}#map=17/{resultLatitude.ToString(CultureInfo.InvariantCulture)}/{resultLongitude.ToString(CultureInfo.InvariantCulture)}",
                        Distance = distance,
                        Latitude = resultLatitude, Longitude = resultLongitude,
                        SourceLabel = "OpenStreetMap • address and availability may need confirmation"
                    }, needsAddress));
                }

                // Resolve missing street addresses concurrently. The previous sequential
                // three-second lookup per hospital could make this screen take minutes.
                using var geocodingSlots = new SemaphoreSlim(4);
                var addressTasks = results.Select(async candidate =>
                {
                    if (!candidate.NeedsAddress) return candidate.Result;
                    try
                    {
                        await geocodingSlots.WaitAsync(cancellationToken);
                        try
                        {
                            var place = (await Geocoding.Default.GetPlacemarksAsync(
                                candidate.Result.Latitude, candidate.Result.Longitude)
                                .WaitAsync(TimeSpan.FromSeconds(4), cancellationToken)).FirstOrDefault();
                            if (!string.IsNullOrWhiteSpace(place?.Thoroughfare))
                            {
                                candidate.Result.Address = "Near " + string.Join(", ",
                                    new[] { place.Thoroughfare, place.SubLocality, place.Locality }
                                        .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());
                            }
                        }
                        finally
                        {
                            geocodingSlots.Release();
                        }
                    }
                    catch (Exception)
                    {
                        // Retain the coordinates-and-directions fallback rather than
                        // hiding a potentially critical hospital from the user.
                    }
                    return candidate.Result;
                });

                var resolved = await Task.WhenAll(addressTasks);
                return resolved.Where(result => result.Distance <= radius)
                    .GroupBy(result => (result.Name, result.Latitude, result.Longitude))
                    .Select(group => group.First()).OrderBy(result => result.Distance).ToList();
            }
        }

        private static bool TryGetCoordinates(JsonElement element, out double latitude, out double longitude)
        {
            if (element.TryGetProperty("lat", out var lat) && element.TryGetProperty("lon", out var lon) ||
                element.TryGetProperty("center", out var center) &&
                center.TryGetProperty("lat", out lat) && center.TryGetProperty("lon", out lon))
            {
                latitude = lat.GetDouble();
                longitude = lon.GetDouble();
                return true;
            }
            latitude = longitude = 0;
            return false;
        }

        private static string? GetTag(JsonElement tags, string name) =>
            tags.ValueKind == JsonValueKind.Object && tags.TryGetProperty(name, out var value) ? value.GetString() : null;

        private static string FormatAddress(JsonElement tags)
        {
            var full = GetTag(tags, "addr:full");
            if (!string.IsNullOrWhiteSpace(full)) return full;
            var street = GetTag(tags, "addr:street") ?? GetTag(tags, "contact:street") ?? GetTag(tags, "addr:place");
            if (string.IsNullOrWhiteSpace(street)) return "Address not supplied";
            var address = string.Join(", ", new[] { GetTag(tags, "addr:housenumber") ?? GetTag(tags, "contact:housenumber"),
                street, GetTag(tags, "addr:suburb"), GetTag(tags, "addr:city") ?? GetTag(tags, "contact:city") }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
            return string.IsNullOrWhiteSpace(address) ? "Address not supplied" : address;
        }

        private static string GetTypeLabel(JsonElement tags, SupportCategory category) => category switch
        {
            SupportCategory.Police => "Police Station",
            SupportCategory.NgoNpo => "NGO / NPO",
            _ => "Hospital • listed as 24 hours"
        };

        private static string? NormalizeWebsite(string? website)
        {
            if (string.IsNullOrWhiteSpace(website)) return null;
            var candidate = website.Contains("://") ? website : $"https://{website}";
            return Uri.TryCreate(candidate, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? uri.AbsoluteUri : null;
        }

        private static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadius = 6_371_000;
            var latDelta = DegreesToRadians(lat2 - lat1);
            var lonDelta = DegreesToRadians(lon2 - lon1);
            var a = Math.Sin(latDelta / 2) * Math.Sin(latDelta / 2) +
                    Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                    Math.Sin(lonDelta / 2) * Math.Sin(lonDelta / 2);
            return earthRadius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
    }

    public sealed class PoiResult
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string MapsUrl { get; set; } = string.Empty;
        public string? WebsiteUrl { get; set; }
        public string? Phone { get; set; }
        public double Distance { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string SourceLabel { get; set; } = string.Empty;
        public double DistanceKm => Math.Round(Distance / 1000, 1);
        public string WebsiteDisplay => WebsiteUrl ?? string.Empty;
        public bool HasPhone => !string.IsNullOrWhiteSpace(Phone);
        public bool HasWebsite => !string.IsNullOrWhiteSpace(WebsiteUrl);
        public string TagColor =>
            Type.Contains("Police", StringComparison.OrdinalIgnoreCase) ? "#1565C0" :
            Type.Contains("Hospital", StringComparison.OrdinalIgnoreCase) ? "#C62828" :
            Type.Contains("rape", StringComparison.OrdinalIgnoreCase) || Type.Contains("sexual", StringComparison.OrdinalIgnoreCase) ? "#AD1457" :
            Type.Contains("shelter", StringComparison.OrdinalIgnoreCase) ? "#2E7D32" :
            Type.Contains("child", StringComparison.OrdinalIgnoreCase) ? "#EF6C00" :
            "#455A64";

        public ICommand OpenMapsCommand => new Command(() => OpenMapsAsync().Forget("NearbyHelp.OpenMaps"));
        public ICommand OpenWebsiteCommand => new Command(() => OpenWebsiteAsync().Forget("NearbyHelp.OpenWebsite"));
        public ICommand CallCommand => new Command(() =>
        {
            try
            {
                var first = Phone?.Split(new[] { ';', ',', '/' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                var number = Regex.Replace(first ?? "", @"[^+0-9]", "");
                if (number.Length > 0 && PhoneDialer.Default.IsSupported) PhoneDialer.Default.Open(number);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NearbyHelp.Call] Could not open dialer: {ex.Message}");
            }
        });

        private async Task OpenMapsAsync()
        {
            if (Uri.TryCreate(MapsUrl, UriKind.Absolute, out var uri))
                await Launcher.Default.OpenAsync(uri);
        }

        private async Task OpenWebsiteAsync()
        {
            if (Uri.TryCreate(WebsiteUrl, UriKind.Absolute, out var uri))
                await Launcher.Default.OpenAsync(uri);
        }
    }
}
