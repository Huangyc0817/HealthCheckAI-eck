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

            // 統一換行
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

            // 只壓縮空白，不壓掉換行
            text = Regex.Replace(text, @"[ \t]+", " ");

            // 修正常見 OCR 小數點斷開：5. 36 -> 5.36
            text = Regex.Replace(text, @"(\d)\.\s+(\d)", "$1.$2");

            // 修正範圍值被拆開：4. 5- 5. 9 -> 4.5-5.9
            text = Regex.Replace(text, @"(\d)\.\s+(\d)\s*-\s*(\d)\.\s+(\d)", "$1.$2-$3.$4");

            // 百分比斷開：50. 0 % -> 50.0%
            text = Regex.Replace(text, @"(\d)\.\s+(\d)\s*%", "$1.$2%");

            // 常見標題前後補換行
            string[] sections =
            {
                "主訴與病史", "健康原因", "健檢原因",
                "系統體格檢查", "實驗室檢查", "Laboratory Examination",
                "精密儀器檢查", "診斷及建議", "Diagnosis and Suggestion",
                "體檢結果", "身體組成分析"
            };

            foreach (var s in sections.Distinct())
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

            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            text = Regex.Replace(text, @"[ \t]+", " ");
            text = Regex.Replace(text, @"(\d)\.\s+(\d)", "$1.$2");
            text = Regex.Replace(text, @"(\d)\.\s+(\d)\s*-\s*(\d)\.\s+(\d)", "$1.$2-$3.$4");
            text = Regex.Replace(text, @"(\d)\.\s+(\d)\s*%", "$1.$2%");

            // 表頭
            text = text.Replace("檢查項目 本次 前次 本次參考值", "\n檢查項目\t本次\t前次\t本次參考值\n");

            // 分類列
            string[] categories =
            {
        "血液檢查", "生化檢查", "肝功能檢查", "腎功能檢查", "血脂肪檢查",
        "糖尿病檢查", "痛風檢查", "胰臟功能檢查", "心臟血管功能檢查",
        "甲狀腺檢查", "風濕免疫學檢查", "肝炎標記", "血液腫瘤標誌",
        "其它檢查", "尿液檢查"
    };

            foreach (var c in categories)
            {
                text = Regex.Replace(
                    text,
                    $@"\s*({Regex.Escape(c)}\s*\([^)]+\))\s*",
                    "\n[$1]\n"
                );
            }

            // 把黏在一起的「下一個檢驗項目(英文縮寫)」切開
            text = Regex.Replace(
                text,
                @"\s+(?=[\u4e00-\u9fffA-Za-z][^\n]{0,40}?\([^)]+\)\s)",
                "\n"
            );

            var lines = text
                .Split('\n')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            // 把被拆成兩行的資料列接回去
            // 例如：
            // 平均紅血球色素量(MCH)
            // 29.5 --- 26-34 pg
            // => 平均紅血球色素量(MCH) 29.5 --- 26-34 pg
            var mergedLines = new List<string>();

            foreach (var line in lines)
            {
                bool looksLikeValueOnly = Regex.IsMatch(
                    line,
                    @"^(<?\s*[\d\.]+(?:\([^)]+\))?|---|Negative|Positive|Non-reactive|-\s*|≦\s*[\d\.]+)\s+.*$",
                    RegexOptions.IgnoreCase
                );

                bool prevLooksLikeItem = mergedLines.Count > 0 &&
                    Regex.IsMatch(mergedLines.Last(), @"\([^)]+\)$");

                if (looksLikeValueOnly && prevLooksLikeItem)
                {
                    mergedLines[mergedLines.Count - 1] += " " + line;
                }
                else
                {
                    mergedLines.Add(line);
                }
            }

            lines = mergedLines;

            var result = new List<string>();
            bool headerAdded = false;

            foreach (var line in lines)
            {
                if (line.Contains("檢查項目") && line.Contains("本次") && line.Contains("前次") && line.Contains("本次參考值"))
                {
                    if (!headerAdded)
                    {
                        result.Add("檢查項目\t本次\t前次\t本次參考值");
                        headerAdded = true;
                    }
                    continue;
                }

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    result.Add(line.Trim('[', ']'));
                    continue;
                }

                // 解析單列：項目 / 本次 / 前次 / 參考值
                var m = Regex.Match(
                    line,
                    @"^(?<item>.+?)\s+(?<current><?\s*[^ ]+(?:\([^)]+\))?)\s+(?<prev>---|[^ ]+)\s+(?<ref>.+)$"
                );

                if (m.Success)
                {
                    result.Add(
                        $"{m.Groups["item"].Value.Trim()}\t" +
                        $"{m.Groups["current"].Value.Trim()}\t" +
                        $"{m.Groups["prev"].Value.Trim()}\t" +
                        $"{m.Groups["ref"].Value.Trim()}"
                    );
                }
                else
                {
                    result.Add(line);
                }
            }

            return string.Join("\n", result);
        }

        public static string FormatAiSummary(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            text = Regex.Replace(text, @"(\d)\.\s+(\d)", "$1.$2");
            text = Regex.Replace(text, @"(\d)\.\s+(\d)\s*%", "$1.$2%");

            string[] heads = { "AI 綜合分析結果", "來賓：", "科別：", "需追蹤程度：", "內容摘要：", "重點整理：", "健康建議：" };
            foreach (var h in heads)
                text = text.Replace(h, "\n" + h);

            text = Regex.Replace(text, @"\n?\s*(\d+)\.\s*", "\n$1. ");
            text = Regex.Replace(text, @"\n{3,}", "\n\n");

            return text.Trim();
        }
    }
}