# Huawei / AppGallery build

Build the Huawei-compatible APK from the repository root:

```powershell
dotnet restore GBV_Shield.csproj -p:HuaweiBuild=true
dotnet build GBV_Shield.csproj -f net10.0-android --no-restore -p:HuaweiBuild=true -p:OutputPath=bin\HuaweiAppGallery\ -v:minimal
```

Restore with `HuaweiBuild=true` each time you switch from the standard Android
build. The two flavors have different NuGet dependency graphs; reusing the
standard restore can omit Dagger/reCAPTCHA classes and break Huawei AI chat at
runtime. Restore again without `HuaweiBuild=true` before building standard
Android.

The resulting APK is `bin\HuaweiAppGallery\com.gbvshield.app.huawei-Signed.apk`.

The Huawei build keeps sign-in, SOS, background shake detection, SMS, GPS, live location, chat, shelters and Stories. It uses the existing Firebase REST APIs, so it does not depend on Google Play Services.

Firebase Cloud Messaging is deliberately excluded because Huawei devices without Google Mobile Services cannot receive FCM reliably. The build sets the Huawei package ID and excludes the FCM service at compile time. The app's local safety-status notification still works. To add remote push notifications later, create an AppGallery Connect app with package ID `com.gbvshield.app.huawei`, download `agconnect-services.json`, and configure Huawei Push Kit credentials in the secure build environment.

Before publishing to AppGallery, create the matching Android application in the existing Firebase project if its API key has Android application restrictions enabled. The same Firebase backend can be used once the Huawei package is allow-listed.
