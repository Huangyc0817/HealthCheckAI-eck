using Microsoft.AspNetCore.Mvc;

namespace HealthCheckAI.Models
{
    public class ReportFile
    {
        public int Id { get; set; }
        public string PatientName { get; set; } = "";
        public string OriginalName { get; set; } = "";
        public string? ContentType { get; set; }
        public DateTime UploadedAt { get; set; }
        public string? ExtractedText { get; set; }
    }
}
