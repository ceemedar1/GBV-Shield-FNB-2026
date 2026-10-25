using GBV_Shield.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors; // ✅ For Geolocation
using Microsoft.Maui.Storage;
using Microsoft.Maui.Graphics;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GBV_Shield.Features.Shelters
{
    public partial class ShelterPage : ContentPage
    {
        private readonly OSMService _mapsService;
        private SupportCategory _selectedCategory = SupportCategory.Hospital;

        public ShelterPage()
        {
            InitializeComponent();
            _mapsService = App.Services.GetRequiredService<OSMService>();
        }

        private async void OnRefreshClicked(object sender, EventArgs e)
            => await LoadCategoryAsync();

        private async void OnHospitalClicked(object sender, EventArgs e)
        {
            SelectCategory(SupportCategory.Hospital);
            await LoadCategoryAsync();
        }

        private async void OnPoliceClicked(object sender, EventArgs e)
        {
            SelectCategory(SupportCategory.Police);
            await LoadCategoryAsync();
        }

        private async void OnNgoClicked(object sender, EventArgs e)
        {
            SelectCategory(SupportCategory.NgoNpo);
            await LoadCategoryAsync();
        }

        private void SelectCategory(SupportCategory category)
        {
            _selectedCategory = category;
            foreach (var button in new[] { HospitalsButton, PoliceButton, NgoButton })
            {
                button.BackgroundColor = Color.FromArgb("#E0E0E0");
                button.TextColor = Color.FromArgb("#222222");
            }

            var selected = category switch
            {
                SupportCategory.Police => PoliceButton,
                SupportCategory.NgoNpo => NgoButton,
                _ => HospitalsButton
            };
            selected.BackgroundColor = Color.FromArgb("#4CAF50");
            selected.TextColor = Colors.White;
            ShelterList.ItemsSource = null;
            EmptyMessage.Text = "Tap Refresh to search this category near you.";
        }

        private async Task LoadCategoryAsync()
        {
            if (!RefreshButton.IsEnabled) return;
            RefreshButton.IsEnabled = false;
            HospitalsButton.IsEnabled = PoliceButton.IsEnabled = NgoButton.IsEnabled = false;
            LoadingIndicator.IsVisible = LoadingIndicator.IsRunning = true;
            EmptyMessage.Text = "Searching nearby services…";
            SearchSummary.Text = _selectedCategory == SupportCategory.NgoNpo
                ? "Searching child-protection, welfare and abuse-support services within 50 km…"
                : _selectedCategory == SupportCategory.Hospital
                    ? "Searching all hospitals within 10 km and public emergency hospitals within 25 km…"
                : "Searching within 10 km…";
            try
            {
                using var searchTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                // ✅ Try to get device location
                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8));
                var location = await Geolocation.Default.GetLocationAsync(request, searchTimeout.Token);

                if (location == null)
                {
                    EmptyMessage.Text = "Location unavailable. Enable location and tap Refresh.";
                    SearchSummary.Text = "Search could not start.";
                    await DisplayAlertAsync("Location Error", "Unable to determine your current location. Please enable GPS and try again.", "OK");
                    return;
                }

                double lat = location.Latitude;
                double lon = location.Longitude;

                ShelterList.ItemsSource = null;

                // NGO/NPO services are often less densely mapped, so search farther for that category.
                var radiusMeters = _selectedCategory switch
                {
                    SupportCategory.NgoNpo => 50_000,
                    SupportCategory.Hospital => 25_000,
                    _ => 10_000
                };
                var rangeLabel = _selectedCategory == SupportCategory.Hospital
                    ? "all nearby hospitals ≤10 km + public emergency hospitals ≤25 km"
                    : $"within {radiusMeters / 1000} km";
                var results = await _mapsService.GetNearbySupportAsync(
                    lat, lon, _selectedCategory, radiusMeters: radiusMeters,
                    cancellationToken: searchTimeout.Token,
                    onDirectoryReady: contacts =>
                    {
                        ShelterList.ItemsSource = contacts;
                        SearchSummary.Text = $"{contacts.Count} verified public hospitals available • nearest first. Checking nearby hospitals…";
                    });
                ShelterList.ItemsSource = results;
                SearchSummary.Text = $"{results.Count} services: {rangeLabel} • nearest first. " + _mapsService.SearchNotice;

                if (results.Count == 0)
                {
                    EmptyMessage.Text = _selectedCategory == SupportCategory.Hospital
                        ? "No nearby hospitals or verified public emergency hospitals were found. Check location and connection, then retry."
                        : _selectedCategory == SupportCategory.Police
                            ? "No police stations with a listed address were found within 10 km. The directory may be incomplete."
                            : $"No matching services were found within {radiusMeters / 1000} km.";
                }
            }
            catch (FeatureNotSupportedException)
            {
                EmptyMessage.Text = SearchSummary.Text = "Location is not supported on this device.";
                await DisplayAlertAsync("Error", "Location services are not supported on this device.", "OK");
            }
            catch (PermissionException)
            {
                EmptyMessage.Text = SearchSummary.Text = "Location permission is needed. Enable it in Settings, then refresh.";
                await DisplayAlertAsync("Error", "Location permission is denied. Please enable it in settings.", "OK");
            }
            catch (OperationCanceledException)
            {
                EmptyMessage.Text = "The search took too long. Check your connection and tap Refresh.";
                SearchSummary.Text = "Search stopped after 45 seconds.";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NearbyHelp] {ex}");
                EmptyMessage.Text = "Search could not finish. Check your connection and tap Refresh to retry.";
                SearchSummary.Text = "Search unavailable. Please retry.";
                await DisplayAlertAsync("Error", $"Failed to refresh shelters.\n\n{ex.Message}", "OK");
            }
            finally
            {
                LoadingIndicator.IsRunning = LoadingIndicator.IsVisible = false;
                RefreshButton.IsEnabled = true;
                HospitalsButton.IsEnabled = PoliceButton.IsEnabled = NgoButton.IsEnabled = true;
            }
        }

    }
}
