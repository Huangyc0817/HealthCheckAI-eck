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
        // 專門解析「系統體格檢查 / Physical Examination」這種 3 欄表格
        public static List<PhysicalExamRow> ParsePhysicalExamTable(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<PhysicalExamRow>();

            // 1) 清一下常見雜字
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            text = Regex.Replace(text, @"[ ]{2,}", " ");
            text = text.Trim();

            // 2) 把整段先「壓成一行」比較好切
            var one = Regex.Replace(text, @"\s+", " ");

            // 3) 抓出我們要的項目（你圖上有：身高/體重/BMI/腹圍/脈搏/血壓）
            //    你之後要加更多項目，就在這裡加 key
            var keys = new[]
            {
                "身高", "體重", "理想體重範圍公式", "體質量指數", "腹圍", "脈搏", "血壓"
            };

            // 若文本連這些 key 都沒有，直接回空
            if (!keys.Any(k => one.Contains(k))) return new List<PhysicalExamRow>();

            var rows = new List<PhysicalExamRow>();

            for (int i = 0; i < keys.Length; i++)
            {
                var key = keys[i];
                var start = one.IndexOf(key, StringComparison.Ordinal);
                if (start < 0) continue;

                int end = one.Length;
                // 找下一個 key 當作切段終點
                for (int j = i + 1; j < keys.Length; j++)
                {
                    var next = one.IndexOf(keys[j], start + key.Length, StringComparison.Ordinal);
                    if (next > start)
                    {
                        end = next;
                        break;
                    }
                }

                var seg = one.Substring(start, end - start).Trim();

                // seg 例：
                // 身高（Body height）156.6 公分
                // 體重（Body weight）64.7 公斤 43.8至59.2公斤
                // 血壓（Blood pressure）151/89 mmHg 120-90 / 80-60 mmHg

                var row = ParseSegmentToRow(seg);
                if (!string.IsNullOrWhiteSpace(row.Item))
                    rows.Add(row);
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
