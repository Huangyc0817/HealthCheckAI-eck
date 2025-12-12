namespace HealthCheckAI.Models
{
    public class DeptSummaryViewModel
    {
        public int Order { get; set; }          // 原來的序號 1~8
        public string Department { get; set; }  // 中文科別
        public string EnglishName { get; set; } // 英文科別
        public string Severity { get; set; }    // 嚴重程度：低 / 中 / 高
        public int Score { get; set; }          // 給進度條用的分數 0~100
    }
}
