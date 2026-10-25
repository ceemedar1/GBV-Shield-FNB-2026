using GBV_Shield.Services;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GBV_Shield
{
    public partial class App : Application
    {
        private readonly AuthService _auth;
        private readonly ReportsService _reports;
        private Page _rootPage;
        private CancellationTokenSource? _shakeResumeCts;

        // Keep DI service provider accessible globally
        public static IServiceProvider Services { get; private set; } = default!;

        // DI-friendly constructor (matches your MauiProgram registrations)
        public App(AuthService auth, ReportsService reports, IServiceProvider services)
        {
            InitializeComponent();

            _auth = auth;
            _reports = reports;
            Services = services;

            // Global crash guards
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[CRASH GUARD] UnhandledException: {e.ExceptionObject}");
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                e.SetObserved();
                System.Diagnostics.Debug.WriteLine($"[CRASH GUARD] UnobservedTaskException: {e.Exception}");
            };

            // Startup page (matches your project)
            _rootPage = new Features.Common.SplashPage(_auth, _reports);

        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(_rootPage);
            window.Title = "GBV Shield";
            return window;
        }

        public void SetRootPage(Page page)
        {
            _rootPage = page;
            if (Windows.Count > 0)
                Windows[0].Page = page;
        }

        protected override void OnStart()
        {
            base.OnStart();
        }

        protected override void OnSleep()
        {
            base.OnSleep();
            _shakeResumeCts?.Cancel();
            // Android's opt-in foreground safety service owns monitoring in the background.
        }

        protected override void OnResume()
        {
            base.OnResume();
            _shakeResumeCts?.Cancel();
            _shakeResumeCts?.Dispose();
            _shakeResumeCts = new CancellationTokenSource();
            ResumeShakeMonitoringAsync(_shakeResumeCts.Token).Forget("ShakeDetection.Resume");
        }

        private static async Task ResumeShakeMonitoringAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Let MAUI finish drawing the first page before requesting an Android
                // foreground service. A busy startup previously caused Android's
                // ForegroundServiceDidNotStartInTimeException.
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                    ShakeDetectionService.EnsureMonitoring();
            }
            catch (OperationCanceledException)
            {
                // The app returned to the background before the delayed start.
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShakeDetection] Resume start failed: {ex.Message}");
            }
        }
    }
}
