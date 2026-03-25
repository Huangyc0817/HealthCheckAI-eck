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
        // 專門解析「體格檢查 / Physical Examination」這種 4 欄表格
        public static List<PhysicalExamRow> ParsePhysicalExamTable(string text)
        {
            var rows = new List<PhysicalExamRow>();
            if (string.IsNullOrWhiteSpace(text)) return rows;

            var lines = text.Split('\n')
                            .Select(x => x.Trim())
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .ToList();

            foreach (var line in lines)
            {
                // 跳過表頭
                if (line.Contains("項目") && line.Contains("參考值"))
                    continue;

                var parts = line.Split('\t')
                                .Select(x => x.Trim())
                                .ToList();

                // 4欄：項目 / 本次 / 前次 / 參考值
                if (parts.Count >= 4)
                {
                    rows.Add(new PhysicalExamRow
                    {
                        Item = parts[0],
                        Result = parts[1],
                        Previous = parts[2],
                        Reference = parts[3]
                    });
                }
                // 3欄：項目 / 結果 / 參考值
                else if (parts.Count >= 3)
                {
                    rows.Add(new PhysicalExamRow
                    {
                        Item = parts[0],
                        Result = parts[1],
                        Previous = "",
                        Reference = parts[2]
                    });
                }
            }

            return rows;
        }

        private static PhysicalExamRow ParseSegmentToRow(string seg)
        {
            // 先把中文 item 抓出來（seg 開頭）
            // item 可能後面有（英文）
            var itemMatch = Regex.Match(seg, @"^(?<item>[\u4e00-\u9fff]+)");
            if (!itemMatch.Success) return new PhysicalExamRow();

            var item = itemMatch.Groups["item"].Value.Trim();

            // 把 item（含英文括號）從 seg 拿掉，剩下就是結果+參考值
            // 盡量吃掉 (English) 那段
            var rest = Regex.Replace(seg, @"^[\u4e00-\u9fff]+(\s*（.*?）|\s*\(.*?\))?\s*", "").Trim();

            // 特例：理想體重範圍公式 不是三欄表格，就整段塞 result
            if (item.Contains("理想體重"))
            {
                return new PhysicalExamRow
                {
                    Item = "理想體重範圍公式",
                    Result = rest,
                    Reference = "-"
                };
            }

            // 嘗試把「參考值」抓出來：
            // 常見型態：43.8至59.2公斤、18.5-23.9、60-100 次/分鐘、120-90 / 80-60 mmHg、國人女性標準：小於80公分
            string reference = "";
            string result = rest;

            // 先抓「國人標準：...」這種
            var refLabel = Regex.Match(rest, @"(國人.*?標準[:：]\s*.*)$");
            if (refLabel.Success)
            {
                reference = refLabel.Groups[1].Value.Trim();
                result = rest.Replace(reference, "").Trim();
                return new PhysicalExamRow { Item = item, Result = result, Reference = reference };
            }

            // 再抓「範圍」類型（xx至yy、xx-yy、含單位）
            var range = Regex.Match(rest, @"(?<ref>\d+(\.\d+)?\s*(至|-)\s*\d+(\.\d+)?\s*[^\s]+.*)$");
            if (range.Success)
            {
                reference = range.Groups["ref"].Value.Trim();
                result = rest.Substring(0, range.Index).Trim();
                return new PhysicalExamRow { Item = item, Result = result, Reference = reference };
            }

            // 再抓像「60-100 次/分鐘」這種
            var range2 = Regex.Match(rest, @"(?<ref>\d+\s*-\s*\d+\s*[^ ]+)$");
            if (range2.Success)
            {
                reference = range2.Groups["ref"].Value.Trim();
                result = rest.Substring(0, range2.Index).Trim();
                return new PhysicalExamRow { Item = item, Result = result, Reference = reference };
            }

            // 找不到參考值，就全部當 result
            return new PhysicalExamRow { Item = item, Result = result, Reference = "-" };
        }
    }
}
