using System.Collections.ObjectModel;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GBV_Shield.Models;
using GBV_Shield.Services;

namespace GBV_Shield.Features.ChatHotline
{
    public partial class ChatViewModel : ObservableObject
    {
        private readonly AiChatService _chat;
        private CancellationTokenSource? _request;

        public ObservableCollection<ChatMessage> Messages { get; } = new();
        [ObservableProperty] private string statusText = string.Empty;
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private bool hasConsented;

        [ObservableProperty]
        private string outgoingText = string.Empty;

        public ChatViewModel(AiChatService chat)
        {
            _chat = chat;
        }

        public Task StartAsync()
        {
            StatusText = _chat.IsConfigured ? "Ready when you are." : "AI support is not activated yet.";
            return Task.CompletedTask;
        }

        public void Stop()
        {
            _request?.Cancel();
            Messages.Clear();
            OutgoingText = "";
            HasConsented = false;
        }

        [RelayCommand]
        private void ClearChat() { Stop(); StatusText = "Chat cleared from this screen. Provider logs are not deleted."; }

        [RelayCommand]
        private async Task SendMessageAsync()
        {
            if (IsBusy || string.IsNullOrWhiteSpace(OutgoingText)) return;
            if (!HasConsented) { StatusText = "Please read and accept the AI chat notice first."; return; }
            var text = OutgoingText.Trim();
            if (text.Length > 2000) { StatusText = "Keep your message under 2,000 characters."; return; }
            var history = Messages.TakeLast(6).Select(m => new AiChatTurn(m.IsSelf ? "user" : "assistant", m.Text))
                .Append(new AiChatTurn("user", text))
                .Select(t => t with { Text = t.Text.Length > 2000 ? t.Text[..2000] : t.Text }).ToArray();
            using var cts = new CancellationTokenSource();
            _request = cts;
            IsBusy = true;
            StatusText = "AI is replying… This does not contact emergency services.";
            try
            {
                var reply = await _chat.ReplyAsync(history, cts.Token);
                cts.Token.ThrowIfCancellationRequested();
                Messages.Add(new ChatMessage { Text = text, IsSelf = true, SenderId = "You", TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
                Messages.Add(new ChatMessage { Text = reply, SenderId = "AI support", TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
                while (Messages.Count > 40) Messages.RemoveAt(0);
                if (OutgoingText.Trim() == text) OutgoingText = "";
                StatusText = "AI reply — may be inaccurate. No human responder has been contacted.";
            }
            catch (OperationCanceledException) { StatusText = cts.IsCancellationRequested ? "Chat cleared." : "AI timed out. Retry; don't wait for chat in an emergency."; }
            catch (InvalidOperationException ex) { StatusText = ex.Message; }
            catch (Exception) { StatusText = "Could not get an AI reply. Check your connection. Use SOS or Nearby Help for assistance."; }
            finally { IsBusy = false; _request = null; }
        }
    }
}
