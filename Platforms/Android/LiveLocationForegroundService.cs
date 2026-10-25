using System;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using GBV_Shield.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GBV_Shield.Platforms.Android;

[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeLocation)]
public sealed class LiveLocationForegroundService : Service
{
    private const int NotificationId = 7302;
    private const string ChannelId = "gbv_shield_live_location";
    private CancellationTokenSource? _expiryCts;

    public static void EnsureRunning()
    {
        var context = global::Android.App.Application.Context;
        var intent = new Intent(context, typeof(LiveLocationForegroundService));
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            context.StartForegroundService(intent);
        else
            context.StartService(intent);
    }

    public static void RequestStop()
    {
        var context = global::Android.App.Application.Context;
        context.StopService(new Intent(context, typeof(LiveLocationForegroundService)));
    }

    public override void OnCreate()
    {
        base.OnCreate();
        try
        {
            CreateNotificationChannel();
            StartForeground(NotificationId, BuildNotification());
        }
        catch (Exception ex)
        {
            Log.Error("LiveLocation", $"Could not start tracking service: {ex}");
            StopSelf();
        }
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var tracking = App.Services.GetService<LiveLocationService>();
        if (tracking?.IsActive != true)
        {
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        tracking.ResumeActiveSessionAsync().Forget("LiveLocation.ServiceResume");
        _expiryCts?.Cancel();
        _expiryCts?.Dispose();
        _expiryCts = new CancellationTokenSource();
        _ = StopAfterExpiryAsync(tracking, _expiryCts.Token);
        return StartCommandResult.Sticky;
    }

    private async Task StopAfterExpiryAsync(LiveLocationService tracking, CancellationToken token)
    {
        try
        {
            while (tracking.IsActive)
                await Task.Delay(TimeSpan.FromMinutes(1), token);
            if (!token.IsCancellationRequested) StopSelf();
        }
        catch (System.OperationCanceledException) { }
    }

    public override void OnDestroy()
    {
        _expiryCts?.Cancel();
        _expiryCts?.Dispose();
        _expiryCts = null;
        base.OnDestroy();
    }

    public override IBinder? OnBind(Intent? intent) => null;

    private void CreateNotificationChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        manager?.CreateNotificationChannel(new NotificationChannel(
            ChannelId, "Live SOS location", NotificationImportance.Low)
        {
            Description = "Shares your location during an active SOS session."
        });
    }

    private Notification BuildNotification()
    {
        var launchIntent = PackageManager?.GetLaunchIntentForPackage(PackageName!);
        var pendingIntent = launchIntent == null ? null : PendingIntent.GetActivity(
            this, 0, launchIntent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        var builder = OperatingSystem.IsAndroidVersionAtLeast(26)
            ? new Notification.Builder(this, ChannelId)
            : new Notification.Builder(this);
        return builder.SetContentTitle("GBV Shield live location active")
            .SetContentText("Sharing your SOS location for up to 8 hours")
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetOngoing(true)
            .SetCategory(Notification.CategoryService)
            .SetContentIntent(pendingIntent)
            .Build();
    }
}
