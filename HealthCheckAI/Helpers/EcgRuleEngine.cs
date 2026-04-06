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

            if (!string.IsNullOrWhiteSpace(ecg.MachineInterpretation))
            {
                lines.Add($"3. 儀器初步判讀顯示：{ecg.MachineInterpretation}。");
            }
            else
            {
                lines.Add("3. 本報告未明確抓到儀器初步判讀結果。");
            }

            if (!string.IsNullOrWhiteSpace(ecg.PRInterval))
                lines.Add($"4. PR 間期：{ecg.PRInterval}。");

            if (!string.IsNullOrWhiteSpace(ecg.QRSDuration))
                lines.Add($"5. QRS 時間：{ecg.QRSDuration}。");

            if (!string.IsNullOrWhiteSpace(ecg.QT_QTc))
                lines.Add($"6. QT/QTc：{ecg.QT_QTc}。");

            if (!string.IsNullOrWhiteSpace(ecg.Axes))
                lines.Add($"7. 電軸參數：{ecg.Axes}。");

            suggestions.Add("1. 此結果為系統依儀器文字參數產生之初步整理，仍需由醫師結合完整心電圖波形判讀。");
            suggestions.Add("2. 若有胸悶、心悸、呼吸喘、頭暈或昏厥等症狀，建議儘速就醫評估。");

            return (string.Join("\n", lines), string.Join("\n", suggestions));
        }

        private static int? ParseFirstInt(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            var m = Regex.Match(input, @"\d+");
            if (!m.Success) return null;

            if (int.TryParse(m.Value, out int value))
                return value;

            return null;
        }
    }
}