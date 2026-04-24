using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

            var text = ext switch
            {
                ".txt" or ".csv" => ExtractTxt(filePath),
                ".pdf" => ExtractPdf(path: filePath, department),
                ".docx" => ExtractDocx(filePath),
                _ => "(尚未支援此檔案格式，請改用 .txt/.pdf/.docx)"
            };

            // 只在眼科報告時移除原始建議段落
            text = RemoveSuggestionSection(text, department);

            return text;
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

                var m = Regex.Match(
                    line,
                    @"^(?<section>" + Regex.Escape(s) + @"\s*\([^)]+\))\s*(?<rest>.*)$"
                );

                if (m.Success)
                {
                    sectionPart = m.Groups["section"].Value.Trim();
                    remainPart = m.Groups["rest"].Value.Trim();
                    return true;
                }

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

        private static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            text = text.Replace('\u00A0', ' ')
                       .Replace("（", "(")
                       .Replace("）", ")")
                       .Trim();

            return Regex.Replace(text, @"\s+", " ");
        }

        private static bool IsNoise(string line)
        {
            return Regex.IsMatch(line, @"^-\d+-$")
                || Regex.IsMatch(line, @"^\d{8,}$")
                || line == "(" || line == ")";
        }

        private static string FixBrokenLine(string line)
        {
            line = line.Replace(" - ", "-");
            line = line.Replace(" / ", "/");
            line = line.Replace("( ", "(").Replace(" )", ")");

            return line;
        }

        private static string RemoveSuggestionSection(string text, string? department)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            // 只針對眼科處理，避免影響其他科別
            if (string.IsNullOrWhiteSpace(department) || !department.Contains("眼"))
                return text;

            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

            // 砍掉「建議(Suggestion) / 建議（Suggestion） / 建議」之後全部內容
            text = Regex.Replace(
                text,
                @"建議(\s*[（(]\s*Suggestion\s*[）)])?\s*[\s\S]*$",
                "",
                RegexOptions.IgnoreCase
            );

            return text.Trim();
        }
    }
}