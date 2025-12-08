using System.IO;
using System.Text;
using UglyToad.PdfPig;
using Xceed.Words.NET;

namespace HealthCheckAI.Services
{
    public class FileTextExtractor
    {
        public string Extract(string filePath, string? contentType = null)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException(filePath);

            // 先用副檔名判斷
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
                sb.AppendLine(page.Text);
            return sb.ToString();
        }

        private string ExtractDocx(string path)
        {
            using var doc = DocX.Load(path);
            return doc.Text ?? string.Empty;
        }
    }
}

