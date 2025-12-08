using System;
using System.IO;
using Microsoft.ML;

namespace HealthCheckAI.Services
{
    // ❶ 輸入與輸出結構（要跟訓練時一致）
    public class TextInput
    {
        public string Text { get; set; } = "";
    }

    public class RiskPrediction
    {
        // 二元分類標準輸出
        public bool PredictedLabel { get; set; }
        public float Probability { get; set; }
        public float Score { get; set; }
    }

    // ❷ 服務：載入模型並提供預測
    public class AiModelService
    {
        private readonly object _lock = new();
        private readonly string _modelPath;
        private ITransformer? _cachedModel;
        private MLContext? _ml;

        public AiModelService(string modelPath)
        {
            _modelPath = modelPath;
        }

        private void EnsureModelLoaded()
        {
            if (_cachedModel != null) return;

            lock (_lock)
            {
                if (_cachedModel != null) return;

                _ml = new MLContext(seed: 1);
                if (!File.Exists(_modelPath))
                    throw new FileNotFoundException("找不到 model.zip", _modelPath);

                _cachedModel = _ml.Model.Load(_modelPath, out _);
            }
        }

        public (string Label, float Probability) Predict(string? text)
        {
            EnsureModelLoaded();
            if (_ml == null || _cachedModel == null)
                throw new InvalidOperationException("模型尚未載入");

            // PredictionEngine 不具 thread-safe，因此這裡每次建立
            using var engine = _ml.Model.CreatePredictionEngine<TextInput, RiskPrediction>(_cachedModel);
            var result = engine.Predict(new TextInput { Text = text ?? "" });

            var label = result.PredictedLabel ? "高風險 ⚠️" : "低風險 ✅";
            return (label, result.Probability);
        }

        public (string summary, string severity) Analyze(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return ("尚無可分析內容", "低");

            // --- 超級簡化的規則（先跑通流程，再換 ML.NET）---
            var severeKeywords = new[] { "出血", "梗塞", "腫瘤", "危急", "異常升高", "急診" };
            var midKeywords = new[] { "偏高", "偏低", "需追蹤", "建議複檢" };

            int score = 0;
            foreach (var k in severeKeywords) if (text.Contains(k)) score += 2;
            foreach (var k in midKeywords) if (text.Contains(k)) score += 1;

            string severity = score >= 2 ? "高" : score == 1 ? "中" : "低";

            // 很粗略的「摘要」示例：擷取前 300 字
            string summary = text.Length > 300 ? text[..300] + "..." : text;

            return (summary, severity);
        }
    }
}
