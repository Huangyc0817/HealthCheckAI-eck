using System.Text.RegularExpressions;

namespace HealthCheckAI.Helpers
{
    public static class UltrasoundTextFormatter
    {
        public static string Format(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            text = text.Replace("\r\n", " ")
                       .Replace("\r", " ")
                       .Replace("\n", " ")
                       .Replace('\u00A0', ' ')
                       .Trim();

            text = Regex.Replace(text, @"\s+", " ");

            text = text.Replace("精密儀器檢查 （Precision instrument inspection）", "")
                       .Replace("精密儀器檢查 (Precision instrument inspection)", "")
                       .Trim();

            string[] titles =
            {
                "頸動脈超音波檢查",
                "甲狀腺超音波檢查",
                "腹部超音波",
                "心臟超音波檢查",
                "乳房超音波檢查",
                "婦科超音波檢查",
                "肝纖維化掃描"
            };
            // ✅ 標題
            foreach (var title in titles)
            {
                text = Regex.Replace(text, title, $"\n\n【{title}】\n");
            }

            // ✅ 固定欄位
            text = Regex.Replace(text, @"診斷\s*[（(]Diagnosis[）)]", "\n診斷（Diagnosis）\n");
            text = Regex.Replace(text, @"建議\s*[（(]Suggestion[）)]", "\n建議（Suggestion）\n");

            // ✅ 自動切項目（通用）
            text = Regex.Replace(text,
                @"(?<!\n)([\u4e00-\u9fff]{2,}(囊腫|結節|脂肪肝|異常|閉鎖不全|副脾|纖維化))",
                "\n$1");

            // ✅ 數值優化
            text = Regex.Replace(text, @"、", " / ");

            // ✅ 清理
            text = Regex.Replace(text, @"\n{3,}", "\n\n");

            // 只保留最多一個空白行，不要連續很多行
            text = Regex.Replace(text, @"\n{3,}", "\n\n");

            return text.Trim();
        }
    }
}