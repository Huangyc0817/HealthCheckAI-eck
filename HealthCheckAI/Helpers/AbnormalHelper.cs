using System.Text.RegularExpressions;
using System.Globalization;

namespace HealthCheckAI.Helpers
{
    public static class AbnormalHelper //判斷紅字
    {
        public static bool IsAbnormal(string result, string reference)
        {
            if (string.IsNullOrWhiteSpace(result))
                return false;

            var r = result.ToLower();

            // ✅ 先排除「正常」
            if (r.Contains("無明顯異常") ||
                r.Contains("未見異常") ||
                r.Contains("正常"))
                return false;

            // ❗ 再判斷「異常」
            if (r.Contains("異常") ||
                r.Contains("略高") ||
                r.Contains("略低") ||
                r.Contains("偏高") ||
                r.Contains("偏低"))
                return true;

            // ✅ 數值判斷
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