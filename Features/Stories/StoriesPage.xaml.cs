using System;
using System.Threading.Tasks;
using GBV_Shield.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;

namespace GBV_Shield.Features.Stories;

public partial class StoriesPage : ContentPage
{
    private readonly StoryService _storyService;
    private bool _loading;

    public StoriesPage()
    {
        InitializeComponent();
        _storyService = App.Services.GetRequiredService<StoryService>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadStoriesAsync();
    }

    private async void OnAddStoryClicked(object sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync(nameof(AddStoryPage)); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Stories.Navigation] {ex}"); }
    }

    private async void OnRefreshClicked(object sender, EventArgs e) => await LoadStoriesAsync();
    private async void OnRefreshing(object sender, EventArgs e) => await LoadStoriesAsync();

    private async Task LoadStoriesAsync()
    {
        if (_loading) return;
        _loading = true;
        LoadingIndicator.IsVisible = LoadingIndicator.IsRunning = StoriesCollection.ItemsSource is null;
        RetryButton.IsVisible = false;
        try
        {
            var stories = await _storyService.GetStoriesAsync();
            StoriesCollection.ItemsSource = stories;
            EmptyMessage.Text = "No community stories have been shared yet.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Stories] {ex}");
            StoriesCollection.ItemsSource = Array.Empty<Story>();
            EmptyMessage.Text = "Stories could not load. Check your connection and try again.";
            RetryButton.IsVisible = true;
        }
        finally
        {
            StoriesRefresh.IsRefreshing = false;
            LoadingIndicator.IsRunning = LoadingIndicator.IsVisible = false;
            _loading = false;
        }
    }

    private async void OnDeleteStoryClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: Story story } || !story.IsOwner) return;
        try
        {
            if (!await DisplayAlertAsync("Delete story?", "This permanently removes your story from Community Stories.", "Delete", "Cancel")) return;
            await _storyService.DeleteStoryAsync(story.Id);
            await LoadStoriesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Stories] Delete failed: {ex}");
            try { await DisplayAlertAsync("Could not delete", "Check your connection and try again.", "OK"); } catch { }
        }
    }
}
