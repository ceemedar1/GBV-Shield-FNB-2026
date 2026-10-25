# GBV Shield — FNB App of the Year 2026

GBV Shield is a .NET MAUI safety app developed for Resiliency Care Centre. The Android and Huawei variants include SOS alerts, live-location sharing, nearby help, stories, and an in-app AI chat. This repository is a source review snapshot for the hackathon judges. The submitted pitch deck documents owner-reported device tests and current release limits.

## Source layout

- `Features/`, `Services/`, `Models/`: shared MAUI user interface and app logic.
- `Platforms/Android`, `Platforms/iOS`: platform-specific integrations.
- `backend/GBVShield.Api`: optional API and tracking backend.
- `public/`: Firebase-hosted AI chat and privacy pages.
- `database.rules.json`, `stories.database.rules.json`: database access rules.

## Build setup

Install the .NET 10 SDK and MAUI Android workload. Replace the placeholder Firebase and reCAPTCHA identifiers in `Constants.cs`, `Config/AppConfig.cs`, and `public/ai-chat.html` with your own project values. Add your own `Platforms/Android/google-services.json` and `Platforms/iOS/GoogleService-Info.plist`; these are deliberately excluded. Set server secrets through environment variables or a secret store.

For Android: `dotnet build GBV_Shield.csproj -f net10.0-android`. For Huawei: add `-p:HuaweiBuild=true`. iOS packaging requires a Mac. The source snapshot is provided for review; a configured build and physical-device acceptance test are required before production deployment.

## Safety and privacy

Never use real emergency contacts during a demo. The app can transmit SMS and location links. The Firebase projects and API used by production are not included as working credentials here. The app must not be presented as a substitute for emergency services. See `public/privacy.html` for the deployed privacy notice.

Copyright (c) 2026 Resiliency Care Centre. All rights reserved. No reuse licence is granted by this public review snapshot.
