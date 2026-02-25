using System.Text.RegularExpressions;

namespace HealthCheckAI.Helpers
{
    public class AiReportParts
    {
        public string Header { get; set; } = "";      // 來賓/科別/程度那段
        public string Summary { get; set; } = "";     // 內容摘要
        public string KeyPoints { get; set; } = "";   // 重點整理
        public string Suggestions { get; set; } = ""; // 健康建議
    }

    public static class AiReportParser
    {
        public static AiReportParts Split(string text)
        {
            text ??= "";

            // 先用關鍵標題切
            var header = "";
            var summary = "";
            var keypoints = "";
            var suggestions = "";

            // 抓 Header（AI 綜合分析結果到「內容摘要：」前）
            var mHeader = Regex.Match(text, @"^(.*?)(內容摘要：)", RegexOptions.Singleline);
            if (mHeader.Success)
                header = mHeader.Groups[1].Value.Trim();
            else
                header = "";

            // Summary（內容摘要：到重點整理：前）
            var mSummary = Regex.Match(text, @"內容摘要：\s*(.*?)(重點整理：)", RegexOptions.Singleline);
            if (mSummary.Success)
                summary = mSummary.Groups[1].Value.Trim();

            // KeyPoints（重點整理：到健康建議：前）
            var mKey = Regex.Match(text, @"重點整理：\s*(.*?)(健康建議：)", RegexOptions.Singleline);
            if (mKey.Success)
                keypoints = mKey.Groups[1].Value.Trim();

            // Suggestions（健康建議：之後全部）
            var mSug = Regex.Match(text, @"健康建議：\s*(.*)$", RegexOptions.Singleline);
            if (mSug.Success)
                suggestions = mSug.Groups[1].Value.Trim();

            return new AiReportParts
            {
                Header = header,
                Summary = summary,
                KeyPoints = keypoints,
                Suggestions = suggestions
            };
        }

        // 組回去存回 AiSummary（保持原本格式）
        public static string Join(AiReportParts p)
        {
            return
                $"{p.Header}\n\n" +
                "內容摘要：\n" + (p.Summary ?? "") + "\n\n" +
                "重點整理：\n" + (p.KeyPoints ?? "") + "\n\n" +
                "健康建議：\n" + (p.Suggestions ?? "");
        }
    }
}
