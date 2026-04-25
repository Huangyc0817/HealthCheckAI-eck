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
     @"(?i)(vent\s*(rate|inte|int|rnte|ratee)?)\s*[:：]?\s*([0-9OIlT]{2,3})\s*(BPM|BIW)?");
            result.PRInterval = FindValue(text,
    @"(?i)(PR\s*(int|ant|interval))\s*[:：]?\s*([0-9OIlT]{2,4})\s*(ms|mn)?");
            result.QRSDuration = FindValue(text,
    @"(?i)QRS\s*dur\s*[:：]?\s*([0-9OIlTBG]{2,4})\s*(ms)?");
            result.QT_QTc = FindValue(text,
                @"(?i)(QT\s*/\s*QTc|QT\s*QTc)\s*[:：]?\s*([0-9OIlT]{2,4}\s*/\s*[0-9OIlT]{2,4})\s*(ms)?");

            result.QT_QTc = FindValue(text,
    @"(?i)QT\s*/\s*QTc\s*[:：]?\s*([0-9OIlTBG]{2,4}\s*/\s*[0-9OIlTBG]{2,4})\s*(ms)?");

            result.MachineInterpretation = FindInterpretation(text);

            FixUnits(result);

           
            // ⭐⭐⭐ 補 QT/QTc fallback（重點在這）
            if (string.IsNullOrWhiteSpace(result.QT_QTc))
            {
                var lines = text.Split('\n');

                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];

                    if (line.Contains("QRS") || line.Contains("dur"))
                    {
                        if (i + 1 < lines.Length)
                        {
                            var nextLine = lines[i + 1];

                            var m = Regex.Match(nextLine, @"(\d{3})\s*[/\s]\s*(\d{3})");

                            if (m.Success)
                            {
                                result.QT_QTc = $"{m.Groups[1].Value}/{m.Groups[2].Value} ms";
                                break;
                            }
                        }
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(result.Axes))
            {
                var lines = text.Split('\n');

                foreach (var line in lines)
                {
                    if (line.ToLower().Contains("axes") || line.Contains("P") || line.Contains("R"))
                    {
                        var m = Regex.Match(line, @"(\d{2,3})\s+(\d{2,3})\s+(\d{2,3})");

                        if (m.Success)
                        {
                            result.Axes = $"{m.Groups[1].Value} {m.Groups[2].Value} {m.Groups[3].Value}";
                            break;
                        }
                    }
                }
            }
            var judge = EcgRuleEngine.Build(result);
            result.Summary = judge.summary;
            result.Suggestion = judge.suggestion;

            return result;
        }

        private static string Normalize(string text)
        {
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

            // OCR 常見錯字修正
            text = text.Replace("Ｑ", "Q")
                       .Replace("Ｔ", "T")
                       .Replace("／", "/")
                       .Replace("：", ":");

            text = Regex.Replace(text, @"[ \t]+", " ");
            text = Regex.Replace(text, @"\n{2,}", "\n");

            return text.Trim();
        }

        private static string FindValue(string text, string pattern)
        {
            var m = Regex.Match(text, pattern);

            if (!m.Success)
                return "";

            // 👉 永遠抓「數字 group」
            for (int i = 1; i < m.Groups.Count; i++)
            {
                var value = m.Groups[i].Value.Trim();

                if (Regex.IsMatch(value, @"[0-9OIlTBG]"))
                    return FixOcrNumber(value);
            }

            return "";
        }

        private static string FixOcrNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            return value
                .Replace("O", "0")
                .Replace("Hi", "BPM")
.Replace("omy", "ms")
.Replace("oan", "int")
.Replace("rato", "rate")
                .Replace("o", "0")
                .Replace("I", "1")
                .Replace("l", "1")
                .Replace("T", "7")
                .Replace("B", "8")   // BG → 86
                .Replace("G", "6")
                .Replace("BIW", "BPM")
                .Replace("mn", "ms")
                .Replace("W", "M")   // BIW → BPM
                .Trim();
        }

        private static void FixUnits(EcgParameterResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.HeartRate) &&
                !result.HeartRate.Contains("BPM", StringComparison.OrdinalIgnoreCase))
            {
                result.HeartRate += " BPM";
            }

            if (!string.IsNullOrWhiteSpace(result.PRInterval) &&
                !result.PRInterval.Contains("ms", StringComparison.OrdinalIgnoreCase))
            {
                result.PRInterval += " ms";
            }

            if (!string.IsNullOrWhiteSpace(result.QRSDuration) &&
                !result.QRSDuration.Contains("ms", StringComparison.OrdinalIgnoreCase))
            {
                result.QRSDuration += " ms";
            }

            if (!string.IsNullOrWhiteSpace(result.QT_QTc) &&
                !result.QT_QTc.Contains("ms", StringComparison.OrdinalIgnoreCase))
            {
                result.QT_QTc += " ms";
            }
        }

        private static string FindInterpretation(string text)
        {
            string[] keys =
            {
                "normal ecg",
                "normal sinus rhythm",
                "sinus rhythm",
                "sanus rhythm",
                "sinus arrhythmia",
                "sinus bradycardia",
                "sinus tachycardia",
                "atrial fibrillation",
                "af",
                "premature ventricular contraction",
                "pvc",
                "st elevation",
                "st depression",
                "abnormal ecg"
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