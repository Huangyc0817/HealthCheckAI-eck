using System;
using System.IO;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace HealthCheckAI.Services
{
    // 這兩個如果你另外有檔案，就不要重複宣告；現在先放一起沒關係
    public class HealthReportData
    {
        [LoadColumn(0)]
        public string Label { get; set; } = string.Empty;

        [LoadColumn(1)]
        public string Text { get; set; } = string.Empty;
    }

    public class HealthReportPrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedLabel { get; set; } = string.Empty;

        public float[] Score { get; set; } = Array.Empty<float>();
    }

    public class MlNetAiPredictionService : IAiPredictionService
    {
        private readonly MLContext _mlContext;
        private readonly string _modelPath;
        private PredictionEngine<HealthReportData, HealthReportPrediction>? _engine;

        public MlNetAiPredictionService()
        {
            _mlContext = new MLContext();

            _modelPath = Path.Combine(Directory.GetCurrentDirectory(),
                                      "wwwroot", "data", "health_model.zip");

            // 如果模型檔存在，就先載入；如果不存在，先不要丟錯，等 Predict 再處理
            if (File.Exists(_modelPath))
            {
                var model = _mlContext.Model.Load(_modelPath, out var _);
                _engine = _mlContext.Model.CreatePredictionEngine<HealthReportData, HealthReportPrediction>(model);
            }
        }

        public (string label, float probability) Predict(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return ("無法判定", 0f);

            // 如果模型還沒訓練（檔案不存在），給一個友善提示
            if (!File.Exists(_modelPath))
            {
                return ("模型尚未訓練，請先到「訓練 AI」頁面訓練。", 0f);
            }

            // 如果檔案有了但 _engine 還沒建立，就在這裡補載入一次
            if (_engine == null)
            {
                var model = _mlContext.Model.Load(_modelPath, out var _);
                _engine = _mlContext.Model.CreatePredictionEngine<HealthReportData, HealthReportPrediction>(model);
            }

            var input = new HealthReportData { Text = text };
            var pred = _engine.Predict(input);

            string label = pred.PredictedLabel;

            float prob = 0f;
            if (pred.Score != null && pred.Score.Length > 0)
            {
                var max = pred.Score.Max();
                var exp = pred.Score.Select(s => MathF.Exp(s - max)).ToArray();
                var sum = exp.Sum();
                prob = sum > 0 ? exp[Array.IndexOf(pred.Score, max)] / sum : 0f;
            }

            return (label, prob);
        }
    }
}
