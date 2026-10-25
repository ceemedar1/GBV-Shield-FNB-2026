using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GBV_Shield.Models;
using Microsoft.Maui.ApplicationModel;

namespace GBV_Shield.Services
{
    public sealed class ChatService
    {
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
        private readonly AuthService _auth;
        private CancellationTokenSource? _pollingCts;
        private Task? _pollingTask;
        private readonly object _pollingLock = new();
        private string? _uid;
        private string? _idToken;

        public ObservableCollection<ChatMessage> Messages { get; } = new();

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public ChatService(AuthService auth)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        }

        public async Task InitializeAsync()
        {
            _uid = await _auth.GetLocalIdAsync();
            _idToken = await _auth.GetIdTokenAsync();
        }

        public void StartPolling()
        {
            lock (_pollingLock)
            {
                if (_pollingTask is { IsCompleted: false }) return;
                _pollingCts?.Dispose();
                _pollingCts = new CancellationTokenSource();
                _pollingTask = PollLoopAsync(_pollingCts.Token);
                _pollingTask.Forget("Chat.PollingLoop");
            }
        }

        public void StopPolling()
        {
            lock (_pollingLock)
            {
                try { _pollingCts?.Cancel(); }
                catch (ObjectDisposedException) { }
                _pollingCts?.Dispose();
                _pollingCts = null;
                _pollingTask = null;
            }
        }

        public async Task<bool> SendMessageAsync(string? text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (!await EnsureIdentityAsync()) return false;

            var message = new ChatMessage
            {
                SenderId = _uid!,
                Text = text.Trim(),
                TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                IsSelf = true
            };

            var json = JsonSerializer.Serialize(message, JsonOpts);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(BuildMessagesUrl(), content, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await InitializeAsync();
                return false;
            }

            response.EnsureSuccessStatusCode();
            return true;
        }

        private async Task<bool> EnsureIdentityAsync()
        {
            if (string.IsNullOrWhiteSpace(_uid) || string.IsNullOrWhiteSpace(_idToken))
                await InitializeAsync();

            return !string.IsNullOrWhiteSpace(_uid) && !string.IsNullOrWhiteSpace(_idToken);
        }

        private string BuildMessagesUrl()
        {
            var conversationId = Uri.EscapeDataString(_uid!);
            return $"{Constants.FirebaseDatabaseUrl.TrimEnd('/')}/conversations/{conversationId}/messages.json?auth={Uri.EscapeDataString(_idToken!)}";
        }

        private async Task PollLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await RefreshMessagesAsync(cancellationToken);
                    await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Chat] Poll failed; retrying: {ex.Message}");
                    try { await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }

        private async Task RefreshMessagesAsync(CancellationToken cancellationToken)
        {
            if (!await EnsureIdentityAsync()) return;

            using var response = await _http.GetAsync(BuildMessagesUrl(), cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await InitializeAsync();
                return;
            }
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var items = new List<ChatMessage>();

            if (!string.IsNullOrWhiteSpace(json) && json != "null")
            {
                var messages = JsonSerializer.Deserialize<Dictionary<string, ChatMessage>>(json, JsonOpts);
                if (messages is not null)
                    items = messages.Values.OrderBy(message => message.TimestampMs).ToList();
            }

            foreach (var message in items)
                message.IsSelf = message.SenderId == _uid;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Messages.Clear();
                foreach (var message in items) Messages.Add(message);
            });
        }
    }
}
