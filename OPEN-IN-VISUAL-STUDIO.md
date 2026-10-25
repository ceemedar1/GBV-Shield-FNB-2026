# Open the project

1. Extract the ZIP completely before opening it.
2. Install a Visual Studio version supporting .NET 10 and its .NET MAUI workload, including Android tools. The SDK pinned in global.json is 10.0.400.
3. Open GBV_Shield.sln. Restore NuGet packages when prompted.
4. Set GBV_Shield as the startup project, choose Debug and an Android device/emulator.

The ZIP includes the MAUI app, backend API, regression checks, Firebase rules, infrastructure and resources. Build outputs, IDE caches, local user settings, logs and the private Android signing keystore are excluded. Keep the original keystore backed up securely; it is needed for release updates signed with that key. Debug builds do not require it.

The newest AI chat source is included but not activated: server credentials, a model and a deployed backend URL are still required. See backend/GBVShield.Api/AI-CHAT.md. Azure billing/grant activation is still pending Microsoft support. iOS builds require an appropriately configured Mac.

This is a source archive, not an APK installer. Firebase client configuration remains included so the project retains its existing application identity; do not treat database rules or client configuration as server credentials.
