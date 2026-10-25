using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;

namespace GBV_Shield.Services
{
    public static class ShakeDetectionService
    {
        public const string EnabledPreferenceKey = "ShakeSosEnabled";

        private const double TriggerThresholdG = 0.90;
        private const double StrongPeakThresholdG = 1.75;
        private const double RearmThresholdG = 0.65;
        private const int RequiredPeakCount = 3;
        private static readonly TimeSpan PeakWindow = TimeSpan.FromMilliseconds(1600);
        private static readonly TimeSpan MinimumPeakSpacing = TimeSpan.FromMilliseconds(140);
        private static readonly TimeSpan TriggerCooldown = TimeSpan.FromSeconds(60);
        private const double GravityFilterAlpha = 0.82;

        private static readonly object StateLock = new();
        private static bool _running;
        private static bool _initialized;
        private static bool _peakArmed = true;
        private static int _peakCount;
        private static long _firstPeakTimestamp;
        private static long _lastPeakTimestamp;
        private static long _lastTriggerTimestamp;
        private static bool _strongPeakSeen;
        private static double _gravityX;
        private static double _gravityY;
        private static double _gravityZ;
        private static int _triggerInProgress;
#if ANDROID
        private static int _androidServiceStartRequested;
#endif

        public static bool IsEnabled => Preferences.Default.Get(EnabledPreferenceKey, false);
        public static bool IsRunning
        {
            get { lock (StateLock) return _running; }
        }

        public static bool SetEnabled(bool enabled)
        {
            if (!enabled)
            {
                Preferences.Default.Set(EnabledPreferenceKey, false);
#if ANDROID
                StopAndroidForegroundService();
#endif
                Stop();
                return true;
            }

            if (!Accelerometer.Default.IsSupported)
                return false;

            Preferences.Default.Set(EnabledPreferenceKey, true);
#if ANDROID
            if (!StartAndroidForegroundService())
            {
                Preferences.Default.Set(EnabledPreferenceKey, false);
                return false;
            }
#endif
            var started = Start(forceEnable: true);
            if (!started)
            {
                Preferences.Default.Set(EnabledPreferenceKey, false);
#if ANDROID
                StopAndroidForegroundService();
#endif
            }
            return started;
        }

        public static bool EnsureMonitoring()
        {
            if (!IsEnabled) return false;
#if ANDROID
            return StartAndroidForegroundService();
#else
            return Start();
#endif
        }

        public static bool Start() => Start(forceEnable: false);

        private static bool Start(bool forceEnable)
        {
            lock (StateLock)
            {
                if (_running) return true;
                if ((!forceEnable && !IsEnabled) || !Accelerometer.Default.IsSupported) return false;

                ResetState();
                try
                {
                    Accelerometer.Default.ReadingChanged += OnReadingChanged;
                    Accelerometer.Default.Start(SensorSpeed.Game);
                    _running = true;
                }
                catch (Exception ex)
                {
                    Accelerometer.Default.ReadingChanged -= OnReadingChanged;
                    _running = false;
                    ResetState();
                    Debug.WriteLine($"[ShakeDetection] Could not start accelerometer: {ex.Message}");
                    return false;
                }
            }

            Debug.WriteLine("[ShakeDetection] Started");
            return true;
        }

        public static void Stop()
        {
            lock (StateLock)
            {
                if (!_running) return;

                try
                {
                    Accelerometer.Default.ReadingChanged -= OnReadingChanged;
                    if (Accelerometer.Default.IsMonitoring)
                        Accelerometer.Default.Stop();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ShakeDetection] Could not stop accelerometer cleanly: {ex.Message}");
                }
                finally
                {
                    _running = false;
                    ResetState();
                }
            }

            Debug.WriteLine("[ShakeDetection] Stopped");
        }

        private static void OnReadingChanged(object? sender, AccelerometerChangedEventArgs args)
        {
            var acceleration = args.Reading.Acceleration;
            var shouldTrigger = false;

            lock (StateLock)
            {
                if (!_running) return;

                if (!_initialized)
                {
                    _gravityX = acceleration.X;
                    _gravityY = acceleration.Y;
                    _gravityZ = acceleration.Z;
                    _initialized = true;
                    return;
                }

                _gravityX = GravityFilterAlpha * _gravityX + (1 - GravityFilterAlpha) * acceleration.X;
                _gravityY = GravityFilterAlpha * _gravityY + (1 - GravityFilterAlpha) * acceleration.Y;
                _gravityZ = GravityFilterAlpha * _gravityZ + (1 - GravityFilterAlpha) * acceleration.Z;

                var linearX = acceleration.X - _gravityX;
                var linearY = acceleration.Y - _gravityY;
                var linearZ = acceleration.Z - _gravityZ;
                var magnitude = Math.Sqrt(linearX * linearX + linearY * linearY + linearZ * linearZ);

                if (magnitude < RearmThresholdG)
                {
                    _peakArmed = true;
                    return;
                }

                if (!_peakArmed || magnitude < TriggerThresholdG) return;
                _peakArmed = false;
                shouldTrigger = RegisterPeak(Stopwatch.GetTimestamp(), magnitude);
            }

            if (shouldTrigger)
                TriggerSosAsync().Forget("ShakeDetection.TriggerSOS");
        }

        private static bool RegisterPeak(long now, double magnitude)
        {
            if (_lastTriggerTimestamp != 0 && Stopwatch.GetElapsedTime(_lastTriggerTimestamp, now) < TriggerCooldown)
                return false;

            if (_lastPeakTimestamp != 0 && Stopwatch.GetElapsedTime(_lastPeakTimestamp, now) < MinimumPeakSpacing)
                return false;

            if (_firstPeakTimestamp == 0 || Stopwatch.GetElapsedTime(_firstPeakTimestamp, now) > PeakWindow)
            {
                _firstPeakTimestamp = now;
                _peakCount = 1;
                _strongPeakSeen = magnitude >= StrongPeakThresholdG;
            }
            else
            {
                _peakCount++;
                _strongPeakSeen |= magnitude >= StrongPeakThresholdG;
            }

            _lastPeakTimestamp = now;
            Debug.WriteLine($"[ShakeDetection] Peak {_peakCount}/{RequiredPeakCount}, linear acceleration {magnitude:F2}g");
            if (_peakCount < RequiredPeakCount) return false;
            if (!_strongPeakSeen)
            {
                Debug.WriteLine("[ShakeDetection] Pattern ignored: no strong deliberate-shake peak.");
                _peakCount = 0;
                _firstPeakTimestamp = 0;
                return false;
            }

            _lastTriggerTimestamp = now;
            _peakCount = 0;
            _firstPeakTimestamp = 0;
            return true;
        }

        private static async Task TriggerSosAsync()
        {
            if (Interlocked.Exchange(ref _triggerInProgress, 1) != 0) return;

            try
            {
                Debug.WriteLine("[ShakeDetection] Three-peak pattern detected; dispatching SOS.");
                var dispatched = await MainThread.InvokeOnMainThreadAsync(() => SosService.SendSosAsync(silent: true));
                Debug.WriteLine($"[ShakeDetection] SOS dispatch result: {dispatched}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ShakeDetection] SOS dispatch failed: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _triggerInProgress, 0);
            }
        }

        private static void ResetState()
        {
            _initialized = false;
            _peakArmed = true;
            _peakCount = 0;
            _firstPeakTimestamp = 0;
            _lastPeakTimestamp = 0;
            _strongPeakSeen = false;
            _gravityX = _gravityY = _gravityZ = 0;
        }

#if ANDROID
        private static bool StartAndroidForegroundService()
        {
            if (GBV_Shield.Platforms.Android.ShakeSosForegroundService.IsActive)
                return true;

            // Android can call App.OnStart and App.OnResume close together. Do not
            // create multiple foreground-service requests while one is pending.
            if (Interlocked.CompareExchange(ref _androidServiceStartRequested, 1, 0) != 0)
                return true;

            try
            {
                var context = Android.App.Application.Context;
                var intent = new Android.Content.Intent(context, typeof(GBV_Shield.Platforms.Android.ShakeSosForegroundService));
                if (OperatingSystem.IsAndroidVersionAtLeast(26))
                    context.StartForegroundService(intent);
                else
                    context.StartService(intent);
                return true;
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _androidServiceStartRequested, 0);
                Debug.WriteLine($"[ShakeDetection] Could not start background service: {ex.Message}");
                return false;
            }
        }

        internal static void NotifyAndroidServiceStarted() =>
            Volatile.Write(ref _androidServiceStartRequested, 1);

        internal static void NotifyAndroidServiceStopped() =>
            Interlocked.Exchange(ref _androidServiceStartRequested, 0);

        private static void StopAndroidForegroundService()
        {
            try
            {
                var context = Android.App.Application.Context;
                context.StopService(new Android.Content.Intent(context, typeof(GBV_Shield.Platforms.Android.ShakeSosForegroundService)));
                Interlocked.Exchange(ref _androidServiceStartRequested, 0);
            }
            catch (Exception ex)
            {
                Interlocked.Exchange(ref _androidServiceStartRequested, 0);
                Debug.WriteLine($"[ShakeDetection] Could not stop background service: {ex.Message}");
            }
        }
#endif
    }
}
