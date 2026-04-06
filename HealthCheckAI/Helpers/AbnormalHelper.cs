using System.Text.RegularExpressions;
using System.Globalization;

namespace HealthCheckAI.Helpers
{
    public static class AbnormalHelper
    {
        public static bool IsAbnormal(string result, string reference)
        {
            if (string.IsNullOrWhiteSpace(result))
                return false;

            // 1️⃣ 關鍵字判斷
            string r = result.ToLower();

            if (r.Contains("異常") ||
                r.Contains("略高") ||
                r.Contains("略低") ||
                r.Contains("偏高") ||
                r.Contains("偏低"))
                return true;

            // 正常直接排除
            if (r.Contains("正常") || r.Contains("無明顯異常"))
                return false;

            // 2️⃣ 數值 vs 參考值
            if (!string.IsNullOrWhiteSpace(reference))
            {
                var numMatch = Regex.Match(result, @"\d+(\.\d+)?");
                var rangeMatch = Regex.Match(reference, @"(\d+(\.\d+)?)\s*(至|-)\s*(\d+(\.\d+)?)");

                if (numMatch.Success && rangeMatch.Success)
                {
                    double value = double.Parse(numMatch.Value, CultureInfo.InvariantCulture);
                    double min = double.Parse(rangeMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                    double max = double.Parse(rangeMatch.Groups[4].Value, CultureInfo.InvariantCulture);

                    if (value < min || value > max)
                        return true;
                }
            }

            return false;
        }
    }
}