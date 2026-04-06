using System.Text.RegularExpressions;
using HealthCheckAI.Models;

namespace HealthCheckAI.Helpers
{
    public static class EcgReportParser
    {
        public static EcgParameterResult Parse(string text)
        {
            var result = new EcgParameterResult();
            if (string.IsNullOrWhiteSpace(text))
                return result;

            text = Normalize(text);
            result.RawText = text;

            result.HeartRate = FindValue(text,
                @"(?i)(vent rate|heart rate|rate)\s*[:：]?\s*(\d+\s*BPM|\d+)");
            result.PRInterval = FindValue(text,
                @"(?i)(PR int|PR interval)\s*[:：]?\s*(\d+\s*ms|\d+)");
            result.QRSDuration = FindValue(text,
                @"(?i)(QRS dur|QRS duration)\s*[:：]?\s*(\d+\s*ms|\d+)");
            result.QT_QTc = FindValue(text,
                @"(?i)(QT\/QTc|QT QTc|QT/QTc)\s*[:：]?\s*(\d+\s*\/\s*\d+\s*ms|\d+\s*\/\s*\d+|\d+\s*ms)");
            result.Axes = FindValue(text,
                @"(?i)(P\-R\-T axes|axes)\s*[:：]?\s*([\-0-9\s\/]+)");
            result.MachineInterpretation = FindInterpretation(text);

            var judge = EcgRuleEngine.Build(result);
            result.Summary = judge.summary;
            result.Suggestion = judge.suggestion;

            return result;
        }

        private static string Normalize(string text)
        {
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            text = Regex.Replace(text, @"[ \t]+", " ");
            text = Regex.Replace(text, @"\n{2,}", "\n");
            return text.Trim();
        }

        private static string FindValue(string text, string pattern)
        {
            var m = Regex.Match(text, pattern);
            if (!m.Success) return "";
            return m.Groups[m.Groups.Count - 1].Value.Trim();
        }

        private static string FindInterpretation(string text)
        {
            string[] keys =
            {
                "normal ecg",
                "sinus rhythm",
                "sinus arrhythmia",
                "sinus bradycardia",
                "sinus tachycardia"
            };

            foreach (var key in keys)
            {
                var m = Regex.Match(text, $@"(?i)\b{Regex.Escape(key)}\b");
                if (m.Success)
                    return m.Value;
            }

            return "";
        }
    }
}