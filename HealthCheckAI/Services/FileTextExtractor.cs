using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using Xceed.Words.NET;

namespace HealthCheckAI.Services
{
    public class FileTextExtractor
    {
        public string Extract(string filePath, string? contentType = null, string? department = null)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException(filePath);

            var ext = Path.GetExtension(filePath).ToLowerInvariant();

            return ext switch
            {
                ".txt" or ".csv" => ExtractTxt(filePath),
                ".pdf" => ExtractPdf(path: filePath, department),
                ".docx" => ExtractDocx(filePath),
                _ => "(尚未支援此檔案格式，請改用 .txt/.pdf/.docx)"
            };
        }
        private static readonly string[] SectionNames =
{
    "血液檢查","生化檢查","肝功能檢查","腎功能檢查","血脂肪檢查",
    "糖尿病檢查","痛風檢查","胰臟功能檢查","心臟血管功能檢查",
    "甲狀腺檢查","肝炎標記","血液腫瘤標誌","其它檢查","尿液檢查"
};

        private static bool TrySplitSectionLine(string line, out string sectionPart, out string remainPart)
        {
            sectionPart = "";
            remainPart = "";

            if (string.IsNullOrWhiteSpace(line))
                return false;

            foreach (var s in SectionNames)
            {
                if (!line.Contains(s))
                    continue;

                // 例：生化檢查 (Biochemistry Exam) 白蛋白(Albumin) 4.7 --- 3.5-5.7 g/dL
                var m = System.Text.RegularExpressions.Regex.Match(
                    line,
                    @"^(?<section>" + System.Text.RegularExpressions.Regex.Escape(s) + @"\s*\([^)]+\))\s*(?<rest>.*)$"
                );

                if (m.Success)
                {
                    sectionPart = m.Groups["section"].Value.Trim();
                    remainPart = m.Groups["rest"].Value.Trim();
                    return true;
                }

                // 沒有英文括號時也拆
                if (line.StartsWith(s))
                {
                    sectionPart = s;
                    remainPart = line.Substring(s.Length).Trim();
                    return true;
                }
            }

            return false;
        }
        private string ExtractTxt(string path)
            => File.ReadAllText(path, Encoding.UTF8);
        private static List<string> MergeBrokenSectionLines(List<string> lines)
        {
            var result = new List<string>();

            for (int i = 0; i < lines.Count; i++)
            {
                var current = lines[i].Trim();

                if (i + 1 < lines.Count)
                {
                    var next = lines[i + 1].Trim();

                    // 修 section 被拆成兩行：生化檢 + 查 ( Biochemistry Exam )
                    if ((current.EndsWith("檢") || current.EndsWith("查")) &&
                        (next.StartsWith("查") || next.StartsWith("( ") || next.StartsWith("(")))
                    {
                        var merged = (current + next).Replace("檢查", "檢查");
                        result.Add(FixBrokenLine(merged));
                        i++;
                        continue;
                    }

                    if (current.EndsWith("檢") && next.StartsWith("查"))
                    {
                        result.Add(FixBrokenLine(current + next));
                        i++;
                        continue;
                    }
                }

                result.Add(FixBrokenLine(current));
            }

            return result;
        }
        private string ExtractPdf(string path, string? department)
        {
            var sb = new StringBuilder();

            using var doc = PdfDocument.Open(path);

            foreach (var page in doc.GetPages())
            {
                var words = page.GetWords()
                    .Select(w => new
                    {
                        Text = NormalizeText(w.Text),
                        Left = w.BoundingBox.Left,
                        Right = w.BoundingBox.Right,
                        Bottom = w.BoundingBox.Bottom
                    })
                    .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                    .ToList();

                if (!words.Any())
                    continue;

                // 🔥 用 Y 分列（比你原本穩）
                var rows = words
                    .GroupBy(w => Math.Round(w.Bottom / 3.0) * 3.0)
                    .OrderByDescending(g => g.Key)
                    .ToList();

                var pageLines = new List<string>();

                foreach (var row in rows)
                {
                    var ordered = row.OrderBy(w => w.Left).ToList();

                    var lineBuilder = new StringBuilder();
                    double? prevRight = null;

                    foreach (var w in ordered)
                    {
                        if (prevRight.HasValue)
                        {
                            var gap = w.Left - prevRight.Value;

                            if (gap > 60)
                                lineBuilder.Append('\t');
                            else if (gap > 25)
                                lineBuilder.Append('\t');
                            else
                                lineBuilder.Append(' ');
                        }

                        lineBuilder.Append(w.Text);
                        prevRight = w.Right;
                    }

                    var line = lineBuilder.ToString().Trim();

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (IsNoise(line))
                        continue;

                    pageLines.Add(line);
                }

                pageLines = MergeBrokenSectionLines(pageLines);

                foreach (var line in pageLines)
                {
                    sb.AppendLine(line);
                }

                sb.AppendLine();

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string ExtractDocx(string path)
        {
            using var doc = DocX.Load(path);
            return doc.Text ?? string.Empty;
        }

        // ===================== 工具區 =====================

        private static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            text = text.Replace('\u00A0', ' ')
                       .Replace("（", "(")
                       .Replace("）", ")")
                       .Trim();

            return System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        }

        private static bool IsNoise(string line)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(line, @"^-\d+-$")
                || System.Text.RegularExpressions.Regex.IsMatch(line, @"^\d{8,}$")
                || line == "(" || line == ")";
        }

        
        private static string FixBrokenLine(string line)
        {
            // 修正這種：
            // 4.5 - 5.9 → 4.5-5.9
            line = line.Replace(" - ", "-");

            // 修正單位空格
            line = line.Replace(" / ", "/");

            // 修正括號
            line = line.Replace("( ", "(").Replace(" )", ")");

            return line;
        }
    }
}