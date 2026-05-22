namespace HealthCheckAI.Models
{
    public class HealthTrendViewModel
    {
        public string Department { get; set; } = "";
        public int CurrentScore { get; set; }
        public int PreviousScore { get; set; }
        public int Difference { get; set; }
        public string TrendText { get; set; } = "";
        public string TrendIcon { get; set; } = "";
        public DateTime? CurrentDate { get; set; }

        public DateTime? PreviousDate { get; set; }
    }
}