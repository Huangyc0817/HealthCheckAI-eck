using System;
using System.Linq;

namespace HealthCheckAI.Services
{
    public class SimpleAiService : IAiPredictionService
    {
        public (string label, float probability) Predict(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return ("無法判斷", 0f);

            // 🔎 例：如果有「嚴重」「緊急」「異常」，就判斷偏重
            var keywordsBad = new[] { "嚴重", "緊急", "異常", "病變", "出血", "高風險" };
            var keywordsNormal = new[] { "正常", "良好", "穩定" };

            int badCount = keywordsBad.Count(k => text.Contains(k));
            int okCount = keywordsNormal.Count(k => text.Contains(k));

            if (badCount > okCount)
                return ("⚠️ 高風險", 0.85f);

            if (okCount > 0)
                return ("🟢 正常", 0.75f);

            return ("🟡 需注意", 0.6f);
        }
    }
}
