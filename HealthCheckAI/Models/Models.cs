using System.Collections.Generic;

namespace HealthCheckAI.Models
{

    namespace HealthCheckAI.Models
    {
        public class AiReportResult
        {
            public string Label { get; set; } = string.Empty;
            public float Probability { get; set; }

            public string Summary { get; set; } = string.Empty;
            public string SeverityLevel { get; set; } = string.Empty;
            public string KeyPoints { get; set; } = string.Empty;
            public string Suggestions { get; set; } = string.Empty;
        }
    }

}
