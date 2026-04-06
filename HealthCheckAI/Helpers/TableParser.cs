using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HealthCheckAI.Helpers
{
    public class PhysicalExamRow
    {
        public string Item { get; set; } = "";
        public string Result { get; set; } = "";
        public string Previous { get; set; } = "";
        public string Reference { get; set; } = "";
        public bool IsSection { get; set; } = false;
    }

    public static class TableParser
    {
        public static List<PhysicalExamRow> ParsePhysicalExamTable(string text)
        {
            var rows = new List<PhysicalExamRow>();
            if (string.IsNullOrWhiteSpace(text)) return rows;

            var lines = text.Split('\n')
                            .Select(x => NormalizeLine(x))
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .ToList();

            foreach (var line in lines)
            {
                // 跳過表頭
                if (line.Contains("項目") && (line.Contains("結果") || line.Contains("本次")) && line.Contains("參考值"))
                    continue;

                // 跳過明顯標題
                if (line.Contains("Physical Examination") ||
                    line.Contains("系統體格檢查表") ||
                    line.Contains("理學檢查"))
                    continue;

                // 先嘗試 tab 分欄
                var parts = line.Split('\t')
                                .Select(x => x.Trim())
                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                .ToList();

                if (parts.Count >= 4)
                {
                    rows.Add(new PhysicalExamRow
                    {
                        Item = parts[0],
                        Result = parts[1],
                        Previous = parts[2],
                        Reference = string.Join(" ", parts.Skip(3))
                    });
                    continue;
                }

                if (parts.Count == 3)
                {
                    rows.Add(new PhysicalExamRow
                    {
                        Item = parts[0],
                        Result = parts[1],
                        Previous = "",
                        Reference = parts[2]
                    });
                    continue;
                }

                // fallback：用內容分析
                var parsed = ParseSegmentToRow(line);
                if (!string.IsNullOrWhiteSpace(parsed.Item))
                {
                    rows.Add(parsed);
                }
            }

            return rows;
        }

        private static string NormalizeLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return "";

            line = line.Replace("（", "(").Replace("）", ")");
            line = Regex.Replace(line, @"\s+", " ").Trim();

            // 修正常見英文括號被拆
            line = Regex.Replace(line, @"\(\s+", "(");
            line = Regex.Replace(line, @"\s+\)", ")");

            // 修正常見項目名被拆
            line = line.Replace("( Body height )", "(Body height)")
                       .Replace("( Body weight )", "(Body weight)")
                       .Replace("( Pulse rate )", "(Pulse rate)")
                       .Replace("( Blood pressure )", "(Blood pressure)")
                       .Replace("( Abdominal girth )", "(Abdominal girth)")
                       .Replace("( BMI )", "(BMI)")
                       .Replace("( Lymph node )", "(Lymph node)");

            return line;
        }

        private static PhysicalExamRow ParseSegmentToRow(string seg)
        {
            seg = NormalizeLine(seg);

            var itemMatch = Regex.Match(seg, @"^(?<item>[\u4e00-\u9fffA-Za-z0-9\(\)\-\s\/]+?)\s+(?<rest>.+)$");
            if (!itemMatch.Success) return new PhysicalExamRow();

            var item = itemMatch.Groups["item"].Value.Trim();
            var rest = itemMatch.Groups["rest"].Value.Trim();

            if (item.Contains("理想體重"))
            {
                return new PhysicalExamRow
                {
                    Item = item,
                    Result = rest,
                    Previous = "",
                    Reference = "-"
                };
            }

            string reference = "";
            string result = rest;

            var refLabel = Regex.Match(rest, @"(國人.*?標準[:：]\s*.*)$");
            if (refLabel.Success)
            {
                reference = refLabel.Groups[1].Value.Trim();
                result = rest.Substring(0, refLabel.Index).Trim();
                return new PhysicalExamRow
                {
                    Item = item,
                    Result = result,
                    Previous = "",
                    Reference = reference
                };
            }

            var range = Regex.Match(rest, @"(?<ref>\d+(\.\d+)?\s*(至|-)\s*\d+(\.\d+)?\s*[^ ]+.*)$");
            if (range.Success)
            {
                reference = range.Groups["ref"].Value.Trim();
                result = rest.Substring(0, range.Index).Trim();
                return new PhysicalExamRow
                {
                    Item = item,
                    Result = result,
                    Previous = "",
                    Reference = reference
                };
            }

            var bp = Regex.Match(rest, @"(?<ref>\d+\s*-\s*\d+\s*/\s*\d+\s*-\s*\d+\s*mmHg)$", RegexOptions.IgnoreCase);
            if (bp.Success)
            {
                reference = bp.Groups["ref"].Value.Trim();
                result = rest.Substring(0, bp.Index).Trim();
                return new PhysicalExamRow
                {
                    Item = item,
                    Result = result,
                    Previous = "",
                    Reference = reference
                };
            }

            var simpleRange = Regex.Match(rest, @"(?<ref>\d+(\.\d+)?\s*-\s*\d+(\.\d+)?\s*[^ ]+)$");
            if (simpleRange.Success)
            {
                reference = simpleRange.Groups["ref"].Value.Trim();
                result = rest.Substring(0, simpleRange.Index).Trim();
                return new PhysicalExamRow
                {
                    Item = item,
                    Result = result,
                    Previous = "",
                    Reference = reference
                };
            }

            return new PhysicalExamRow
            {
                Item = item,
                Result = result,
                Previous = "",
                Reference = "-"
            };
        }
    }
}