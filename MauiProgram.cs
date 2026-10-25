using Microsoft.Maui;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using CommunityToolkit.Maui;
using Plugin.LocalNotification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;
using System.IO;
using GBV_Shield.Services;
using GBV_Shield.Models;

namespace GBV_Shield
{
    public static class MauiProgram
    {
        public static MauiApp CurrentApp { get; private set; } = default!;

        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
#if ANDROID || IOS || MACCATALYST
                .UseLocalNotification()
#endif
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // ✅ Core services
            builder.Services.AddSingleton<AuthService>();
            builder.Services.AddSingleton<LocationService>();
            builder.Services.AddSingleton<FirebaseService>();
            builder.Services.AddSingleton<AzureLiveLocationClient>();
            builder.Services.AddSingleton<LiveLocationService>();
            builder.Services.AddSingleton<ReportsService>();
            builder.Services.AddSingleton<ChatService>();
            builder.Services.AddSingleton<AiChatService>();
            builder.Services.AddTransient<Features.ChatHotline.ChatViewModel>();
            builder.Services.AddSingleton<NotificationService>();
            builder.Services.AddSingleton<OSMService>();

            // ✅ Local DB
            builder.Services.AddSingleton(sp =>
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "gbv_reports.db3");
                return new LocalReportsStore(dbPath);
            });

            // ✅ ✅ ✅ API‑BASED STORY SERVICE
            builder.Services.AddSingleton<StoriesAuthService>();
            builder.Services.AddSingleton<StoryService>();

            CurrentApp = builder.Build();
            return CurrentApp;
        }
    }
}
