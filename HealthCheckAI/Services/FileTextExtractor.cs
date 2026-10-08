using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace HealthCheckAI.Services
{
    public class FileTextExtractor
    {
        // 💡 核心升級 1：在參數中偷偷開一個後門，讓 Controller 可以把 OcrService 傳進來
        public string Extract(string filePath, string? contentType = null, string? department = null, OcrService? ocrService = null)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException(filePath);

            var ext = Path.GetExtension(filePath).ToLowerInvariant();

            var text = ext switch
            {
                ".txt" or ".csv" => ExtractTxt(filePath),
                ".pdf" => ExtractPdf(filePath, department, ocrService), // 👈 將 ocrService 傳遞給 PDF 解析器
                ".docx" => ExtractDocx(filePath, department),
                _ => "(尚未支援此檔案格式，請改用 .txt/.pdf/.docx)"
            };

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

        // 💡 核心升級 2：修改 ExtractPdf 讓它具備「讀圖」能力
        private string ExtractPdf(string path, string? department, OcrService? ocrService)
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

                // 🔥 方案 B 發動：如果這一頁「沒有抓到半個字」，它八成是一張包在 PDF 裡的圖片！
                if (!words.Any())
                {
                    if (ocrService != null)
                    {
                        var images = page.GetImages();
                        foreach (var image in images)
                        {
                            try
                            {
                                byte[] imgBytes = null;

                                // 嘗試從 PDF 中把圖片的位元組抽出來
                                if (image.TryGetPng(out var pngBytes))
                                {
                                    imgBytes = pngBytes;
                                }
                                else if (image.RawBytes != null) // 👈 改用這個各版本都支援的屬性
                                {
                                    imgBytes = image.RawBytes.ToArray();
                                }

                                if (imgBytes != null && imgBytes.Length > 0)
                                {
                                    // 產生一個隨機暫存檔名，把圖存到主機裡
                                    var tempImagePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".png");
                                    File.WriteAllBytes(tempImagePath, imgBytes);

                                    // 呼叫 OCR 服務來辨識這張剛抽出來的圖！
                                    var ocrResult = ocrService.ExtractTextFromImage(tempImagePath);
                                    if (!string.IsNullOrWhiteSpace(ocrResult))
                                    {
                                        sb.AppendLine(ocrResult);
                                    }

                                    // 辨識完畢，刪除暫存檔保持系統乾淨
                                    File.Delete(tempImagePath);
                                }
                            }
                            catch
                            {
                                // 忽略單張圖片抽取失敗，繼續處理下一張
                            }
                        }
                    }
                    continue; // 處理完圖片後，直接跳到下一頁
                }

                // --- 以下維持原本正常的 PDF 實體文字分行與對齊邏輯 ---
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

        // 🔥 DOCX 方法維持你原本修正好的版本
        // 🔥 核心升級 3：終極版 DOCX 解析器 (破解隱藏表格陷阱)
        private string ExtractDocx(string path, string? department = null)
        {
            var sb = new StringBuilder();

            try
            {
                using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(path, false))
                {
                    var body = wordDoc.MainDocumentPart?.Document.Body;
                    if (body != null)
                    {
                        // 💡 使用 Descendants() 掃描所有深度的元素，破解 Word 的「內容控制項」陷阱
                        var elements = body.Descendants().Where(e => e is Paragraph || e is Table);

                        foreach (var element in elements)
                        {
                            if (element is Paragraph para)
                            {
                                // 若段落被包在表格裡，跳過 (交由下方的 Table 邏輯處理，避免文字重複)
                                if (para.Ancestors<Table>().Any())
                                    continue;

                                if (!string.IsNullOrWhiteSpace(para.InnerText))
                                {
                                    sb.AppendLine(NormalizeText(para.InnerText));
                                }
                            }
                            else if (element is Table table)
                            {
                                // 若為巢狀表格 (表格裡的表格)，跳過內層，避免重複處理
                                if (table.Ancestors<Table>().Any())
                                    continue;

                                // 使用 Descendants 取代 Elements，破解表格行可能被 <sdt> 包覆的問題
                                foreach (var row in table.Descendants<TableRow>())
                                {
                                    // 確保該 Row 直屬於當前 Table
                                    if (row.Ancestors<Table>().FirstOrDefault() != table)
                                        continue;

                                    var cellsText = new List<string>();
                                    foreach (var cell in row.Descendants<TableCell>())
                                    {
                                        if (cell.Ancestors<TableRow>().FirstOrDefault() != row)
                                            continue;

                                        cellsText.Add(NormalizeText(cell.InnerText));
                                    }

                                    // 只要這行有字，就組裝起來
                                    if (cellsText.Any(t => !string.IsNullOrWhiteSpace(t)))
                                    {
                                        // 💡 使用 " | " 分隔欄位，排版會非常整齊！
                                        sb.AppendLine(string.Join(" | ", cellsText));
                                    }
                                }
                                sb.AppendLine(); // 表格結束多空一行
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"(Word 檔案讀取失敗: {ex.Message})");
            }

            return sb.ToString().Trim();
        }

        private static string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            text = text.Replace('\u00A0', ' ')
                       .Replace("（", "(")
                       .Replace("）", ")")
                       // 💡 關鍵修復：把半形小於/大於換成全形，破解 HTML 隱形吞噬陷阱！
                       .Replace("<", "＜")
                       .Replace(">", "＞")
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

            if (string.IsNullOrWhiteSpace(department) || !department.Contains("眼"))
                return text;

            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

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