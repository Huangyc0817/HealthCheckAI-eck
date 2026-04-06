using System;

namespace HealthCheckAI.Helpers
{
    public static class ReportClassifier
    {
        public static ReportType FromCategory(string? category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return ReportType.Unknown;

            var c = category.Replace(" ", "")
                            .Replace("\r", "")
                            .Replace("\n", "")
                            .Trim();

            if (c.Contains("體格檢查表"))
                return ReportType.PhysicalExam;

            if (c.Contains("理學檢查"))
                return ReportType.SimplePhysical;

            if (c.Contains("實驗室檢查"))
                return ReportType.Laboratory;

            if (c.Contains("眼科檢查"))
                return ReportType.Eye;

            if (c.Contains("靜態心電圖"))
                return ReportType.ECG;

            if ( c.Contains("精密儀器檢查") )
                return ReportType.Ultrasound;

            return ReportType.Unknown;
        }
    }
}