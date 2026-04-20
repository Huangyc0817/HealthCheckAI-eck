using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HealthCheckAI.Helpers
{
    public static class EyeReportFormatter  //眼科檢查解析器
    {
        public static string Format(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            text = text.Replace("（", "(").Replace("）", ")");
            text = Regex.Replace(text, @"[ \t]+", " ");
            text = Regex.Replace(text, @"\n{2,}", "\n");
            text = text.Trim();

            var lines = text.Split('\n')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            var cleaned = new List<string>();

            foreach (var line in lines)
            {
                if (IsNoise(line)) continue;
                cleaned.Add(line);
            }

            var all = string.Join(" ", cleaned);
            all = Regex.Replace(all, @"\s+", " ").Trim();

            // 常見欄位名稱前斷行
            string[] headers =
{
    "各科檢查(Inspection)",
    "眼科檢查",
    "視力裸視",
    "矯正視力",
    "眼壓(<21)",
    "電腦驗光",
    "散光",
    "辨色力",
    "免散瞳眼底攝影報告及影像",
    "診斷(Diagnosis)",
    "診斷（Diagnosis）",
    "建議(Suggestion)",
    "建議（Suggestion）"
};

            foreach (var h in headers)
            {
                all = Regex.Replace(all, $"{Regex.Escape(h)}", "\n" + h + "\n");
            }



            // 右眼 / 左眼資料整理
            all = Regex.Replace(all, @"(?<!\S)右眼(?!\S)", "\n右眼：");
            all = Regex.Replace(all, @"(?<!\S)左眼(?!\S)", "\n左眼：");

            // 把連在一起的數值欄位稍微分開
            all = Regex.Replace(all, @"(?<=\d)\s+(?=-?\d+(\.\d+)?)", " / ");
            all = Regex.Replace(all, @"(?<=---)\s+(?=\d)", " / ");
            all = Regex.Replace(all, @"(?<=異常)\s+(?=左眼：|右眼：)", "\n");

            // ✅ 修正括號被切開
            all = Regex.Replace(all, @"\(\s+", "(");
            all = Regex.Replace(all, @"\s+\)", ")");

            // ✅ 診斷欄位前斷行，但不要把眼壓( Intraocular pressure )拆爛
            all = Regex.Replace(
                all,
                @"\s*(視力\(Visual acuity\)|眼壓\(Intraocular pressure\)|眼底\(Fundus\)|視網膜病變\(Retinopathy\)|黃斑\(Macula\))\s*",
                "\n$1 "
            );

            // ✅ 如果 OCR 把中英文拆開，也順便補救
            all = Regex.Replace(all, @"眼壓\s*\(\s*Intraocular pressure\s*\)", "眼壓(Intraocular pressure)");
            all = Regex.Replace(all, @"眼底\s*\(\s*Fundus\s*\)", "眼底(Fundus)");
            all = Regex.Replace(all, @"視網膜病變\s*\(\s*Retinopathy\s*\)", "視網膜病變(Retinopathy)");
            all = Regex.Replace(all, @"黃斑\s*\(\s*Macula\s*\)", "黃斑(Macula)");
            all = Regex.Replace(all, @"視力\s*\(\s*Visual acuity\s*\)", "視力(Visual acuity)");

            // 清掉多餘空行
            all = Regex.Replace(all, @"\n{3,}", "\n\n").Trim();

            return all;
        }

        private static bool IsNoise(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return true;

            return Regex.IsMatch(line, @"^-\d+-$")          // 頁碼
                   || Regex.IsMatch(line, @"^\d{8,}$")      // 流水號
                   || line == "("
                   || line == ")"
                   || line == "（"
                   || line == "）";
        }
    }
}