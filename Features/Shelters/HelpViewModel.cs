using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GBV_Shield.Features.Common;
using GBV_Shield.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace GBV_Shield.Features.Shelters
{
    public partial class HelpViewModel : BaseViewModel
    {
        private readonly OSMService _mapsService;

        public ObservableCollection<PoiResult> Results { get; } = new();

        public HelpViewModel(OSMService mapsService)
        {
            _mapsService = mapsService;
            Title = $"Nearby Shelters & Police (within {Constants.ShelterSearchRadius / 1000} km)";
        }

        [RelayCommand]
        public async Task SearchAsync()   // ✅ Changed from private → public
        {
            IsBusy = true;
            Results.Clear();

            try
            {
                var location = await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium));

                if (location == null)
                {
                    await Shell.Current.DisplayAlertAsync("Location Error", "Unable to get your current location.", "OK");
                    return;
                }

                var results = await _mapsService.GetNearbySupportAsync(location.Latitude, location.Longitude);

                if (results.Count == 0)
                {
                    await Shell.Current.DisplayAlertAsync("No Results", "No shelters or police stations found nearby.", "OK");
                    return;
                }

                foreach (var r in results)
                    Results.Add(r);
            }
            catch (FeatureNotSupportedException)
            {
                await Shell.Current.DisplayAlertAsync("Error", "Location services are not supported on this device.", "OK");
            }
            catch (PermissionException)
            {
                await Shell.Current.DisplayAlertAsync("Permission Denied", "Location permission was denied. Please enable it in settings.", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlertAsync("Error", $"Something went wrong: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
