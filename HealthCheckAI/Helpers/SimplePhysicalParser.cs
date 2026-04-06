using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HealthCheckAI.Helpers
{
    public static class SimplePhysicalParser
    {
        public static List<PhysicalExamRow> Parse(string text)
        {
            var rows = new List<PhysicalExamRow>();
            if (string.IsNullOrWhiteSpace(text)) return rows;

            var lines = NormalizeAndMergeLines(text);

            foreach (var line in lines)
            {
                if (ShouldSkip(line))
                    continue;

                // 跳過表頭
                if (line == "項目" || line == "結果" || line == "參考值")
                    continue;

                if ((line.Contains("項目") && line.Contains("結果")) ||
                    line == "項目 結果" ||
                    line == "項目 結果 參考值")
                    continue;

                var normalizedLine = NormalizeInline(line);

                var tabParts = normalizedLine.Split('\t')
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                if (tabParts.Count >= 2)
                {
                    var item = CleanupItem(tabParts[0]);
                    var result = CleanupResult(tabParts[1]);

                    if (IsHeaderLike(item, result))
                        continue;

                    rows.Add(new PhysicalExamRow
                    {
                        Item = item,
                        Result = result,
                        Previous = "",
                        Reference = "",
                        IsSection = false
                    });
                    continue;
                }

                var m = Regex.Match(
                    normalizedLine,
                    @"^(?<item>.+?)\s+(?<result>無明顯異常|正常|異常|未見異常.*|略有異常.*|無異常.*)$"
                );

                if (m.Success)
                {
                    var item = CleanupItem(m.Groups["item"].Value);
                    var result = CleanupResult(m.Groups["result"].Value);

                    if (IsHeaderLike(item, result))
                        continue;

                    rows.Add(new PhysicalExamRow
                    {
                        Item = item,
                        Result = result,
                        Previous = "",
                        Reference = "",
                        IsSection = false
                    });
                }
            }

            return rows;
        }

        private static List<string> NormalizeAndMergeLines(string text)
        {
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

            var rawLines = text.Split('\n')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            var merged = new List<string>();

            foreach (var raw in rawLines)
            {
                var line = NormalizeInline(raw);

                if (Regex.IsMatch(line, @"^-\d+-$")) continue;
                if (Regex.IsMatch(line, @"^\d{8,}$")) continue;

                if (merged.Count > 0)
                {
                    // 1. 括號開頭接回上一行
                    if (line.StartsWith("(") || line.StartsWith("（"))
                    {
                        merged[^1] += " " + line;
                        continue;
                    }

                    // 2. 英文括號中間段接回上一行
                    if (Regex.IsMatch(line, @"^(Head|Eye|Neck|Thyroid|Lymph|Chest|Heart|Lung|Abdomen|Back|Extremities|Peripheral|Skin|Others)\b", RegexOptions.IgnoreCase))
                    {
                        merged[^1] += " " + line;
                        continue;
                    }

                    // 3. 英文殘尾接回上一行：node) / vessels) / rate) 這種
                    if (Regex.IsMatch(line, @"^[A-Za-z][A-Za-z\s\-]*\)\s*(無明顯異常|正常|異常|未見異常.*|略有異常.*)?$", RegexOptions.IgnoreCase))
                    {
                        merged[^1] += " " + line;
                        continue;
                    }

                    // 4. 純英文殘片也接回上一行
                    if (Regex.IsMatch(line, @"^[A-Za-z][A-Za-z\s\-]*\)?$", RegexOptions.IgnoreCase))
                    {
                        merged[^1] += " " + line;
                        continue;
                    }
                }

                merged.Add(line);
            }

            return merged;
        }

        private static bool ShouldSkip(string line)
        {
            return line.Contains("理學檢查")
                   || line.Contains("Physical Examination")
                   || line.Contains("內容摘要")
                   || line.Contains("規則判讀")
                   || line.Contains("檢查內容");
        }

        private static string NormalizeInline(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";

            s = s.Replace("（", "(").Replace("）", ")");
            s = Regex.Replace(s, @"\s+", " ").Trim();
            s = Regex.Replace(s, @"\(\s+", "(");
            s = Regex.Replace(s, @"\s+\)", ")");

            // 常見英文括號名稱修正
            s = s.Replace("( Head )", "(Head)")
                 .Replace("( Eye )", "(Eye)")
                 .Replace("( Neck )", "(Neck)")
                 .Replace("( Thyroid )", "(Thyroid)")
                 .Replace("( Lymph node )", "(Lymph node)")
                 .Replace("( Chest )", "(Chest)")
                 .Replace("( Heart )", "(Heart)")
                 .Replace("( Lung )", "(Lung)")
                 .Replace("( Abdomen )", "(Abdomen)")
                 .Replace("( Back )", "(Back)")
                 .Replace("( Extremities )", "(Extremities)")
                 .Replace("( Peripheral vessels )", "(Peripheral vessels)")
                 .Replace("( Skin )", "(Skin)")
                 .Replace("( Others )", "(Others)")
                 .Replace("( Head)", "(Head)")
                 .Replace("( Eye)", "(Eye)")
                 .Replace("( Neck)", "(Neck)")
                 .Replace("( Thyroid)", "(Thyroid)")
                 .Replace("( Lymph node)", "(Lymph node)")
                 .Replace("( Chest)", "(Chest)")
                 .Replace("( Heart)", "(Heart)")
                 .Replace("( Lung)", "(Lung)")
                 .Replace("( Abdomen)", "(Abdomen)")
                 .Replace("( Back)", "(Back)")
                 .Replace("( Extremities)", "(Extremities)")
                 .Replace("( Peripheral vessels)", "(Peripheral vessels)")
                 .Replace("( Skin)", "(Skin)")
                 .Replace("( Others)", "(Others)");

            // 被切碎的常見項目直接補回
            s = s.Replace("淋巴結(Lymph node)", "淋巴結(Lymph node)")
                 .Replace("週邊血管(Peripheral vessels)", "週邊血管(Peripheral vessels)");

            return s;
        }

        private static string CleanupItem(string item)
        {
            if (string.IsNullOrWhiteSpace(item)) return "";

            item = NormalizeInline(item);

            item = item.Replace("淋巴結(Lymph node", "淋巴結(Lymph node)")
                       .Replace("週邊血管(Peripheral vessels", "週邊血管(Peripheral vessels)")
                       .Trim();

            // 如果最後缺右括號，補上
            var leftCount = item.Count(c => c == '(');
            var rightCount = item.Count(c => c == ')');
            if (leftCount > rightCount)
                item += ")";

            return item;
        }

        private static string CleanupResult(string result)
        {
            if (string.IsNullOrWhiteSpace(result)) return "";
            result = NormalizeInline(result);
            result = result.Replace("node) 無明顯異常", "無明顯異常")
                           .Replace("vessels) 無明顯異常", "無明顯異常")
                           .Trim();
            return result;
        }

        private static bool IsHeaderLike(string item, string result)
        {
            return (item == "項目" && result == "結果")
                || item == "項目"
                || result == "結果";
        }
    }
}