using Microsoft.Maui.ApplicationModel;  // ✅ This gives access to Permissions.*
using System;
using System.Threading.Tasks;

namespace GBV_Shield.Services
{
    public static class PermissionService
    {
        public static async Task<bool> RequestShakeSosPermissionsAsync()
        {
            try
            {
                var location = await RequestPermissionAsync<Microsoft.Maui.ApplicationModel.Permissions.LocationWhenInUse>();
                if (location != PermissionStatus.Granted) return false;

#if ANDROID
                var sms = await RequestPermissionAsync<GBV_Shield.Services.Permissions.SmsPermission>();
                if (sms != PermissionStatus.Granted) return false;

                // A visible ongoing notification is required for reliable background monitoring.
                _ = await RequestPermissionAsync<Microsoft.Maui.ApplicationModel.Permissions.PostNotifications>();
#endif
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PermissionService] Shake SOS permission error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Helper that checks and requests a single permission type.
        /// </summary>
        private static async Task<PermissionStatus> RequestPermissionAsync<T>() where T : Microsoft.Maui.ApplicationModel.Permissions.BasePermission, new()
        {
            try
            {
                var status = await Microsoft.Maui.ApplicationModel.Permissions.CheckStatusAsync<T>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Microsoft.Maui.ApplicationModel.Permissions.RequestAsync<T>();
                    System.Diagnostics.Debug.WriteLine($"[PermissionService] {typeof(T).Name} → {status}");
                }
                return status;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PermissionService] RequestPermissionAsync<{typeof(T).Name}> error: {ex.Message}");
                return PermissionStatus.Denied;
            }
        }
    }
}
