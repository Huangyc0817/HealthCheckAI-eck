using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HealthCheckAI.Helpers
{
    public class TextBlock
    {
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
    }

    public static class TextBlockParser
    {
        // ✅ 判斷：像不像表格（有「項目 結果 參考值」或很多規律的數值欄位）
        public static bool LooksLikeTable(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            var t = Normalize(text);

            // 有明顯表格頭
            if (Regex.IsMatch(t, @"項目\s*結果\s*參考值")) return true;

            // 行內同時出現「數值 + 單位」的比例高，也通常是表格/檢驗項目
            // 例如：156.6 公分、151/89 mmHg、84 次/分鐘
            int hits = Regex.Matches(t, @"\d+(\.\d+)?\s*(公分|公斤|mmHg|次/分鐘|%|mg/dL|mmol/L|U/L)", RegexOptions.IgnoreCase).Count;
            return hits >= 3;
        }

        // ✅ 把純文字拆成多段（標題/內容）
        public static List<TextBlock> SplitToBlocks(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<TextBlock>();

            var t = Normalize(text);

            // 先把「常見標題」前後加換行，讓它容易分段
            string[] headings =
            {
                "主訴與病史","健檢原因","健康原因",
                "系統體格檢查","理學檢查","眼科檢查","靜態心電圖","實驗室檢查","精密儀器檢查",
                "診斷及建議","重點整理","健康建議","內容摘要"
            };

            foreach (var h in headings)
            {
                t = t.Replace(h, $"\n\n##{h}##\n");
            }

            // 用空行切段
            var parts = Regex.Split(t, @"\n{2,}")
                             .Select(p => p.Trim())
                             .Where(p => !string.IsNullOrWhiteSpace(p))
                             .ToList();

            var blocks = new List<TextBlock>();

            foreach (var p in parts)
            {
                // 如果段落是標題段（##標題##）
                var m = Regex.Match(p, @"^##(.+?)##\s*(.*)$", RegexOptions.Singleline);
                if (m.Success)
                {
                    blocks.Add(new TextBlock
                    {
                        Title = m.Groups[1].Value.Trim(),
                        Content = m.Groups[2].Value.Trim()
                    });
                }
                else
                {
                    // 沒標題 → 當作一般段落
                    blocks.Add(new TextBlock
                    {
                        Title = "",
                        Content = p
                    });
                }
            }

            // 如果內容太擠（沒有換行），再做一次「句子換行」
            for (int i = 0; i < blocks.Count; i++)
            {
                blocks[i].Content = AddSentenceBreaks(blocks[i].Content);
            }

            return blocks;
        }

        private static string Normalize(string text)
        {
            // 統一換行 + 壓掉多餘空白，但保留段落
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            text = Regex.Replace(text, @"[ \t]{2,}", " ");
            text = Regex.Replace(text, @"\n{3,}", "\n\n");
            return text.Trim();
        }

        private static string AddSentenceBreaks(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";

            // 中文句號/分號/冒號換行
            s = Regex.Replace(s, @"([。；：])\s*", "$1\n");

            // 英文句點後換行（避免小數點誤切：前面是數字就不切）
            s = Regex.Replace(s, @"(?<!\d)\.\s*", ".\n");

            // 條列 1) 2) 或 1. 2. 換行
            s = Regex.Replace(s, @"\s*(\d+)[\.\)]\s*", "\n$1. ");

            // 避免太多空行
            s = Regex.Replace(s, @"\n{3,}", "\n\n");

            return s.Trim();
        }
    }
}
