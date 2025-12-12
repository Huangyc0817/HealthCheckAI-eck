namespace HealthCheckAI.Models
{
    public class PatientFile
    {
        public int Id { get; set; }
        public string PatientName { get; set; }
        // 這裡改成用來存帳號（Username），不是中文名
        public string Department { get; set; }
        public DateTime UploadDate { get; set; }
        public string? ContentType { get; set; }
        public DateTime? UploadedAt { get; set; }
        public string? ExtractedText { get; set; }
        public string? AiSummary { get; set; }
        public string? AiSeverity { get; set; }
        public bool IsPublishedToPublic { get; set; }
        public DateTime? PublishedAt { get; set; }
        public int? AiScore { get; set; }

    }
}