using System;
using System.Text.RegularExpressions;

namespace HealthCheckAI.Helpers
{
    public static class EyeSuggestionHelper
    {
        public static string GenerateVisionSuggestion(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            text = text.Replace("（", "(").Replace("）", ")");
            text = text.Replace("\t", " ");

            var rightMatch = Regex.Match(text, @"右眼\s+([0-9.]+)");
            var leftMatch = Regex.Match(text, @"左眼\s+([0-9.]+)");

            if (!rightMatch.Success || !leftMatch.Success)
                return "";

            if (!decimal.TryParse(rightMatch.Groups[1].Value, out var rightVision))
                return "";

            if (!decimal.TryParse(leftMatch.Groups[1].Value, out var leftVision))
                return "";

            if (leftVision < rightVision)
            {
                return $"左眼視力相對較差（左眼 {leftVision}、右眼 {rightVision}），請至門診追蹤。";
            }

            if (rightVision < leftVision)
            {
                return $"右眼視力相對較差（右眼 {rightVision}、左眼 {leftVision}），請至門診追蹤。";
            }

            return $"雙眼視力接近（右眼 {rightVision}、左眼 {leftVision}），請定期至門診追蹤。";
        }
    }
}