using System;
using GBV_Shield.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;

namespace GBV_Shield.Features.Stories;

public partial class AddStoryPage : ContentPage
{
    private readonly StoryService _storyService;
    private bool _posting;

    public AddStoryPage()
    {
        InitializeComponent();
        _storyService = App.Services.GetRequiredService<StoryService>();
    }

    private void OnContentChanged(object sender, TextChangedEventArgs e)
        => CharacterCount.Text = $"{e.NewTextValue?.Length ?? 0} / {StorySafety.MaximumLength}";

    private async void OnPostStoryClicked(object sender, EventArgs e)
    {
        if (_posting) return;
        StatusLabel.Text = string.Empty;
        var content = ContentEditor.Text?.Trim();
        var validation = StorySafety.Validate(content);
        if (validation is not null)
        {
            StatusLabel.Text = validation;
            return;
        }
        if (!PrivacyCheck.IsChecked)
        {
            StatusLabel.Text = "Please confirm the privacy statement before sharing.";
            return;
        }
        try
        {
            var confirmed = await DisplayAlertAsync(
                "Share anonymously?",
                "Your story will immediately become visible to signed-in GBV Shield users. It is not sent as an emergency report.",
                "Share", "Review again");
            if (!confirmed) return;

            _posting = true;
            PostButton.IsEnabled = false;
            PostingIndicator.IsVisible = PostingIndicator.IsRunning = true;
            await _storyService.AddStoryAsync(content!);
            ContentEditor.Text = string.Empty;
            await Shell.Current.GoToAsync("..");
        }
        catch (InvalidOperationException ex)
        {
            StatusLabel.Text = ex.Message;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Stories] Post failed: {ex}");
            StatusLabel.Text = "Your story was not shared. Check your connection and try again.";
        }
        finally
        {
            if (_posting)
            {
                _posting = false;
                PostButton.IsEnabled = true;
                PostingIndicator.IsRunning = PostingIndicator.IsVisible = false;
            }
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync(".."); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Stories.Navigation] {ex}"); }
    }
}
