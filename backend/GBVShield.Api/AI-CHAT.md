# AI chat activation

Implemented but not activated. No model/key or deployed chat URL has been configured.
The app shows this explicitly. Existing Firebase hotline history is not forwarded to AI.

Configure server-only environment variables (never commit secrets or put them in MAUI):

- `OpenAI__ApiKey`: dedicated API project credential
- `OpenAI__Model`: an available Responses-compatible text model, selected and evaluated before launch
- `Firebase__ApiKey`: the app's Firebase project API key, used for token validation

Deploy this backend behind HTTPS, then set `AppConfig.AiChatBaseUrl` to its verified `/api/` URL.
Do not assume an Azure grant pays OpenAI API charges. No paid resources were created.
Configure provider budget controls and operational monitoring before enabling public use.
The current shared limit is 20 requests/minute; it is not a monetary spending cap and is
per process. Add per-user/distributed quotas before scaling. Authentication uses Firebase
account lookup and rejects missing/invalid tokens. No Azure table dependency for chat.

Consent is required per screen session. History is memory-only, cleared on leaving, with
six previous turns sent for context. Clearing the screen does not erase provider logs.
`store:false` disables response application-state storage, not all provider retention.
Do not enable HTTP request/response body logging for this endpoint.

Verification: `dotnet run --project backend/ChatChecks/ChatChecks.csproj` uses a mocked
provider; it does not demonstrate live model access, safety quality, or a working deployment.
Before release test a signed-in phone, invalid tokens, no internet, provider quota/timeouts,
clear/leave during a pending response, account switches, and large text/font sizes.
Evaluate model responses with a GBV professional for imminent danger, coercive control,
self-harm, child safeguarding, legal/medical uncertainty, languages and prompt injection.
The agent offers conversation only, not dispatch, location tracking or a staffed hotline.

Reference: https://developers.openai.com/api/docs/guides/text
