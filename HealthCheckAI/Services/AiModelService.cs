using System;
using System.Collections.Generic;
using System.Linq;
using HealthCheckAI.Models;

namespace HealthCheckAI.Services
{
    /// <summary>
    /// 現在版本：純關鍵字規則的 AI（不使用 model.zip）
    /// 以後你有訓練好的文字模型，再把 ML.NET 接回來即可。
    /// </summary>
    public class AiModelService : IAiPredictionService
    {
        public AiReportResult Analyze(string patientName, string department, string text)
        {
            text ??= string.Empty;

            // 1. 先用關鍵字判斷風險標籤 + 機率
            var (label, prob) = PredictByKeyword(text);

            // 2. 轉成「高 / 中 / 低」嚴重程度
            string severity;
            if (label.Contains("高")) severity = "高";
            else if (label.Contains("需注意") || label.Contains("中")) severity = "中";
            else severity = "低";

            // 3. 做摘要（前 300 字）
            string summary = text.Length > 1000 ? text.Substring(0, 1000) + "..." : text;

            // 4. 抓一些關鍵句子當「重點整理」
            var keyLines = ExtractKeyPoints(text);
            string keyPoints = string.Join("\n", keyLines);

            // 5. 依「科別」給不同建議
            string suggestions = BuildSuggestionsByDepartment(department, text, severity);

            return new AiReportResult
            {
                Label = label,
                Probability = prob,
                Summary = summary,
                SeverityLevel = severity,
                KeyPoints = keyPoints,
                Suggestions = suggestions
            };
        }
        private string BuildSuggestionsByDepartment(string department, string text, string severity)
        {
            // 從 dictionary 找科別對應的建議
            if (!_suggestionTemplates.TryGetValue(department, out var list))
            {
                list = new List<string>
        {
            "建議維持規律作息與健康飲食。",
            "如有不適症狀請及早就醫。"
        };
            }
            else
            {
                list = new List<string>(list);
            }

            // 嚴重時補一句警語
            if (severity == "高")
            {
                list.Add("※ 本次判定為『較高需追蹤程度』，建議儘速安排門診評估。");
            }

            // 組成文字
            return string.Join("\n", list);
        }


        // ---------- 內部小工具 ----------
        private string BuildKeyPoints(string text)
        {
            var keyLines = new List<string>();
            var abnormalWords = new[] { "偏高", "偏低", "異常", "需追蹤", "建議複檢", "腫瘤", "出血" };

            foreach (var w in abnormalWords)
            {
                if (text.Contains(w))
                    keyLines.Add($"檢查結果出現「{w}」，建議持續追蹤或門診評估。");
            }

            if (!keyLines.Any())
                keyLines.Add("本次檢查未見明顯異常。");

            return string.Join("\n", keyLines);
        }

        // 🧩 風險標籤：純關鍵字版本
        private (string label, float probability) PredictByKeyword(string text)
        {
            string[] severeWords = { "腫瘤", "出血", "梗塞", "中風", "嚴重", "危急" };
            string[] warningWords = { "偏高", "偏低", "出現異常", "需追蹤", "建議複檢" };
            string[] okWords = { "正常", "良好", "穩定", "無明顯異常" };

            int s = severeWords.Count(w => text.Contains(w));
            int w = warningWords.Count(w => text.Contains(w));
            int o = okWords.Count(w => text.Contains(w));

            if (s > 0) return ("高風險", 0.9f);
            if (w > 0) return ("需注意", 0.7f);
            if (o > 0) return ("較低風險", 0.6f);

            return ("資料有限，建議由醫師判讀", 0.5f);
        }

        // 📌 重點整理：看文字裡有哪些關鍵指標
        private List<string> ExtractKeyPoints(string text)
        {
            var points = new List<string>();

            if (text.Contains("BMI") || text.Contains("體重"))
                points.Add("本次檢查提到 BMI / 體重，建議搭配腰圍與體脂一起評估肥胖風險。");

            if (text.Contains("血壓") || text.Contains("高血壓"))
                points.Add("報告中有血壓相關描述，請留意是否有高血壓或低血壓風險。");

            if (text.Contains("血糖") || text.Contains("糖尿病"))
                points.Add("報告中有血糖／糖尿病相關描述，建議定期追蹤空腹血糖與糖化血色素。");

            if (text.Contains("膽固醇") || text.Contains("三酸甘油脂") || text.Contains("高血脂"))
                points.Add("血脂相關指標偏高時，需調整飲食並增加規律運動，必要時依醫囑用藥。");

            if (!points.Any())
                points.Add("目前依檢查內容未見明顯重大異常，建議按時追蹤後續檢查與門診評估。");

            return points;
        }

        // 🩺 依科別給不同建議（你之後可以自己一直加）
        // 放在 AiModelService 類別裡（欄位區）
        // 先定義一份科別 ➜ 建議清單的模板
        private static readonly Dictionary<string, List<string>> _suggestionTemplates =
            new Dictionary<string, List<string>>
            {
                ["系統體格檢查表"] = new List<string>
            {
        "1. 建議維持正常的作息與適度運動，避免久坐不動。",
        "2. 若體重、腰圍或血壓有接近臨界值，建議定期量測並追蹤變化。"
            },

                ["理學檢查"] = new List<string>
            {
        "1. 目前理學檢查結果如有輕微異常，建議依照醫師建議安排後續追蹤。",
        "2. 若日常生活中出現疼痛、活動受限或其他不適，請及早就醫評估。"
            },

                ["眼科檢查"] = new List<string>
            {
        "1. 建議減少長時間近距離用眼，每 30 分鐘休息 3～5 分鐘。",
        "2. 若出現視力模糊、飛蚊症、閃光感或視野缺損，請儘早就診檢查眼底。"
            },

                ["靜態心電圖"] = new List<string>
            {
        "1. 若心電圖提示心律不整或缺血變化，請依醫師指示安排心臟相關檢查。",
        "2. 建議控制三高（血壓、血糖、血脂），避免抽菸並減少高油高鹽飲食。"
            },

                ["實驗室檢查"] = new List<string>
            {
        "1. 若血脂、血糖或肝腎功能接近或超過正常範圍，建議調整飲食與生活作息。",
        "2. 依照醫師建議於適當時間重複抽血檢查，追蹤指標變化。"
            },

                ["精密儀器檢查"] = new List<string>
            {
        "1. 若影像或儀器檢查有疑似病灶，請依醫師安排進一步檢查或門診追蹤。",
        "2. 即使結果大致正常，若持續有不適症狀，仍建議回診討論。"
            }
            };


    }
}
