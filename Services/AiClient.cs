using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace GBV_Shield.Services
{
    public class AiClient
    {
        private readonly HttpClient _httpClient;

        public AiClient()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://YOUR_API_HOST/")
            };
        }

        public async Task<string> AskAsync(string message, string? city = null, string? country = "South Africa")
        {
            try
            {
                var request = new ChatRequest
                {
                    Message = message,
                    City = city,
                    Country = country
                };

                var response = await _httpClient.PostAsJsonAsync("api/ai/chat", request);

                if (!response.IsSuccessStatusCode)
                    return $"AI request failed: {response.StatusCode}";

                var result = await response.Content.ReadFromJsonAsync<ChatResponse>();

                return result?.Reply ?? "No response from AI.";
            }
            catch (Exception ex)
            {
                return $"Error contacting AI: {ex.Message}";
            }
        }

        private class ChatRequest
        {
            public string Message { get; set; } = string.Empty;
            public string? City { get; set; }
            public string? Country { get; set; }
        }

        private class ChatResponse
        {
            public string Reply { get; set; } = string.Empty;
        }
    }
}