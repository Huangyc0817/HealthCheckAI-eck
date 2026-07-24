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
            if (r.Contains("無明顯異常") || r.Contains("未見異常") || r.Contains("正常"))
                return false;

            // ❗ 再判斷「異常」關鍵字
            if (r.Contains("異常") || r.Contains("略高") || r.Contains("略低") || r.Contains("偏高") || r.Contains("偏低"))
                return true;

            // ✅ 數值判斷邏輯
            if (!string.IsNullOrWhiteSpace(reference))
            {
                // 1. 抓取結果的數字 (resultValue)
                var numMatch = Regex.Match(result, @"\d+(\.\d+)?");
                if (!numMatch.Success) return false;
                double value = double.Parse(numMatch.Value, CultureInfo.InvariantCulture);

                // 2. 嘗試判斷「範圍」(例如：43.8 至 59.2)
                var rangeMatch = Regex.Match(reference, @"(\d+(\.\d+)?)\s*(至|-)\s*(\d+(\.\d+)?)");
                if (rangeMatch.Success)
                {
                    double min = double.Parse(rangeMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                    double max = double.Parse(rangeMatch.Groups[4].Value, CultureInfo.InvariantCulture);
                    if (value < min || value > max) return true;
                }
                // 3. ✨ 新增：判斷「門檻值」(例如：小於 80)
                else
                {
                    var singleMatch = Regex.Match(reference, @"\d+(\.\d+)?");
                    if (singleMatch.Success)
                    {
                        double threshold = double.Parse(singleMatch.Value, CultureInfo.InvariantCulture);

                        // 如果參考值有「小於」或「<」，超過即異常
                        if (reference.Contains("小於") || reference.Contains("<"))
                        {
                            if (value > threshold) return true;
                        }
                        // 如果參考值有「大於」或「>」，低於即異常
                        else if (reference.Contains("大於") || reference.Contains(">"))
                        {
                            if (value < threshold) return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}