using Microsoft.ML;
using Microsoft.ML.Data;
using System.IO;

namespace HealthCheckAI.ML
{
    public class PatientData
    {
        [LoadColumn(0)] public float Age { get; set; }
        [LoadColumn(1)] public float BMI { get; set; }
        [LoadColumn(2)] public float BloodPressure { get; set; }
        [LoadColumn(3)] public float Glucose { get; set; }
        [LoadColumn(4)] public bool Label { get; set; }
    }

    public class HealthReportData
    {
        // 第 0 欄：標籤（例如 高風險 / 中風險 / 低風險）
        [LoadColumn(0)]
        public string Label { get; set; } = string.Empty;

        // 第 1 欄：文字內容（健檢報告/診斷敘述）
        [LoadColumn(1)]
        public string Text { get; set; } = string.Empty;
    }

    public class HealthPrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedLabel { get; set; } = string.Empty;

        // 各類別的分數
        public float[] Score { get; set; } = Array.Empty<float>();
    }

    public class AIAnalysisService
    {
        private readonly MLContext _mlContext;

        public AIAnalysisService()
        {
            _mlContext = new MLContext(seed: 1);
        }

        /// <summary>
        /// 訓練模型並存成 .zip，回傳評估結果文字
        /// </summary>
        public string TrainModel(string csvPath)
        {
            if (!File.Exists(csvPath))
            {
                return $"❌ 找不到訓練資料：{csvPath}";
            }

            // 1. 載入資料
            var data = _mlContext.Data.LoadFromTextFile<HealthReportData>(
                path: csvPath,
                hasHeader: true,
                separatorChar: ',');

            // 2. 切 Training / Test
            var split = _mlContext.Data.TrainTestSplit(data, testFraction: 0.2);

            // 3. 建 pipeline：文字轉特徵 + 分類器
            var pipeline =
                _mlContext.Transforms.Conversion.MapValueToKey("Label", "Label")
                .Append(_mlContext.Transforms.Text.FeaturizeText("Features", nameof(HealthReportData.Text)))
                .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            // 4. 訓練
            var model = pipeline.Fit(split.TrainSet);

            // 5. 評估
            var predictions = model.Transform(split.TestSet);
            var metrics = _mlContext.MulticlassClassification.Evaluate(predictions);

            // 6. 存模型（放 wwwroot/data/health_model.zip）
            var modelPath = Path.Combine(Directory.GetCurrentDirectory(),
                                         "wwwroot", "data", "health_model.zip");
            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
            _mlContext.Model.Save(model, data.Schema, modelPath);

            // 7. 回傳給 View 看看效果
            string result =
                $"✅ 模型訓練完成！\n" +
                $"📁 訓練資料：{csvPath}\n" +
                $"📦 模型儲存於：{modelPath}\n\n" +
                $"🎯 MicroAccuracy: {metrics.MicroAccuracy:P2}\n" +
                $"🎯 MacroAccuracy: {metrics.MacroAccuracy:P2}\n" +
                $"LogLoss: {metrics.LogLoss:F4}\n";

            return result;
        }
    }
}
