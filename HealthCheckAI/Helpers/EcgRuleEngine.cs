using System.Text.RegularExpressions;
using HealthCheckAI.Models;

namespace HealthCheckAI.Helpers
{
    public static class EcgRuleEngine
    {
        public static (string summary, string suggestion) Build(EcgParameterResult ecg)
        {
            var lines = new List<string>();
            var suggestions = new List<string>();

            int? hr = ParseFirstInt(ecg.HeartRate);

            if (hr.HasValue)
            {
                lines.Add($"1. 心率約 {hr.Value} BPM。");

                if (hr.Value < 60)
                    lines.Add("2. 心率偏慢，可能偏向竇性心搏過緩。");
                else if (hr.Value > 100)
                    lines.Add("2. 心率偏快，可能偏向竇性心搏過速。");
                else
                    lines.Add("2. 心率落在一般成人常見範圍內。");
            }
            else
            {
                lines.Add("1. 未明確辨識到心率數值。");
            }

            if (!string.IsNullOrWhiteSpace(ecg.MachineInterpretation))
            {
                lines.Add($"3. 儀器初步判讀顯示：{TranslateInterpretation(ecg.MachineInterpretation)}。");
            }
            else
            {
                lines.Add("3. 本報告未明確抓到儀器初步判讀結果。");
            }

            return (string.Join("\n", lines), string.Join("\n", suggestions));
        }

        private static int? ParseFirstInt(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            input = input.Replace("O", "0")
                         .Replace("o", "0")
                         .Replace("I", "1")
                         .Replace("l", "1")
                         .Replace("T", "7");

            var m = Regex.Match(input, @"\d+");

            if (!m.Success)
                return null;

            return int.TryParse(m.Value, out int value) ? value : null;
        }

        private static string TranslateInterpretation(string text)
        {
            var lower = text.ToLower();

            if (lower.Contains("normal sinus rhythm"))
                return "正常竇性心律";

            if (lower.Contains("normal ecg"))
                return "正常心電圖";

            if (lower.Contains("sinus rhythm"))
                return "竇性心律";

            if (lower.Contains("sinus arrhythmia"))
                return "竇性心律不整";

            if (lower.Contains("sinus bradycardia"))
                return "竇性心搏過緩";

            if (lower.Contains("sinus tachycardia"))
                return "竇性心搏過速";

            if (lower.Contains("atrial fibrillation") || lower == "af")
                return "心房顫動";

            if (lower.Contains("pvc") || lower.Contains("premature ventricular contraction"))
                return "心室早期收縮";

            if (lower.Contains("st elevation"))
                return "ST 段上升";

            if (lower.Contains("st depression"))
                return "ST 段下降";

            if (lower.Contains("abnormal ecg"))
                return "異常心電圖";

            return text;
        }
    }
}