using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GBV_Shield.Services
{
    /// <summary>
    /// Polls device GPS and forwards coordinates to ReportsService.SubmitLocationAsync().
    /// </summary>
    public class LocationService
    {
        private readonly ReportsService _reports;
        private readonly object _trackingLock = new();
        private CancellationTokenSource? _cts;
        private Task? _trackingTask;

        public LocationService(ReportsService reports) => _reports = reports;

        public Task StartTrackingAsync()
        {
            lock (_trackingLock)
            {
                if (_trackingTask is { IsCompleted: false }) return Task.CompletedTask;
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                _trackingTask = TrackAsync(_cts.Token);
                _trackingTask.Forget("LocationService.TrackingLoop");
            }

            return Task.CompletedTask;
        }

        private async Task TrackAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var request = new GeolocationRequest(GeolocationAccuracy.High, TimeSpan.FromSeconds(10));
                    var location = await Geolocation.Default.GetLocationAsync(request, cancellationToken);
                    if (location != null)
                        await _reports.SubmitLocationAsync(location.Latitude, location.Longitude);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (FeatureNotSupportedException ex)
                {
                    Console.WriteLine($"[LocationService] GPS not supported: {ex.Message}");
                    break;
                }
                catch (PermissionException ex)
                {
                    Console.WriteLine($"[LocationService] Permission denied: {ex.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LocationService] Update failed; retrying: {ex.Message}");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        public void StopTracking()
        {
            lock (_trackingLock)
            {
                try { _cts?.Cancel(); }
                catch (ObjectDisposedException) { }
                _cts?.Dispose();
                _cts = null;
                _trackingTask = null;
            }
        }
    }
}
