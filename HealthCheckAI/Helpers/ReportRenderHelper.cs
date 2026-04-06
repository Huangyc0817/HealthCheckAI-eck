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
        public ReportType ReportType { get; set; } = ReportType.Unknown;
    }

    public static class ReportRenderHelper
    {
        public static ReportParts Split(string? reportContent, string? category = null)
        {
            var parts = new ReportParts();

            var content = (reportContent ?? "")
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Trim();

            if (string.IsNullOrWhiteSpace(content))
                return parts;

            var reportType = !string.IsNullOrWhiteSpace(category)
                ? ReportClassifier.FromCategory(category)
                : DetectTypeFromText(content);

            int kpIdx = content.IndexOf("重點整理：", StringComparison.Ordinal);
            int sugIdx = content.IndexOf("健康建議：", StringComparison.Ordinal);

            int cutAfter = -1;
            if (kpIdx >= 0 && sugIdx >= 0) cutAfter = Math.Min(kpIdx, sugIdx);
            else if (kpIdx >= 0) cutAfter = kpIdx;
            else if (sugIdx >= 0) cutAfter = sugIdx;

            string before = (cutAfter > 0) ? content.Substring(0, cutAfter).Trim() : content.Trim();
            string afterAll = (cutAfter > 0) ? content.Substring(cutAfter).Trim() : "";

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
            string tableRawText = "";

            switch (reportType)
            {
                case ReportType.SimplePhysical:
                    rows = SimplePhysicalParser.Parse(content);
                    tableRawText = content;
                    break;

                case ReportType.PhysicalExam:
                    rows = PhysicalExamParser.Parse(content);
                    tableRawText = content;
                    break;

                case ReportType.Laboratory:
                    rows = ParseLaboratoryTable(content);
                    tableRawText = content;
                    break;

                case ReportType.Eye:
                    rows = new List<PhysicalExamRow>();
                    tableRawText = EyeReportFormatter.Format(content);
                    break;
                case ReportType.ECG:
                    rows = new List<PhysicalExamRow>();

                    var ecg = EcgReportParser.Parse(content);

                    tableRawText =
                        "【心電圖儀器參數】\n" +
                        $"Heart Rate：{(string.IsNullOrWhiteSpace(ecg.HeartRate) ? "未抓到" : ecg.HeartRate)}\n" +
                        $"PR Interval：{(string.IsNullOrWhiteSpace(ecg.PRInterval) ? "未抓到" : ecg.PRInterval)}\n" +
                        $"QRS Duration：{(string.IsNullOrWhiteSpace(ecg.QRSDuration) ? "未抓到" : ecg.QRSDuration)}\n" +
                        $"QT/QTc：{(string.IsNullOrWhiteSpace(ecg.QT_QTc) ? "未抓到" : ecg.QT_QTc)}\n" +
                        $"Axes：{(string.IsNullOrWhiteSpace(ecg.Axes) ? "未抓到" : ecg.Axes)}\n" +
                        $"Machine Interpretation：{(string.IsNullOrWhiteSpace(ecg.MachineInterpretation) ? "未抓到" : ecg.MachineInterpretation)}\n\n";

                    break;
                case ReportType.Ultrasound:
                case ReportType.Unknown:
                default:
                    // 這三類先不要硬拆表格，直接保留原文最穩
                    rows = new List<PhysicalExamRow>();
                    tableRawText = content;
                    break;
            }

            parts.BeforeText = before;
            parts.TableRows = rows;
            parts.TableRawText = tableRawText;
            parts.KeyPointsText = kp;
            parts.SuggestionsText = sug;
            parts.ReportType = reportType;

            return parts;
        }

        private static ReportType DetectTypeFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return ReportType.Unknown;

            if (text.Contains("理學檢查") || (text.Contains("Physical Examination") && text.Contains("無明顯異常")))
                return ReportType.SimplePhysical;

            if ((text.Contains("體格檢查") || text.Contains("Physical Examination")) && IsBodyCheckFormat(text))
                return ReportType.PhysicalExam;

            if (text.Contains("Laboratory Examination") || (text.Contains("檢查項目") && text.Contains("本次") && text.Contains("前次")))
                return ReportType.Laboratory;

            if (text.Contains("眼科檢查"))
                return ReportType.Eye;

            if (text.Contains("心電圖") || text.Contains("ECG"))
                return ReportType.ECG;

            if (text.Contains("超音波") || text.Contains("儀器檢查"))
                return ReportType.Ultrasound;

            return ReportType.Unknown;
        }

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
                    mergedLines[^1] += " " + line;
                else
                    mergedLines.Add(line);
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
                    reference = string.Join(" ", cols.Skip(3));
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
                        previous = "";
                        reference = cols[2];
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

            if (IsUnitOnlyLine(text)) return true;
            if (IsNoiseLine(text)) return true;

            if (!text.Contains('\t'))
            {
                if (Regex.IsMatch(text, @"^[A-Za-z\)\(]+$")) return true;
                if (Regex.IsMatch(text, @"^(Reactive.*|Nonreactive.*|Positive.*|Negative.*|NOT FOUND.*|FOUND.*)$", RegexOptions.IgnoreCase)) return true;
                if (Regex.IsMatch(text, @"^(<|>|≦|≧|\d|mg/dL|g/dL|pg|fL|IU/L|U/L|NG/DL|mIU/L|/HPF)", RegexOptions.IgnoreCase)) return true;
                if (text.Length <= 18) return true;
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

        private static string CleanupText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            var t = text.Replace('\u00A0', ' ');
            t = Regex.Replace(t, @"\s+", " ");
            return t.Trim();
        }

        private static bool IsBodyCheckFormat(string text)
        {
            return text.Contains("Body height") ||
                   text.Contains("Body weight") ||
                   text.Contains("Pulse rate") ||
                   text.Contains("Blood pressure") ||
                   text.Contains("Abdominal girth") ||
                   text.Contains("BMI");
        }
    }
}