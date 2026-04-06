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
        public string Extract(string filePath, string? contentType = null)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException(filePath);

            var ext = Path.GetExtension(filePath).ToLowerInvariant();

            return ext switch
            {
                ".txt" or ".csv" => ExtractTxt(filePath),
                ".pdf" => ExtractPdf(filePath),
                ".docx" => ExtractDocx(filePath),
                _ => "(尚未支援此檔案格式，請改用 .txt/.pdf/.docx)"
            };
        }

        private string ExtractTxt(string path)
            => File.ReadAllText(path, Encoding.UTF8);

        private string ExtractPdf(string path)
        {
            var sb = new StringBuilder();

            using var doc = PdfDocument.Open(path);

            foreach (var page in doc.GetPages())
            {
                var words = page.GetWords().ToList();

                // 依 Y 座標分群，組成列
                var rowGroups = words
                    .GroupBy(w => Math.Round(w.BoundingBox.Bottom / 5.0) * 5.0)
                    .OrderByDescending(g => g.Key)
                    .ToList();

                foreach (var row in rowGroups)
                {
                    var orderedWords = row.OrderBy(w => w.BoundingBox.Left).ToList();

                    var lineBuilder = new StringBuilder();
                    double? prevRight = null;

                    foreach (var w in orderedWords)
                    {
                        if (prevRight.HasValue)
                        {
                            var gap = w.BoundingBox.Left - prevRight.Value;

                            // gap 大就視為跨欄，用 tab 分隔
                            if (gap > 25)
                                lineBuilder.Append('\t');
                            else
                                lineBuilder.Append(' ');
                        }

                        lineBuilder.Append(w.Text);
                        prevRight = w.BoundingBox.Right;
                    }

                    var line = lineBuilder.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(line))
                        sb.AppendLine(line);
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string ExtractDocx(string path)
        {
            using var doc = DocX.Load(path);
            return doc.Text ?? string.Empty;
        }
    }
}