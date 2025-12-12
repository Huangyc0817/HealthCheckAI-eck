using System.Text.RegularExpressions;

namespace HealthCheckAI.Helpers
{
    public static class TextFormatter
    {
        public static string FormatReportText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // 壓縮多重空白
            text = Regex.Replace(text, @"\s+", " ");

            // 句號＋中文句號後面換行
            text = Regex.Replace(text, @"。", "。\n");
            text = Regex.Replace(text, @"\.\s*", ".\n");

            // 條列 1. 2. 3. 換行
            text = Regex.Replace(text, @"(\d+)\.\s*", "\n$1. ");

            // 自動偵測標題並換行
            string[] sections =
            {
                "主訴與病史", "健康原因", "健檢原因",
                "系統體格檢查", "實驗室檢查", "精密儀器檢查",
                "診斷及建議", "Diagnosis and Suggestion",
                "體檢結果", "身體組成分析"
            };

            foreach (var s in sections)
            {
                text = text.Replace(s, $"\n\n{s}\n");
            }

            // 避免過多空行
            text = Regex.Replace(text, @"\n{3,}", "\n\n");

            return text.Trim();
        }
    }
}
