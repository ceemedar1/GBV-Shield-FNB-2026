using System;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;

namespace GBV_Shield.Services.Permissions
{
#pragma warning disable CA1416 // Android-only permission implementation
    public class SmsPermission
        : Microsoft.Maui.ApplicationModel.Permissions.BasePermission
    {
#if ANDROID
        private const string SmsPermissionName = Android.Manifest.Permission.SendSms;
#endif

        public override Task<PermissionStatus> CheckStatusAsync()
        {
#if ANDROID
            var context = Android.App.Application.Context;
            if (context == null)
                return Task.FromResult(PermissionStatus.Denied);

            var status = context.CheckSelfPermission(SmsPermissionName);

            return Task.FromResult(
                status == Android.Content.PM.Permission.Granted
                    ? PermissionStatus.Granted
                    : PermissionStatus.Denied);
#else
            return Task.FromResult(PermissionStatus.Granted);
#endif
        }

        public override Task<PermissionStatus> RequestAsync()
        {
#if ANDROID
            var context = Android.App.Application.Context;
            if (context == null)
                return Task.FromResult(PermissionStatus.Denied);

            var status = context.CheckSelfPermission(SmsPermissionName);
            if (status == Android.Content.PM.Permission.Granted)
                return Task.FromResult(PermissionStatus.Granted);

            return Microsoft.Maui.ApplicationModel.Permissions
                .RequestAsync<Microsoft.Maui.ApplicationModel.Permissions.Sms>();
#else
            return Task.FromResult(PermissionStatus.Granted);
#endif
        }

        public override void EnsureDeclared()
        {
#if ANDROID
            // ✅ Fixes CS8602 safely
            var context = Android.App.Application.Context;
            var packageManager = context?.PackageManager;

            if (packageManager == null)
                return;

            if (!packageManager.HasSystemFeature(
                    Android.Content.PM.PackageManager.FeatureTelephony))
            {
                throw new PermissionException(
                    "This device does not support SMS sending.");
            }
#endif
        }

        public override bool ShouldShowRationale()
        {
#if ANDROID
            var activity = Platform.CurrentActivity;
            if (activity == null)
                return false;

            return AndroidX.Core.App.ActivityCompat
                .ShouldShowRequestPermissionRationale(
                    activity,
                    SmsPermissionName);
#else
            return false;
#endif
        }
    }
#pragma warning restore CA1416
}
