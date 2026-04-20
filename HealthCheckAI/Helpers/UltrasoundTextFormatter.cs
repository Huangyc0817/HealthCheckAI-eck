using System.Text.RegularExpressions;

namespace HealthCheckAI.Helpers
{
    public static class UltrasoundTextFormatter
    {
        public static string Format(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            text = text.Replace("\r\n", " ")
                       .Replace("\r", " ")
                       .Replace("\n", " ")
                       .Replace('\u00A0', ' ')
                       .Trim();

            text = Regex.Replace(text, @"\s+", " ");

            text = text.Replace("精密儀器檢查 （Precision instrument inspection）", "")
                       .Replace("精密儀器檢查 (Precision instrument inspection)", "")
                       .Trim();

            string[] titles =
            {
                "頸動脈超音波檢查",
                "甲狀腺超音波檢查",
                "腹部超音波",
                "心臟超音波檢查",
                "乳房超音波檢查",
                "婦科超音波檢查",
                "肝纖維化掃描"
            };

            foreach (var title in titles)
            {
                text = text.Replace(title, $"\n\n【{title}】");
            }

            text = text.Replace("報告", "\n報告");
            text = text.Replace("診斷（Diagnosis）", "\n診斷（Diagnosis）");
            text = text.Replace("診斷 (Diagnosis)", "\n診斷（Diagnosis）");
            text = text.Replace("建議（Suggestion）", "\n建議（Suggestion）");
            text = text.Replace("建議 (Suggestion)", "\n建議（Suggestion）");
            text = text.Replace("乳房影像診斷分級（BI-RADS category）", "\n乳房影像診斷分級（BI-RADS category）");

            text = text.Replace("頸動脈：", "\n頸動脈：");
            text = text.Replace("椎動脈：", "\n椎動脈：");
            text = text.Replace("膽胰腎：", "\n膽胰腎：");

            text = text.Replace("右側甲狀腺囊腫", "\n右側甲狀腺囊腫");
            text = text.Replace("左側甲狀腺囊腫", "\n左側甲狀腺囊腫");
            text = text.Replace("極輕度脂肪肝", "\n極輕度脂肪肝");
            text = text.Replace("肝臟結節", "\n肝臟結節");
            text = text.Replace("疑似血管瘤", "\n疑似血管瘤");
            text = text.Replace("疑似副脾", "\n疑似副脾");
            text = text.Replace("左心房及左心室大小正常", "\n左心房及左心室大小正常");
            text = text.Replace("左心室收縮功能正常", "\n左心室收縮功能正常");
            text = text.Replace("輕度三尖瓣閉鎖不全", "\n輕度三尖瓣閉鎖不全");
            text = text.Replace("輕度肺動脈瓣閉鎖不全", "\n輕度肺動脈瓣閉鎖不全");
            text = text.Replace("無局部左心室壁活動異常", "\n無局部左心室壁活動異常");
            text = text.Replace("肝纖維化等級：", "\n肝纖維化等級：");
            text = text.Replace("脂肪肝等級：", "\n脂肪肝等級：");

            text = text.Replace("無明顯異常椎動脈：", "無明顯異常\n椎動脈：");
            text = text.Replace("疑似血管瘤疑似副脾", "疑似血管瘤\n疑似副脾");
            text = text.Replace("併鈣化 左側", "併鈣化\n左側");
            text = text.Replace("無明顯異常 乳房影像診斷分級", "無明顯異常\n乳房影像診斷分級");
            text = text.Replace("無明顯異常 【", "無明顯異常\n【");

            text = text.Replace("（", "(").Replace("）", ")");
            text = text.Replace("(Diagnosis)", "（Diagnosis）");
            text = text.Replace("(Suggestion)", "（Suggestion）");
            text = text.Replace("(BI-RADS category)", "（BI-RADS category）");

            text = Regex.Replace(text, @"[ \t]+", " ");
            text = Regex.Replace(text, @" *\n *", "\n");

            // 只保留最多一個空白行，不要連續很多行
            text = Regex.Replace(text, @"\n{3,}", "\n\n");

            return text.Trim();
        }
    }
}