namespace HealthCheckAI.Helpers
{
    public enum ReportType
    {
        Unknown = 0,
        PhysicalExam = 1,       // 系統體格檢查表
        SimplePhysical = 2,     // 理學檢查
        Laboratory = 3,         // 實驗室檢查
        Eye = 4,                // 眼科檢查
        ECG = 5,                // 靜態心電圖
        Ultrasound = 6          // 超音波 / 精密儀器 / 純文字報告
    }
}