using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GBV_Shield.Config;
using GBV_Shield.Models;
using Microsoft.Maui.Networking;

namespace GBV_Shield.Services
{
    public enum ReportSubmissionStatus
    {
        Sent,
        Queued
    }

    public class ReportsService
    {
        private readonly HttpClient _http;
        private readonly LocalReportsStore _localStore;
        private readonly AuthService _auth;
        private readonly SemaphoreSlim _syncLock = new(1, 1);

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public ReportsService(LocalReportsStore localStore, AuthService auth)
        {
            if (string.IsNullOrWhiteSpace(AppConfig.ApiBaseUrl))
                throw new InvalidOperationException("AppConfig.ApiBaseUrl is not set.");

            _localStore = localStore ?? throw new ArgumentNullException(nameof(localStore));
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _http = new HttpClient
            {
                BaseAddress = new Uri(AppConfig.ApiBaseUrl),
                Timeout = TimeSpan.FromSeconds(20)
            };

            Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;

        }

        public async Task<ReportSubmissionStatus> SubmitReportAsync(string description, string type)
        {
            var report = new Report
            {
                Text = description.Trim(),
                Type = string.IsNullOrWhiteSpace(type) ? "general" : type.Trim(),
                CreatedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            // Persist first so an app close or network interruption cannot lose the report.
            await _localStore.AddReportAsync(report);

            if (!await PostReportAsync(report))
                return ReportSubmissionStatus.Queued;

            await _localStore.RemoveReportAsync(report.Id);
            return ReportSubmissionStatus.Sent;
        }

        public async Task<bool> SubmitLocationAsync(double latitude, double longitude)
        {
            var payload = new { latitude, longitude, createdAt = DateTime.UtcNow };
            return await PostJsonAsync("location", payload);
        }

        public async Task SyncPendingReportsAsync()
        {
            if (!await _syncLock.WaitAsync(0)) return;

            try
            {
                var pending = await _localStore.GetPendingReportsAsync();
                foreach (var report in pending)
                {
                    if (!await PostReportAsync(report)) break;
                    await _localStore.RemoveReportAsync(report.Id);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ReportsService] SyncPendingReportsAsync error: {ex}");
            }
            finally
            {
                _syncLock.Release();
            }
        }

        private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs args)
        {
            if (args.NetworkAccess == NetworkAccess.Internet)
                SyncPendingReportsAsync().Forget("Reports.ConnectivitySync");
        }

        private Task<bool> PostReportAsync(Report report)
        {
            var payload = new
            {
                type = report.Type,
                description = report.Text,
                createdAt = DateTimeOffset.FromUnixTimeMilliseconds(report.CreatedAtMs).UtcDateTime
            };
            return PostJsonAsync("reports", payload);
        }

        private async Task<bool> PostJsonAsync(string relativeUrl, object payload)
        {
            try
            {
                var json = JsonSerializer.Serialize(payload, JsonOpts);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var idToken = await _auth.GetIdTokenAsync();
                if (string.IsNullOrWhiteSpace(idToken)) return false;

                using var request = new HttpRequestMessage(HttpMethod.Post, relativeUrl)
                {
                    Content = content
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

                using var response = await _http.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ReportsService] POST {relativeUrl} failed: {ex}");
                return false;
            }
        }
    }
}
