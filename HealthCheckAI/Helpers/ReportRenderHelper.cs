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
            var content = (reportContent ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Trim();
            if (string.IsNullOrWhiteSpace(content)) return parts;

            // 定義切分關鍵字
            string[] tableKeys = { "Laboratory Examination", "Physical Examination", "Imaging Examination", "檢查項目", "項目\t本次" };
            int tableStartIdx = -1;
            foreach (var k in tableKeys)
            {
                var t = content.IndexOf(k, StringComparison.OrdinalIgnoreCase);
                if (t >= 0 && (tableStartIdx == -1 || t < tableStartIdx)) tableStartIdx = t;
            }

            int kpIdx = content.IndexOf("重點整理：", StringComparison.Ordinal);
            int sugIdx = content.IndexOf("健康建議：", StringComparison.Ordinal);
            int cutAfter = (kpIdx >= 0 && sugIdx >= 0) ? Math.Min(kpIdx, sugIdx) : Math.Max(kpIdx, sugIdx);

            if (tableStartIdx >= 0)
            {
                parts.BeforeText = content.Substring(0, tableStartIdx).Trim();
                if (cutAfter > tableStartIdx)
                {
                    parts.TableRawText = content.Substring(tableStartIdx, cutAfter - tableStartIdx).Trim();
                    string afterAll = content.Substring(cutAfter).Trim();
                    ExtractPostTableText(afterAll, parts);
                }
                else
                {
                    parts.TableRawText = content.Substring(tableStartIdx).Trim();
                }
            }
            else
            {
                parts.BeforeText = content;
            }

            if (!string.IsNullOrWhiteSpace(parts.TableRawText))
            {
                parts.TableRows = ParseTableContent(parts.TableRawText);
            }

            return parts;
        }


        private static List<PhysicalExamRow> ParseTableContent(string tablePart)
        {
            var rows = new List<PhysicalExamRow>();
            var lines = tablePart.Split('\n').Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

            // 強制定義錨點清單，確保「項目」欄位精準
            string[] standardItems = {
        "紅血球(RBC)", "白血球(WBC)", "血色素(Hb)", "血球比容積(Ht)", "血小板",
        "平均紅血球體積(MCV)", "平均紅血球血紅素(MCH)", "平均紅血球血紅素濃度(MCHC)",
        "嗜中性球", "淋巴球", "單核球", "嗜伊紅性球", "嗜鹼性球"
    };
            int itemIdx = 0;

            foreach (var line in lines)
            {
                if (IsHeaderLine(line) || IsNoiseLine(line)) continue;

                // 1. 提取這一行所有的數字區塊
                var segments = Regex.Matches(line, @"(\d+[\s\.\-]*\d+[\d\.\^/a-zA-Z%]*)|(<\s*\d+\.?\d*)|(\d+\.\d+)|(\d+)|(---)")
                                    .Cast<Match>().Select(m => m.Value.Trim()).ToList();

                if (segments.Count > 0 && itemIdx < standardItems.Length)
                {
                    var row = new PhysicalExamRow
                    {
                        Item = standardItems[itemIdx], // 強制使用標準名稱
                        Previous = "---",
                        Reference = ""
                    };

                    // 2. 數據分配策略：
                    // A. 尋找長得像參考值（帶 - 或 < 或單位）的碎片
                    var refVal = segments.FirstOrDefault(s => s.Contains("-") || s.Contains("<") || Regex.IsMatch(s, @"[a-zA-Z%/\^]"));
                    if (refVal != null)
                    {
                        row.Reference = refVal;
                        segments.Remove(refVal);
                    }

                    // B. 剩下的數字：第一個是本次，第二個是前次
                    var numericOnly = segments.Where(s => Regex.IsMatch(s, @"\d|---")).ToList();
                    if (numericOnly.Count >= 1) row.Result = numericOnly[0];
                    if (numericOnly.Count >= 2) row.Previous = numericOnly[1];

                    rows.Add(row);
                    itemIdx++; // 移向下一個預期項目
                }
            }
            return rows;
        }

        // 輔助方法：把抓到的數據塊依序塞進對應欄位
        private static PhysicalExamRow CreateRow(string item, List<string> data)
        {
            var row = new PhysicalExamRow { Item = item, Previous = "---", Reference = "" };

            // 尋找看起來像參考值(帶區間 - )的資料
            var refPart = data.FirstOrDefault(d => d.Contains("-") || d.Contains("<") || d.Contains(">"));
            if (refPart != null)
            {
                row.Reference = refPart;
                data.Remove(refPart);
            }

            if (data.Count >= 2)
            {
                row.Result = data[0];
                row.Previous = data[1];
            }
            else if (data.Count == 1)
            {
                row.Result = data[0];
            }

            return row;
        }

        private static bool IsLikelyReference(string text) =>
            Regex.IsMatch(text, @"(\d+\s*-\s*\d+)|(<|>|≦|≧)|(10\^)|(g/dL|uL|fl|pg|%)", RegexOptions.IgnoreCase);

        private static bool IsHeaderLine(string line) =>
            line.Contains("檢查項目") || (line.Contains("本次") && line.Contains("參考值"));

        private static bool IsNoiseLine(string text) =>
            new[] { "）", "(", ")", "（" }.Contains(text.Trim());

        private static bool IsRealSection(string text) =>
            text.Contains("檢查") || text.Contains("Exam") || text.Contains("Function") || text.Contains("Lipid");

        private static string CleanupText(string text) =>
            Regex.Replace(text ?? "", @"[\t\r\n]+", " ").Trim();

        private static void ExtractPostTableText(string afterAll, ReportParts parts)
        {
            int k0 = afterAll.IndexOf("重點整理：", StringComparison.Ordinal);
            int s0 = afterAll.IndexOf("健康建議：", StringComparison.Ordinal);
            if (k0 >= 0 && s0 >= 0)
            {
                if (k0 < s0)
                {
                    parts.KeyPointsText = afterAll.Substring(k0 + 5, s0 - (k0 + 5)).Trim();
                    parts.SuggestionsText = afterAll.Substring(s0 + 5).Trim();
                }
                else
                {
                    parts.SuggestionsText = afterAll.Substring(s0 + 5, k0 - (s0 + 5)).Trim();
                    parts.KeyPointsText = afterAll.Substring(k0 + 5).Trim();
                }
            }
            else if (k0 >= 0) parts.KeyPointsText = afterAll.Substring(k0 + 5).Trim();
            else if (s0 >= 0) parts.SuggestionsText = afterAll.Substring(s0 + 5).Trim();
        }
    }
}