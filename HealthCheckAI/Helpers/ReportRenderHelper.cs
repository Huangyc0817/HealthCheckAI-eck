using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HealthCheckAI.Models;

namespace HealthCheckAI.Helpers
{
    public class ReportParts
    {
        public string BeforeText { get; set; } = "";
        public List<PhysicalExamRow> TableRows { get; set; } = new();
        public string TableRawText { get; set; } = "";
        public string KeyPointsText { get; set; } = "";
        public string SuggestionsText { get; set; } = "";
    }

    public static class ReportRenderHelper
    {
        public static ReportParts Split(string? reportContent)
        {
            var parts = new ReportParts();

            var content = (reportContent ?? "")
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Trim();

            if (string.IsNullOrWhiteSpace(content))
                return parts;

            string[] keys = new[]
            {
                "表（Physical Examination）",
                "Physical Examination",
                "表（Laboratory Examination）",
                "Laboratory Examination",
                "表（Imaging Examination）",
                "Imaging Examination",
                "檢查項目\t本次\t前次\t本次參考值",
                "檢查項目 本次 前次 本次參考值",
                "項目\t結果\t參考值",
                "項目 結果 參考值"
            };

            int idx = -1;
            foreach (var k in keys)
            {
                var t = content.IndexOf(k, StringComparison.Ordinal);
                if (t >= 0 && (idx == -1 || t < idx))
                    idx = t;
            }

            int kpIdx = content.IndexOf("重點整理：", StringComparison.Ordinal);
            int sugIdx = content.IndexOf("健康建議：", StringComparison.Ordinal);

            int cutAfter = -1;
            if (kpIdx >= 0 && sugIdx >= 0) cutAfter = Math.Min(kpIdx, sugIdx);
            else if (kpIdx >= 0) cutAfter = kpIdx;
            else if (sugIdx >= 0) cutAfter = sugIdx;

            string before = content;
            string tablePart = "";
            string afterAll = "";

            if (idx >= 0)
            {
                before = content.Substring(0, idx).Trim();

                if (cutAfter > idx)
                {
                    tablePart = content.Substring(idx, cutAfter - idx).Trim();
                    afterAll = content.Substring(cutAfter).Trim();
                }
                else
                {
                    tablePart = content.Substring(idx).Trim();
                    afterAll = "";
                }
            }
            else
            {
                int sumIdx = content.IndexOf("內容摘要", StringComparison.Ordinal);

                if (sumIdx >= 0)
                {
                    before = content.Substring(0, sumIdx).Trim();

                    if (cutAfter > sumIdx)
                    {
                        tablePart = content.Substring(sumIdx, cutAfter - sumIdx).Trim();
                        afterAll = content.Substring(cutAfter).Trim();
                    }
                    else
                    {
                        tablePart = content.Substring(sumIdx).Trim();
                        afterAll = "";
                    }
                }
                else
                {
                    before = (cutAfter > 0) ? content.Substring(0, cutAfter).Trim() : content.Trim();
                    afterAll = (cutAfter > 0) ? content.Substring(cutAfter).Trim() : "";
                    tablePart = "";
                }
            }

            string kp = "";
            string sug = "";

            if (!string.IsNullOrWhiteSpace(afterAll))
            {
                int k0 = afterAll.IndexOf("重點整理：", StringComparison.Ordinal);
                int s0 = afterAll.IndexOf("健康建議：", StringComparison.Ordinal);

                if (k0 >= 0 && s0 >= 0)
                {
                    if (k0 < s0)
                    {
                        kp = afterAll.Substring(k0 + "重點整理：".Length, s0 - (k0 + "重點整理：".Length)).Trim();
                        sug = afterAll.Substring(s0 + "健康建議：".Length).Trim();
                    }
                    else
                    {
                        sug = afterAll.Substring(s0 + "健康建議：".Length, k0 - (s0 + "健康建議：".Length)).Trim();
                        kp = afterAll.Substring(k0 + "重點整理：".Length).Trim();
                    }
                }
                else if (k0 >= 0)
                {
                    kp = afterAll.Substring(k0 + "重點整理：".Length).Trim();
                }
                else if (s0 >= 0)
                {
                    sug = afterAll.Substring(s0 + "健康建議：".Length).Trim();
                }
            }

            var rows = new List<PhysicalExamRow>();


            if (!string.IsNullOrWhiteSpace(tablePart))
            {
                if (tablePart.Contains("Physical Examination") && IsBodyCheckFormat(tablePart))
                {
                    rows = ParseBodyCheckTable(tablePart);
                }
                else if (tablePart.Contains("Physical Examination"))
                {
                    rows = TableParser.ParsePhysicalExamTable(tablePart);
                }
                else if (tablePart.Contains("Laboratory Examination"))
                {
                    rows = ParseLaboratoryTable(tablePart);
                }
                else if (tablePart.Contains("\t"))
                {
                    rows = ParseLaboratoryTable(tablePart);
                }
                else
                {
                    rows = TableParser.ParsePhysicalExamTable(tablePart);
                }
            }

            parts.BeforeText = before;
            parts.TableRows = rows;
            parts.TableRawText = tablePart;
            parts.KeyPointsText = kp;
            parts.SuggestionsText = sug;

            return parts;
        }

        // =========================
        // Physical Examination：3欄
        // =========================
        private static List<PhysicalExamRow> ParseBodyCheckTable(string text)
        {
            var rows = new List<PhysicalExamRow>();

            if (string.IsNullOrWhiteSpace(text))
                return rows;

            var normalized = text
                .Replace("（", "(")
                .Replace("）", ")")
                .Replace("\r\n", " ")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("\t", " ");

            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

            normalized = normalized
                .Replace("系統體格檢查", "")
                .Replace("表 (Physical Examination)", "")
                .Replace("表（Physical Examination）", "")
                .Replace("項目 結果 參考值", "")
                .Trim();

            var matches = Regex.Matches(
                normalized,
                @"(身高|體重|理想體重範圍公式|體質量指數|腹圍|脈搏|血壓)"
            );

            for (int i = 0; i < matches.Count; i++)
            {
                var item = matches[i].Value;
                int start = matches[i].Index + matches[i].Length;
                int end = (i < matches.Count - 1) ? matches[i + 1].Index : normalized.Length;

                var segment = normalized.Substring(start, end - start).Trim();
                segment = Regex.Replace(segment, @"^\(.*?\)\s*", "").Trim();

                string result = "--";
                string reference = "--";

                if (item == "身高")
                {
                    var m = Regex.Match(segment, @"^(\d+(\.\d+)?\s*公分)");
                    if (m.Success) result = m.Groups[1].Value.Trim();
                }
                else if (item == "體重")
                {
                    var m = Regex.Match(segment, @"^(\d+(\.\d+)?\s*公斤)\s*(.*)$");
                    if (m.Success)
                    {
                        result = m.Groups[1].Value.Trim();
                        reference = string.IsNullOrWhiteSpace(m.Groups[3].Value) ? "--" : m.Groups[3].Value.Trim();
                    }
                }
                else if (item == "理想體重範圍公式")
                {
                    segment = segment.TrimStart('：', ':').Trim();
                    result = segment;
                }
                else if (item == "體質量指數")
                {
                    var m = Regex.Match(segment, @"^(\d+(\.\d+)?)(.*)$");
                    if (m.Success)
                    {
                        result = m.Groups[1].Value.Trim();
                        reference = string.IsNullOrWhiteSpace(m.Groups[3].Value) ? "--" : m.Groups[3].Value.Trim();
                    }
                }
                else if (item == "腹圍")
                {
                    var m = Regex.Match(segment, @"^(\d+(\.\d+)?\s*公分)\s*(.*)$");
                    if (m.Success)
                    {
                        result = m.Groups[1].Value.Trim();
                        reference = string.IsNullOrWhiteSpace(m.Groups[3].Value) ? "--" : m.Groups[3].Value.Trim();
                    }
                }
                else if (item == "脈搏")
                {
                    var m = Regex.Match(segment, @"^(\d+(\.\d+)?\s*次/分鐘)\s*(.*)$");
                    if (m.Success)
                    {
                        result = m.Groups[1].Value.Trim();
                        reference = string.IsNullOrWhiteSpace(m.Groups[3].Value) ? "--" : m.Groups[3].Value.Trim();
                    }
                }
                else if (item == "血壓")
                {
                    var m = Regex.Match(segment, @"^(\d+/\d+\s*mmHg)\s*(.*)$");
                    if (m.Success)
                    {
                        result = m.Groups[1].Value.Trim();
                        reference = string.IsNullOrWhiteSpace(m.Groups[2].Value) ? "--" : m.Groups[2].Value.Trim();
                    }
                }

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

        // =========================
        // Laboratory Examination：4欄
        // =========================
        private static List<PhysicalExamRow> ParseLaboratoryTable(string tablePart)
        {
            var rows = new List<PhysicalExamRow>();

            var lines = tablePart
                .Split('\n')
                .Select(l => l.Replace("\r", "").Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            var mergedLines = new List<string>();

            foreach (var line in lines)
            {
                if (mergedLines.Count == 0)
                {
                    mergedLines.Add(line);
                    continue;
                }

                if (ShouldAppendToPrevious(line))
                {
                    mergedLines[^1] += " " + line;
                }
                else
                {
                    mergedLines.Add(line);
                }
            }

            string? pendingItem = null;

            foreach (var line in mergedLines)
            {
                if (IsHeaderLine(line))
                    continue;

                var cols = line.Split('\t')
                    .Select(x => CleanupText(x))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                if (cols.Count == 0)
                    continue;

                if (cols.Count == 1)
                {
                    var text = cols[0];

                    if (IsNoiseLine(text))
                        continue;

                    if (IsRealSection(text))
                    {
                        rows.Add(new PhysicalExamRow
                        {
                            Item = text,
                            Result = "",
                            Previous = "",
                            Reference = "",
                            IsSection = true
                        });
                    }
                    else
                    {
                        pendingItem = text;
                    }

                    continue;
                }

                string item = "";
                string result = "";
                string previous = "";
                string reference = "";

                if (cols.Count >= 4)
                {
                    item = cols[0];
                    result = cols[1];
                    previous = cols[2];
                    reference = cols[3];
                }
                else if (cols.Count == 3)
                {
                    if (!string.IsNullOrWhiteSpace(pendingItem))
                    {
                        item = pendingItem;
                        result = cols[0];
                        previous = cols[1];
                        reference = cols[2];
                    }
                    else
                    {
                        item = cols[0];
                        result = cols[1];

                        if (cols[2].StartsWith("---"))
                        {
                            previous = "---";
                            reference = cols[2].Replace("---", "").Trim();
                        }
                        else
                        {
                            previous = "";
                            reference = cols[2];
                        }
                    }
                }
                else if (cols.Count == 2)
                {
                    if (!string.IsNullOrWhiteSpace(pendingItem))
                    {
                        item = pendingItem;
                        result = cols[0];
                        previous = "";
                        reference = cols[1];
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    continue;
                }

                item = CleanupText(item);
                result = CleanupText(result);
                previous = CleanupText(previous);
                reference = CleanupText(reference);

                if (string.IsNullOrWhiteSpace(item))
                    continue;

                SplitCombinedSectionAndItem(ref item, rows);
                reference = NormalizeReference(reference);

                rows.Add(new PhysicalExamRow
                {
                    Item = item,
                    Result = result,
                    Previous = previous,
                    Reference = reference,
                    IsSection = false
                });

                pendingItem = null;
            }

            return rows;
        }

        private static bool ShouldAppendToPrevious(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            var text = line.Trim();

            if (IsUnitOnlyLine(text))
                return true;

            if (IsNoiseLine(text))
                return true;

            if (!text.Contains('\t'))
            {
                if (Regex.IsMatch(text, @"^[A-Za-z\)\(]+$"))
                    return true;

                if (Regex.IsMatch(text, @"^(IgG\)|IgM\)|T4\)|AC\)|TG\)|Color\)|protein\)|Bilirubin\)|Cell\)|Typing\)|Test\))$", RegexOptions.IgnoreCase))
                    return true;

                if (Regex.IsMatch(text, @"^(Reactive.*|Nonreactive.*|Non-reactive.*|Positive.*|Negative.*|NOT FOUND.*|FOUND.*)$", RegexOptions.IgnoreCase))
                    return true;

                if (Regex.IsMatch(text, @"^(<|>|≦|≧|\d|mg/dL|g/dL|pg|fL|IU/L|U/L|NG/DL|mIU/L|/HPF)", RegexOptions.IgnoreCase))
                    return true;

                if (text.Length <= 18)
                    return true;
            }

            return false;
        }

        private static bool IsUnitOnlyLine(string line)
        {
            return Regex.IsMatch(
                line,
                @"^(g/dL|mg/dL|pg|fL|IU/L|U/L|ng/mL|NG/ML|NG/DL|mEq/L|MG/DL|mIU/L|/HPF|/100WBC)$",
                RegexOptions.IgnoreCase
            );
        }

        private static bool IsHeaderLine(string line)
        {
            return line.Contains("檢查項目") && line.Contains("本次") && line.Contains("參考值");
        }

        private static bool IsNoiseLine(string text)
        {
            var t = text.Trim();

            return t == "）"
                   || t == "("
                   || t == ")"
                   || t == "（"
                   || t == "1（"
                   || t == "Laboratory Examination"
                   || t == "Physical Examination"
                   || t == "Imaging Examination";
        }

        private static bool IsRealSection(string text)
        {
            return text.Contains("檢查") &&
                   (text.Contains("Exam") ||
                    text.Contains("Function") ||
                    text.Contains("Lipid") ||
                    text.Contains("Hepatitis") ||
                    text.Contains("Markers") ||
                    text.Contains("Routine") ||
                    text.Contains("Others"));
        }

        private static void SplitCombinedSectionAndItem(ref string item, List<PhysicalExamRow> rows)
        {
            var m = Regex.Match(item, @"^(.*?(檢查|Exam|Function|Lipid|Hepatitis|Markers|Routine|Others)\s*\))\s*(.+)$");
            if (m.Success)
            {
                var sectionText = CleanupText(m.Groups[1].Value);
                var itemText = CleanupText(m.Groups[3].Value);

                if (IsRealSection(sectionText))
                {
                    rows.Add(new PhysicalExamRow
                    {
                        Item = sectionText,
                        Result = "",
                        Previous = "",
                        Reference = "",
                        IsSection = true
                    });

                    item = itemText;
                }
            }
        }

        private static string NormalizeReference(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
                return "";

            var r = CleanupText(reference);
            r = Regex.Replace(r, @"(\d)\-\s+(\d)", "$1-$2");

            return r.Trim();
        }

        private static string CleanupText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            var t = text.Replace('\u00A0', ' ');
            t = Regex.Replace(t, @"\s+", " ");
            t = t.Replace(" .", ".")
                 .Replace(".(", "(")
                 .Trim();

            return t;
        }

        private static bool IsBodyCheckFormat(string text)
        {
            return text.Contains("Body height") ||
                   text.Contains("Body weight") ||
                   text.Contains("Pulse rate") ||
                   text.Contains("Blood pressure") ||
                   text.Contains("Abdominal girth");
        }


    }
}