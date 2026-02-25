using System.Linq;
using System.Collections.Generic;
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

        public static string RebuildPhysicalExamLines(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            var lines = text
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            var rebuilt = new List<string>();
            string buffer = "";

            foreach (var line in lines)
            {
                if (Regex.IsMatch(line, @"\(.+?\)"))
                {
                    if (!string.IsNullOrWhiteSpace(buffer))
                        rebuilt.Add(buffer.Trim());

                    buffer = line;
                }
                else
                {
                    buffer += " " + line;
                }
            }

            if (!string.IsNullOrWhiteSpace(buffer))
                rebuilt.Add(buffer.Trim());

            return string.Join("\n", rebuilt);
        }

        public static string FormatAiSummary(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            // 統一換行
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

            // 修正 OCR 常見：小數點被拆開（156. 6 -> 156.6）
            text = Regex.Replace(text, @"(\d)\.\s+(\d)", "$1.$2");

            // 修正 OCR 常見：百分比被拆開（50. 0% -> 50.0%）
            text = Regex.Replace(text, @"(\d)\.\s+(\d)\s*%", "$1.$2%");

            // 讓幾個區塊固定換行（不要壓縮成一行）
            string[] heads = { "AI 綜合分析結果", "來賓：", "科別：", "需追蹤程度：", "內容摘要：", "重點整理：", "健康建議：" };
            foreach (var h in heads)
                text = text.Replace(h, "\n" + h);

            // 條列換行：1. 2. 3.
            text = Regex.Replace(text, @"\n?\s*(\d+)\.\s*", "\n$1. ");

            // 避免過多空行
            text = Regex.Replace(text, @"\n{3,}", "\n\n");

            return text.Trim();
        }


    }
}
