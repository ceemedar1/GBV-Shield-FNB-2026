using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GBV_Shield.Services
{
    public class FirebaseService
    {
        private readonly HttpClient _httpClient;

        public FirebaseService()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        }

        // 🔥 Send data
        public async Task<bool> SendDataAsync(string path, string json, string idToken, CancellationToken token = default)
        {
            try
            {
                var url = $"{Constants.FirebaseDatabaseUrl}/{path}.json?auth={idToken}";
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync(url, content, token);

                if (response.IsSuccessStatusCode) return true;
                System.Diagnostics.Debug.WriteLine(
                    $"[FirebaseService] Failed: {response.StatusCode} - {await response.Content.ReadAsStringAsync(token)}");
                return false;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return false; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FirebaseService] Write failed: {ex.Message}");
                return false;
            }
        }

        // 🔥 Get data
        public async Task<string?> GetDataAsync(string path, string idToken, CancellationToken token = default)
        {
            try
            {
                var url = $"{Constants.FirebaseDatabaseUrl}/{path}.json?auth={idToken}";
                var response = await _httpClient.GetAsync(url, token);
                return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync() : null;
            }
            catch { return null; }
        }
    }
}
