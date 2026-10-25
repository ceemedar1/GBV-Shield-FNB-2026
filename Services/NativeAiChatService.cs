using GBV_Shield.Config;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#if ANDROID
using Firebase;
using Firebase.AppCheck;
#if HUAWEI_BUILD
using Com.Google.Firebase.Appcheck.Recaptcha;
#else
using Firebase.AppCheck.PlayIntegrity;
#endif
#endif

namespace GBV_Shield.Services;

public sealed class NativeAiChatService
{
    public static bool IsSupported =>
#if ANDROID && HUAWEI_BUILD
        !string.IsNullOrWhiteSpace(AppConfig.HuaweiAiAppId) &&
        !string.IsNullOrWhiteSpace(AppConfig.HuaweiAiApiKey) &&
        !string.IsNullOrWhiteSpace(AppConfig.HuaweiRecaptchaSiteKey);
#elif ANDROID
        true;
#else
        false;
#endif
    private const string SafetyInstruction = "You are GBV Shield AI support, not a human counsellor or emergency service. Offer calm, brief, non-judgmental support for survivors of abuse in South Africa. Keep replies under 80 words unless immediate safety needs more. Never blame or pressure someone to report, leave, or confront an abuser. For immediate danger, suggest SOS, local emergency services, or a trusted nearby person if safe. Never imply anyone has been contacted. Never invent phone numbers, addresses, shelter availability, or legal facts. Do not ask for names, IDs, exact locations, or graphic details. Do not promise confidentiality. Do not diagnose or replace medical, legal, or professional care. Ask at most one question.";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly List<(string Role, string Text)> _history = new();

    public void Clear() => _history.Clear();

    public async Task<string> SendAsync(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Message is empty.");
        if (_history.Count >= 40) throw new InvalidOperationException("Session limit reached. Clear the chat to begin again.");
#if ANDROID
        if (!IsSupported) throw new NotSupportedException("Native AI chat is not configured for this platform.");
        var timer = Stopwatch.StartNew();
        var appCheckToken = await GetAppCheckTokenAsync(cancellationToken);
        var attestationMs = timer.ElapsedMilliseconds;
        // Keep enough recent context for continuity without making every turn progressively slower.
        var contents = _history.TakeLast(6).Select(item => new { role = item.Role, parts = new[] { new { text = item.Text } } }).ToList();
        contents.Add(new { role = "user", parts = new[] { new { text = message.Trim() } } });
        var body = JsonSerializer.Serialize(new
        {
            contents,
            systemInstruction = new { parts = new[] { new { text = SafetyInstruction } } },
            generationConfig = new
            {
                maxOutputTokens = 384,
                thinkingConfig = new { thinkingLevel = "low" }
            }
        });
        var url = $"https://firebasevertexai.googleapis.com/v1beta/projects/{AppConfig.FirebaseAiProjectId}/models/gemini-3.5-flash:generateContent";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-goog-api-key", AiApiKey);
        request.Headers.Add("X-Firebase-AppCheck", appCheckToken);
        request.Headers.Add("X-Firebase-Appid", AiAppId);
        using var response = await Client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        Debug.WriteLine($"[NativeAI Timing] appCheck={attestationMs}ms networkAndModel={timer.ElapsedMilliseconds - attestationMs}ms status={(int)response.StatusCode}");
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"AI request failed ({(int)response.StatusCode}).");
        }
        using var json = JsonDocument.Parse(payload);
        var reply = json.RootElement.GetProperty("candidates")[0].GetProperty("content")
            .GetProperty("parts").EnumerateArray()
            .Select(part => part.TryGetProperty("text", out var value) ? value.GetString() : null)
            .Where(value => !string.IsNullOrWhiteSpace(value));
        var answer = string.Join("\n", reply).Trim();
        if (answer.Length == 0) throw new InvalidOperationException("AI returned no readable answer.");
        _history.Add(("user", message.Trim()));
        _history.Add(("model", answer));
        return answer.Length > 4000 ? answer[..4000] : answer;
#else
        throw new NotSupportedException("Native AI chat is not configured for this platform.");
#endif
    }

#if ANDROID
    private static string AiAppId =>
#if HUAWEI_BUILD
        AppConfig.HuaweiAiAppId;
#else
        AppConfig.FirebaseAiAppId;
#endif
    private static string AiApiKey =>
#if HUAWEI_BUILD
        AppConfig.HuaweiAiApiKey;
#else
        AppConfig.FirebaseAiApiKey;
#endif
    private static FirebaseApp? _aiApp;
    private static FirebaseAppCheck? _appCheck;
    private static readonly object AppLock = new();

    private static Task<string> GetAppCheckTokenAsync(CancellationToken cancellationToken) => Task.Run(() =>
    {
        FirebaseAppCheck appCheck;
        lock (AppLock)
        {
            if (_appCheck is null)
            {
                var options = new FirebaseOptions.Builder()
                    .SetApplicationId(AiAppId)
                    .SetApiKey(AiApiKey)
                    .SetProjectId(AppConfig.FirebaseAiProjectId)
                    .SetGcmSenderId("YOUR_GCM_SENDER_ID")
                    .Build();
                _aiApp = FirebaseApp.InitializeApp(Android.App.Application.Context, options,
#if HUAWEI_BUILD
                    "gbv-shield-ai-huawei");
#else
                    "gbv-shield-ai");
#endif
                var app = _aiApp ?? throw new InvalidOperationException("AI Firebase app could not initialize.");
                var configuredAppCheck = FirebaseAppCheck.GetInstance(app);
                configuredAppCheck.InstallAppCheckProviderFactory(
#if HUAWEI_BUILD
                    RecaptchaAppCheckProviderFactory.GetInstance(AppConfig.HuaweiRecaptchaSiteKey));
#else
                    PlayIntegrityAppCheckProviderFactory.Instance);
#endif
                _appCheck = configuredAppCheck;
            }
            appCheck = _appCheck;
        }
        var result = Android.Gms.Tasks.TasksClass.Await(appCheck.GetAppCheckToken(false));
        cancellationToken.ThrowIfCancellationRequested();
        return ((AppCheckToken)result!).Token;
    }, cancellationToken);
#endif
}
