using System.Text;
using System.Text.Json;

namespace HealthCheckAI.Services
{
    public class GeminiService
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public GeminiService(IConfiguration configuration)
        {
            // 從 appsettings.json 讀取 API Key
            _apiKey = configuration["GeminiApiKey"] ?? "";
            _httpClient = new HttpClient();
        }

        public async Task<string> GenerateHealthSummaryAsync(string department, string extractedText)
        {
            if (string.IsNullOrEmpty(_apiKey)) return "【系統錯誤：尚未設定 Gemini API Key】";

            // 這裡就是設計 AI的地方（Prompt Engineering）
            string prompt = $@"
        你是一位專業的健管中心醫師。
        請針對以下【{department}】的健檢原始文字數據，進行精準的客觀分析與具體的健康建議。

        【⚠️ 最高指導原則：防捏造與絕對客觀】
        - 必須完全依據提供的數據，不可憑空捏造。
        - 「內容摘要」必須是 100% 客觀的事實與數值陳述，只說明「什麼正常、什麼異常、代表什麼生理意義（如：過重、血壓偏高）」。
        - 嚴格禁止：在「內容摘要」中絕對不准出現「親愛的醫師您好」等問候語，也絕對禁止出現「提醒」、「關注」、「建議」等任何帶有指導意味的字眼！

        【⚠️ 強制輸出格式】
        請務必「一字不差」遵守以下格式，絕對不要使用 Markdown 符號 (如 **粗體**)：

        需追蹤程度：(高、中、或 低)

        內容摘要：
        (約 50~100 字。請平鋪直敘地報告客觀結果。範例語氣：「本次檢查中，身高與脈搏在正常範圍。但體重、BMI及腹圍高於標準，顯示有體重過重及腹部脂肪偏高之情形。此外，血壓值為...」)

        健康建議：
        1. (針對異常項目的具體改善建議一)
        2. (具體改善建議二)
        3. (具體改善建議三)

        以下是健檢報告的原始文字：
        {extractedText}";

            // 封裝成 Google API 要求的 JSON 格式
            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                }
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);

            // 修正 1：強制清除 API Key 頭尾可能不小心複製到的空白或換行符號
            string safeApiKey = _apiKey.Trim();
            // 修正 2：換成 Google 模型網址
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={safeApiKey}";

            try
            {
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    // 🛡️ 修正 3：把 Google 傳回來的「真正死因 (JSON)」完整印出來！
                    string errorDetails = await response.Content.ReadAsStringAsync();
                    return $"【AI 呼叫失敗，錯誤碼：{response.StatusCode}】\n詳細原因：{errorDetails}";
                }

                string responseString = await response.Content.ReadAsStringAsync();

                // 解析 Google 回傳的複雜 JSON，直接挖出 AI 寫的文字
                using var doc = JsonDocument.Parse(responseString);
                string aiResponseText = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString() ?? "AI 未回傳任何文字";

                return aiResponseText;
            }
            catch (Exception ex)
            {
                return $"【連線至 AI 發生異常】：{ex.Message}";
            }
        }
    }
}