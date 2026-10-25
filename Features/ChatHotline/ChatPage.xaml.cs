using GBV_Shield.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui;
using System;

namespace GBV_Shield.Features.ChatHotline
{
    public partial class ChatPage : ContentPage
    {
        private readonly NativeAiChatService _chat = new();
        private bool _busy;
        private DateTimeOffset _lastSent;

        public ChatPage()
        {
            InitializeComponent();
            if (!NativeAiChatService.IsSupported)
            {
                LoadStatus.Text = "AI chat is not available in this build. Use SOS or Nearby Help in an emergency.";
                ConsentCheckBox.IsEnabled = false;
                MessageEntry.IsEnabled = false;
                SendButton.IsEnabled = false;
            }
        }

        private void ConsentChanged(object? sender, CheckedChangedEventArgs e) => UpdateSendButton();

        private void MessageChanged(object? sender, TextChangedEventArgs e) => UpdateSendButton();

        private void UpdateSendButton() => SendButton.IsEnabled = NativeAiChatService.IsSupported &&
            !_busy && ConsentCheckBox.IsChecked && !string.IsNullOrWhiteSpace(MessageEntry.Text);

        private async void SendClicked(object? sender, EventArgs e)
        {
            var text = MessageEntry.Text?.Trim();
            if (_busy || !ConsentCheckBox.IsChecked || string.IsNullOrEmpty(text)) return;
            if (DateTimeOffset.UtcNow - _lastSent < TimeSpan.FromSeconds(8))
            {
                LoadStatus.Text = "Please wait a few seconds before sending again.";
                return;
            }
            _busy = true;
            _lastSent = DateTimeOffset.UtcNow;
            UpdateSendButton();
            LoadStatus.Text = "AI is replying… No human responder has been contacted.";
            AddBubble(text, true);
            MessageEntry.Text = string.Empty;
            await MessagesScroll.ScrollToAsync(Messages, ScrollToPosition.End, false);
            try
            {
                var answer = await _chat.SendAsync(text);
                AddBubble(answer, false);
                LoadStatus.Text = "AI reply — may be inaccurate. No human responder has been contacted.";
                await MessagesScroll.ScrollToAsync(Messages, ScrollToPosition.End, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NativeAI] {ex}");
                LoadStatus.Text = "AI could not reply. You can retry your message. In an emergency, use SOS or Nearby Help.";
            }
            finally { _busy = false; UpdateSendButton(); }
        }

        private void AddBubble(string text, bool isUser)
        {
            Messages.Children.Add(new Border
            {
                Padding = new Thickness(12),
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(16) },
                BackgroundColor = Color.FromArgb(isUser ? "#6D28D9" : "#EDE9FE"),
                HorizontalOptions = isUser ? LayoutOptions.End : LayoutOptions.Start,
                MaximumWidthRequest = 460,
                Content = new Label { Text = text, TextColor = Color.FromArgb(isUser ? "#FFFFFF" : "#25143C") }
            });
        }

        private void ClearClicked(object? sender, EventArgs e)
        {
            _chat.Clear();
            Messages.Children.Clear();
            MessageEntry.Text = string.Empty;
            ConsentCheckBox.IsChecked = false;
            LoadStatus.Text = NativeAiChatService.IsSupported
                ? "Chat cleared from this screen. Provider logs are not deleted."
                : "AI chat is not available in this build. Use SOS or Nearby Help in an emergency.";
        }

        protected override void OnDisappearing()
        {
            ClearClicked(this, EventArgs.Empty);
            base.OnDisappearing();
        }

        private async void BackClicked(object? sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync(".."); }
            catch { await Navigation.PopAsync(); }
        }
    }
}
