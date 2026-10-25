# GBV Shield Azure live tracking API

This API creates two independent 256-bit capability tokens per SOS session: a viewer token placed in the helper link and a write token held only by the victim's device. Sessions expire after two hours and public responses never expose an account ID.

## Local run

1. Start Azurite on ports 10000–10002.
2. Set `Firebase__ApiKey` and run `dotnet run` from this folder.
3. Open `/health` to confirm readiness.

Production settings are supplied as secrets/environment variables:

- `Firebase__ApiKey`
- `Storage__ConnectionString`
- `Storage__TableName=LiveLocations`
- `Tracking__PublicBaseUrl=https://YOUR-HOST/track.html`

Never commit production connection strings. Restrict the storage account to HTTPS, disable public blob access, enable soft delete, and rotate keys after operator access.
