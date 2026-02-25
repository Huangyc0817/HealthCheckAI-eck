using System.Collections.Generic;

namespace HealthCheckAI.Models
{
    public class AiReportResult
    {
        // 風險標籤：純文字就好，避免 emoji 變成 ??? 
        public string Label { get; set; } = string.Empty;      // 例：高風險 / 較低風險 / 需注意
        public float Probability { get; set; }                 // 0 ~ 1

        // 顯示給來賓的內容
        public string Summary { get; set; } = string.Empty;        // 內容摘要
        public string SeverityLevel { get; set; } = string.Empty;  // 高 / 中 / 低
        public string KeyPoints { get; set; } = string.Empty;      // 重點整理（多行文字）
        public string Suggestions { get; set; } = string.Empty;    // 健康建議（多行文字）
    }
}
