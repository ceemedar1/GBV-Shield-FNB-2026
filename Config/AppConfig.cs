namespace GBV_Shield.Config
{
    public static class AppConfig
    {
        // Set to deployed HTTPS backend /api/ after activation. Never put API keys here.
        public static string AiChatBaseUrl => "";
        public const string FirebaseAiChatUrl = "https://YOUR_HOST/ai-chat.html";
        public const string FirebaseAiProjectId = "YOUR_FIREBASE_AI_PROJECT_ID";
        // Stories use this isolated project; SOS and live location remain on the original project.
        public const string StoriesDatabaseUrl = "https://YOUR_STORIES_DATABASE.firebasedatabase.app";
        public const string FirebaseAiAppId = "YOUR_FIREBASE_ANDROID_APP_ID";
        public const string FirebaseAiApiKey = "YOUR_FIREBASE_AI_API_KEY";
        // Filled from the separately registered Huawei Android app in this Firebase project.
        // These are public client identifiers; do not place a Gemini server key here.
        public const string HuaweiAiAppId = "YOUR_FIREBASE_HUAWEI_APP_ID";
        public const string HuaweiAiApiKey = FirebaseAiApiKey;
        public const string HuaweiRecaptchaSiteKey = "YOUR_RECAPTCHA_SITE_KEY";
        // Keep Firebase active until the Azure endpoint passes the physical-device acceptance test.
        public const string LiveLocationProvider = "Firebase"; // Firebase | Azure

        // From the deployed Azure API overview. Must end in /api/.
        public const string ApiBaseUrl = "https://YOUR_API_HOST/api/";
    }
}
