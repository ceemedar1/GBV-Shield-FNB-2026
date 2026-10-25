# Platform builds

GBV Shield has one shared .NET MAUI codebase and three supported delivery targets.

| Target | Build command | Package identity | Delivery channel |
| --- | --- | --- | --- |
| Android | `dotnet build GBV_Shield.csproj -f net10.0-android --no-restore -v:minimal` | `com.gbvshield.app` | Google Play or direct APK |
| Huawei | `dotnet restore GBV_Shield.csproj -p:HuaweiBuild=true` then `dotnet build GBV_Shield.csproj -f net10.0-android --no-restore -p:HuaweiBuild=true -p:OutputPath=bin\HuaweiAppGallery\ -v:minimal` | `com.gbvshield.app.huawei` | AppGallery or direct APK |
| iOS | `dotnet build GBV_Shield.csproj -f net10.0-ios --no-restore -t:Compile -v:minimal` | configured by the iOS signing profile | TestFlight / App Store |

The Huawei variant keeps the core safety features: sign-in, SOS, SMS, GPS, background shake detection, live location, chat, shelters and Stories. It excludes Firebase Cloud Messaging because it relies on Google Mobile Services. Add Huawei Push Kit later if remote push notifications are required on devices without Google services.

Create release archives using the protected signing credentials. iOS `.ipa` archive and App Store submission require macOS with Xcode and an Apple signing profile.
