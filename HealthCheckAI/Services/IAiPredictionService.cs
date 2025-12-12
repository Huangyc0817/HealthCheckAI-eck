using HealthCheckAI.Models;


namespace HealthCheckAI.Services
{
    public interface IAiPredictionService
    {
        // 如果你之後要純看風險分數可用

        /// <summary>
        /// 分析一份報告文字，回傳完整的 AI 結果
        /// </summary>
        AiReportResult Analyze(string patientName, string department, string text);
    }
}
