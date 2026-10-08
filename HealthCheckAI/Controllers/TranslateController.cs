using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System;

namespace HealthCheckAI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TranslateController : ControllerBase
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public TranslateController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClient = httpClientFactory.CreateClient();
            _apiKey = configuration["GeminiApiKey"]; // 請確保 appsettings.json 有這把 Key
        }

        // 💡 接收前端傳來的一整包陣列
        public class TranslateBatchRequest
        {
            public List<string> Texts { get; set; }
            public string Target { get; set; }
        }

        [HttpPost("batch")]
        public async Task<IActionResult> BatchTranslate([FromBody] TranslateBatchRequest request)
        {
            if (request.Texts == null || !request.Texts.Any())
                return BadRequest("沒有收到文字");

            try
            {
                // 100% 交給 Gemini 翻譯這整包陣列
                var translatedTexts = await CallGeminiBatchTranslate(request.Texts, request.Target);
                return Ok(new { translatedTexts = translatedTexts });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // 🚀 核心邏輯：要求 Gemini 翻譯整個陣列
        private async Task<List<string>> CallGeminiBatchTranslate(List<string> texts, string targetLanguage)
        {
            var targetLangName = targetLanguage == "en" ? "English" : targetLanguage;

            // 將要翻譯的文字清單轉成 JSON 格式
            string jsonInput = JsonSerializer.Serialize(texts);

            // 💡 嚴格的 Prompt 咒語：要求 AI 只能回傳乾淨的 JSON 陣列，順序不能變
            var prompt = $"You are a professional medical translator. Translate the following JSON array of strings into {targetLangName}.\n" +
                         "CRITICAL RULES:\n" +
                         "1. Return ONLY a valid JSON array of translated strings.\n" +
                         "2. Keep the exact same array length and order as the input.\n" +
                         "3. DO NOT wrap the output in markdown code blocks (e.g., no ```json).\n\n" +
                         $"Input:\n{jsonInput}";

            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new { temperature = 0.1 }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            // 💡 更新模型名稱
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                throw new Exception($"Gemini API 拒絕請求: {errorMsg}");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var jsonDocument = JsonDocument.Parse(responseString);

            string resultText = jsonDocument.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString()
                .Trim();

            // 💡 防呆機制：現在它乖乖待在 CallGeminiBatchTranslate 方法裡面了！
            if (resultText.StartsWith("```json")) resultText = resultText.Substring(7);
            if (resultText.StartsWith("```")) resultText = resultText.Substring(3);
            if (resultText.EndsWith("```")) resultText = resultText.Substring(0, resultText.Length - 3);
            resultText = resultText.Trim();

            // 💡 加入 Try-Catch 保護：萬一 Gemini 回傳的 JSON 格式爛掉，才不會讓網頁 500 崩潰
            try
            {
                var translatedList = JsonSerializer.Deserialize<List<string>>(resultText);
                return translatedList;
            }
            catch (JsonException ex)
            {
                // 如果反序列化失敗，把原本的文字直接傳回去，當作沒翻譯，保護系統不崩潰
                Console.WriteLine($"JSON 解析失敗: {ex.Message}. 原始回傳內容: {resultText}");
                return texts;
            }
        }
    }
}