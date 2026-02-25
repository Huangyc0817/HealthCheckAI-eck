using System;
using System.Collections.Generic;

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

            // ✅ 只放「表格明確標記」：不要放「系統體格檢查/實驗室檢查」這種會出現在敘述中的字
            string[] keys = new[]
            {
        "表（Physical Examination）",
        "Physical Examination",
        "表（Laboratory Examination）",
        "Laboratory Examination",
        "表（Imaging Examination）",
        "Imaging Examination",
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

            // 後段切點
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
                // ✅ 沒找到表格 key：用「內容摘要」把摘要移到中間
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

            // 後段拆兩格
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

            // 表格解析（用你現有的 parser）
            var rows = !string.IsNullOrWhiteSpace(tablePart)
                ? TableParser.ParsePhysicalExamTable(tablePart)
                : new List<PhysicalExamRow>();

            parts.BeforeText = before;
            parts.TableRows = rows;
            parts.TableRawText = tablePart;   // ✅ 沒表格時這裡就是「內容摘要...」那段
            parts.KeyPointsText = kp;
            parts.SuggestionsText = sug;

            return parts;
        }

    }
}
