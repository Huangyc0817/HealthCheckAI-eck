using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HealthCheckAI.Models;

namespace HealthCheckAI.Helpers
{
    public static class PhysicalExamParser
    {
        private static readonly string[] KnownItems =
        {
            "身高",
            "體重",
            "理想體重範圍公式",
            "體質量指數",
            "腹圍",
            "脈搏",
            "血壓",
            "音叉"
        };

        public static List<PhysicalExamRow> Parse(string text)
        {
            var rows = new List<PhysicalExamRow>();
            if (string.IsNullOrWhiteSpace(text)) return rows;

            var lines = NormalizeAndMergeLines(text);

            foreach (var line in lines)
            {
                if (ShouldSkip(line))
                    continue;

                if (line.Contains("項目") && line.Contains("結果") && line.Contains("參考值"))
                    continue;

                if (line.StartsWith("理想體重範圍公式"))
                {
                    rows.Add(new PhysicalExamRow
                    {
                        Item = "理想體重範圍公式",
                        Result = ExtractAfterColon(line),
                        Previous = "",
                        Reference = "",
                        IsSection = false
                    });
                    continue;
                }

                var tabParts = line.Split('\t')
                                   .Select(x => x.Trim())
                                   .Where(x => !string.IsNullOrWhiteSpace(x))
                                   .ToList();

                if (tabParts.Count >= 3)
                {
                    rows.Add(new PhysicalExamRow
                    {
                        Item = tabParts[0],
                        Result = tabParts[1],
                        Previous = "",
                        Reference = string.Join(" ", tabParts.Skip(2)),
                        IsSection = false
                    });
                    continue;
                }

                var item = KnownItems.FirstOrDefault(k => line.StartsWith(k));
                if (string.IsNullOrWhiteSpace(item))
                    continue;

                var rest = line.Substring(item.Length).Trim();
                rest = NormalizeInline(rest);

                var (result, reference) = SplitResultAndReference(rest);
                (item, result) = SplitEnglishFromResult(item, result);
                rows.Add(new PhysicalExamRow
                {
                    Item = item,
                    Result = result,
                    Previous = "",
                    Reference = reference,
                    IsSection = false
                });
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

                if ((line.StartsWith("(") || line.StartsWith("（")) && merged.Count > 0)
                {
                    merged[^1] += " " + line;
                    continue;
                }

                if ((line.StartsWith("Body") || line.StartsWith("BMI") || line.StartsWith("Blood") ||
                     line.StartsWith("Pulse") || line.StartsWith("Abdominal")) && merged.Count > 0)
                {
                    merged[^1] += " " + line;
                    continue;
                }

                merged.Add(line);
            }

            return merged;
        }

        private static bool ShouldSkip(string line)
        {
            return line.Contains("Physical Examination")
                   || line.Contains("系統體格檢查表")
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

            s = s.Replace("( Body height )", "(Body height)")
                 .Replace("( Body weight )", "(Body weight)")
                 .Replace("( BMI )", "(BMI)")
                 .Replace("( Abdominal girth )", "(Abdominal girth)")
                 .Replace("( Pulse rate )", "(Pulse rate)")
                 .Replace("( Blood pressure )", "(Blood pressure)")
                 .Replace("( Body height)", "(Body height)")
                 .Replace("( Body weight)", "(Body weight)")
                 .Replace("( BMI)", "(BMI)")
                 .Replace("( Abdominal girth)", "(Abdominal girth)")
                 .Replace("( Pulse rate)", "(Pulse rate)")
                 .Replace("( Blood pressure)", "(Blood pressure)");

            return s;
        }

        private static string ExtractAfterColon(string line)
        {
            var idx = line.IndexOf('：');
            if (idx >= 0 && idx < line.Length - 1)
                return line[(idx + 1)..].Trim();

            idx = line.IndexOf(':');
            if (idx >= 0 && idx < line.Length - 1)
                return line[(idx + 1)..].Trim();

            return line.Replace("理想體重範圍公式", "").Trim();
        }


        private static (string Result, string Reference) SplitResultAndReference(string rest)
        {
            if (string.IsNullOrWhiteSpace(rest))
                return ("", "");

            var labelMatch = Regex.Match(rest, @"(國人.*?標準[:：]\s*.*)$");
            if (labelMatch.Success)
            {
                var reference = labelMatch.Groups[1].Value.Trim();
                var result = rest.Substring(0, labelMatch.Index).Trim();
                return (result, reference);
            }

            var bpMatch = Regex.Match(rest, @"(?<ref>\d+\s*-\s*\d+\s*/\s*\d+\s*-\s*\d+\s*mmHg)$", RegexOptions.IgnoreCase);
            if (bpMatch.Success)
            {
                var reference = bpMatch.Groups["ref"].Value.Trim();
                var result = rest.Substring(0, bpMatch.Index).Trim();
                return (result, reference);
            }

            var rangeMatch = Regex.Match(rest, @"(?<ref>\d+(\.\d+)?\s*(至|-)\s*\d+(\.\d+)?\s*[^ ]+.*)$");
            if (rangeMatch.Success)
            {
                var reference = rangeMatch.Groups["ref"].Value.Trim();
                var result = rest.Substring(0, rangeMatch.Index).Trim();
                return (result, reference);
            }
            return (rest.Trim(), "");
        }
        private static (string Item, string Result) SplitEnglishFromResult(string item, string result)
        {
            if (string.IsNullOrWhiteSpace(result))
                return (item, result);

            var match = Regex.Match(result, @"^\((.*?)\)\s*(.*)$");
            if (match.Success)
            {
                var eng = match.Groups[1].Value.Trim();
                var realResult = match.Groups[2].Value.Trim();

                item = $"{item} ({eng})";
                return (item, realResult);
            }

            return (item, result);
        }

    }
}