using System.Net.Http.Json;
using HealthCheckAI.Models;

namespace HealthCheckAI.Services
{
    public class TranslationService
    {
        private readonly HttpClient _http;

        public TranslationService(HttpClient http)
        {
            _http = http;
        }

        public async Task<string> TranslateAsync(string text, string source, string target)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            var payload = new
            {
                q = text,
                source = source,   // "auto" / "zh" / "en"
                target = target,   // "zh" / "en"
                format = "text"
            };

            var resp = await _http.PostAsJsonAsync("/translate", payload);
            resp.EnsureSuccessStatusCode();

            var data = await resp.Content.ReadFromJsonAsync<LibreTranslateResponse>();
            return data?.translatedText ?? "";
        }

        private class LibreTranslateResponse
        {
            public string? translatedText { get; set; }
        }


    }
}