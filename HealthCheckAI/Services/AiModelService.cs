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
            var list = new List<string>();

            // ===========================
            // 🆕 體格檢查表（加在這裡🔥）
            if (department == "體格檢查表")
            {
                var important = new List<string>();
                var normal = new List<string>();

                // 🧠 BMI
                var bmi = ExtractValue(text, "BMI") ?? ExtractValue(text, "體質量指數");
                if (bmi != null)
                {
                    if (bmi >= 27)
                        important.Add("1. BMI 為 " + bmi + "，屬於肥胖範圍，建議積極控制體重。");
                    else if (bmi >= 24)
                        important.Add("1. BMI 為 " + bmi + "，屬於過重，建議調整飲食與運動。");
                    else
                        normal.Add("• BMI 在正常範圍內。");
                }

                // 🧠 腹圍
                var waist = ExtractValue(text, "腹圍");
                if (waist != null)
                {
                    if (waist > 90)
                        important.Add("2. 腹圍 " + waist + " 公分，已超過建議值，可能有代謝風險。");
                    else
                        normal.Add("• 腹圍在正常範圍內。");
                }

                // 🧠 血壓（特殊格式）
                var bpMatch = System.Text.RegularExpressions.Regex.Match(
                    text,
                    @"血壓.*?(\d{2,3})/(\d{2,3})"
                );

                if (bpMatch.Success)
                {
                    int sys = int.Parse(bpMatch.Groups[1].Value);
                    int dia = int.Parse(bpMatch.Groups[2].Value);

                    if (sys >= 140 || dia >= 90)
                        important.Add("3. 血壓 " + sys + "/" + dia + " mmHg，偏高，建議就醫評估。");
                    else if (sys >= 130 || dia >= 85)
                        important.Add("3. 血壓 " + sys + "/" + dia + " mmHg，偏高，建議改善生活習慣。");
                    else
                        normal.Add("• 血壓在正常範圍內。");
                }

                // 🧠 脈搏
                var pulse = ExtractValue(text, "脈搏") ?? ExtractValue(text, "Pulse");
                if (pulse != null)
                {
                    if (pulse < 60 || pulse > 100)
                        important.Add("4. 脈搏 " + pulse + " 次/分鐘，異常，建議評估心臟狀況。");
                    else
                        normal.Add("• 脈搏在正常範圍內。");
                }

                // 👉 如果沒有異常
                if (!important.Any())
                {
                    important.Add("1. 本次體格檢查大致正常，建議持續維持健康生活習慣。");
                }

                var result = new List<string>();
                result.AddRange(important);
                result.AddRange(normal);

                if (severity == "高")
                {
                    result.Add("※ 本次判定為高風險，建議進一步健康管理或門診評估。");
                }

                return string.Join("\n", result);
            }
            // ===========================


            // 🔬 實驗室檢查 → 用內容判斷
            if (department == "實驗室檢查")
            {
                if (text.Contains("HbA1c") || text.Contains("糖化血色素"))
                {
                    list.Add("1. 檢查顯示糖化血色素異常，建議控制飲食、減少糖分攝取並規律運動。");
                    list.Add("2. 建議定期追蹤 HbA1c(糖尿病) 與空腹血糖，必要時諮詢醫師。");
                }

                if (text.Contains("eGFR") || text.Contains("腎"))
                {
                    list.Add("3. 腎功能指標可能偏低，建議定期追蹤腎功能並避免過量蛋白質與藥物負擔。");
                }

                if (text.Contains("尿") && text.Contains("潛血"))
                {
                    list.Add("4. 尿液檢查出現潛血，建議進一步檢查泌尿系統或定期追蹤。");
                }

                if ((text.Contains("膽固醇") || text.Contains("三酸甘油脂"))
                     && (text.Contains("偏高") || text.Contains("過高") || text.Contains("異常")))
                {
                    list.Add("5. 血脂指標異常，建議減少油脂攝取並增加運動。");
                }

                // 👉 如果完全沒抓到
                if (!list.Any())
                {
                    list.Add("1. 本次檢查大致正常，建議持續維持良好生活習慣並定期追蹤。");
                }
            }
            else
            {
                // 👉 其他科別先用原本模板
                if (!_suggestionTemplates.TryGetValue(department, out list))
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
            }

            // 🔴 高風險補警語
            if (severity == "高")
            {
                list.Add("※ 本次判定為高風險，建議儘速安排門診評估。");
            }

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
            text ??= string.Empty;

            // 先把「正常描述」扣掉，避免把「無明顯異常」誤判成異常
            int normalCount = 0;
            normalCount += CountPlainText(text, "無明顯異常");
            normalCount += CountPlainText(text, "未見明顯異常");
            normalCount += CountPlainText(text, "正常");
            normalCount += CountPlainText(text, "良好");
            normalCount += CountPlainText(text, "穩定");

            int score = 0;

            string[] highRiskWords =
            {
        "危急", "危險", "嚴重", "高度異常", "明顯異常", "陽性",
        "腫瘤", "出血", "梗塞", "中風", "心律不整", "缺血",
        "病灶", "異常波形", "需立即", "立即處理"
    };

            string[] mediumRiskWords =
            {
        "異常", "偏高", "偏低", "過高", "過低", "需追蹤",
        "建議複檢", "建議追蹤", "需門診追蹤", "接近上限", "接近下限"
    };

            string[] lowRiskWords =
            {
        "稍高", "稍低", "輕微", "邊緣", "疑似", "請注意"
    };

            string[] normalWords =
            {
        "正常", "良好", "穩定", "無明顯異常"
    };

            foreach (var word in highRiskWords)
            {
                if (text.Contains(word))
                    score += 30;
            }

            foreach (var word in mediumRiskWords)
            {
                if (word == "異常")
                {
                    int abnormalCount = CountPlainText(text, "異常");
                    int falsePositiveCount =
                        CountPlainText(text, "無明顯異常") +
                        CountPlainText(text, "未見明顯異常");

                    abnormalCount -= falsePositiveCount;
                    if (abnormalCount > 0)
                        score += abnormalCount * 15;
                }
                else
                {
                    if (text.Contains(word))
                        score += 15;
                }
            }

            foreach (var word in lowRiskWords)
            {
                if (text.Contains(word))
                    score += 5;
            }

            score -= normalCount * 12;

            if (score >= 60) return ("高風險", 0.90f);
            if (score >= 25) return ("需注意", 0.75f);
            if (score > 0) return ("較低風險", 0.60f);

            return ("資料有限，建議由醫師判讀", 0.50f);
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

        private int CountPlainText(string text, string keyword)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword))
                return 0;

            return System.Text.RegularExpressions.Regex.Matches(
                text,
                System.Text.RegularExpressions.Regex.Escape(keyword)
            ).Count;
        }

        // 🔢 抓數值（例如 HbA1c 6.3）
        private double? ExtractValue(string text, string keyword)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(keyword))
                return null;

            var match = System.Text.RegularExpressions.Regex.Match(
                text,
                keyword + @".{0,20}?(\d+(\.\d+)?)",
                System.Text.RegularExpressions.RegexOptions.Singleline
            );

            if (match.Success && double.TryParse(match.Groups[1].Value, out double value))
            {
                return value;
            }

            return null;
        }

    }
}
