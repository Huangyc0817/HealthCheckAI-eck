using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HealthCheckAI.Helpers
{
    public static class EyeReportFormatter
    {
        public static string Format(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // 1. 統一基本格式
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            text = text.Replace("（", "(").Replace("）", ")");
            text = text.Replace("\t", " ");

            // 去掉多餘空白
            text = Regex.Replace(text, @"[ ]{2,}", " ");
            text = Regex.Replace(text, @"\n{2,}", "\n");
            text = text.Trim();

            // 2. 先修一些常見被拆開的中英文標題
            text = Regex.Replace(text, @"視力\s*\(\s*Visual\s*\n\s*acuity\s*\)", "視力(Visual acuity)");
            text = Regex.Replace(text, @"眼壓\s*\(\s*Intraocular\s*\n\s*pressure\s*\)", "眼壓(Intraocular pressure)");
            text = Regex.Replace(text, @"診斷\s*[（(]\s*Diagnosis\s*[）)]", "診斷(Diagnosis)");
            text = Regex.Replace(text, @"建議\s*[（(]\s*Suggestion\s*[）)]", "建議(Suggestion)");

            // 3. 直接把整份文字壓成一行
            var all = string.Join(" ", text
                .Split('\n')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x)));

            all = Regex.Replace(all, @"\s+", " ").Trim();

            // AI 常把中英文名稱拆開，先補回來
            all = Regex.Replace(all, @"視力\s*\(\s*Visual\s*acuity\s*\)", "視力(Visual acuity)");
            all = Regex.Replace(all, @"眼壓\s*\(\s*Intraocular\s*pressure\s*\)", "眼壓(Intraocular pressure)");
            all = Regex.Replace(all, @"眼瞼\s*\(\s*Eyelid\s*\)", "眼瞼(Eyelid)");
            all = Regex.Replace(all, @"結膜\s*\(\s*Conjunctiva\s*\)", "結膜(Conjunctiva)");
            all = Regex.Replace(all, @"角膜\s*\(\s*Cornea\s*\)", "角膜(Cornea)");
            all = Regex.Replace(all, @"瞳孔\s*\(\s*Pupil\s*\)", "瞳孔(Pupil)");
            all = Regex.Replace(all, @"晶狀體\s*\(\s*Lens\s*\)", "晶狀體(Lens)");
            all = Regex.Replace(all, @"眼球肌\s*\(\s*Extraocular muscles\s*\)", "眼球肌(Extraocular muscles)");
            all = Regex.Replace(all, @"眼底\s*\(\s*Fundus\s*\)", "眼底(Fundus)");
            all = Regex.Replace(all, @"視網膜病變\s*\(\s*Retinopathy\s*\)", "視網膜病變(Retinopathy)");
            all = Regex.Replace(all, @"玻璃體\s*\(\s*Vitreous body\s*\)", "玻璃體(Vitreous body)");
            all = Regex.Replace(all, @"淚腺\s*\(\s*Lacrimal system\s*\)", "淚腺(Lacrimal system)");
            all = Regex.Replace(all, @"黃斑\s*\(\s*Macula\s*\)", "黃斑(Macula)");
            all = Regex.Replace(all, @"虹膜\s*\(\s*Uvea\s*\)", "虹膜(Uvea)");
            all = Regex.Replace(all, @"其他\s*\(\s*Others\s*\)", "其他(Others)");
            all = Regex.Replace(all, @"診斷\s*\(\s*Diagnosis\s*\)", "診斷(Diagnosis)");
            all = Regex.Replace(all, @"建議\s*\(\s*Suggestion\s*\)", "建議(Suggestion)");

            // 5. 主標題 / 欄位標題重新斷行
            string[] mainHeaders =
            {
                "眼科檢查",
                "視力裸視",
                "矯正視力",
                "眼壓(<21)",
                "電腦驗光",
                "散光",
                "辨色力",
                "診斷(Diagnosis)",
                "建議(Suggestion)",
                "眼底攝影報告"
            };

            foreach (var h in mainHeaders)
            {
                all = Regex.Replace(all, Regex.Escape(h), "\n" + h + "\n");
            }

            // 6. 左右眼前斷行
            all = Regex.Replace(all, @"\s*右眼\s*", "\n右眼 ");
            all = Regex.Replace(all, @"\s*左眼\s*", "\n左眼 ");

            // 7. 避免左眼資料後面直接黏到診斷
            all = Regex.Replace(all, @"(無異常)\s*(診斷\(Diagnosis\))", "$1\n$2");
            all = Regex.Replace(all, @"(-1\.00)\s*(診斷\(Diagnosis\))", "$1\n$2");
            all = Regex.Replace(all, @"(視力異常\s*\([^)]+\))\s*(眼壓\(Intraocular pressure\))", "$1\n$2");

            // 8. 診斷各欄位前斷行
            string[] diagnosisHeaders =
            {
                "視力(Visual acuity)",
                "眼壓(Intraocular pressure)",
                "眼瞼(Eyelid)",
                "結膜(Conjunctiva)",
                "角膜(Cornea)",
                "瞳孔(Pupil)",
                "晶狀體(Lens)",
                "眼球肌(Extraocular muscles)",
                "眼底(Fundus)",
                "視網膜病變(Retinopathy)",
                "玻璃體(Vitreous body)",
                "淚腺(Lacrimal system)",
                "黃斑(Macula)",
                "虹膜(Uvea)",
                "其他(Others)"
            };

            foreach (var item in diagnosisHeaders)
            {
                all = Regex.Replace(
                    all,
                    @"\s*" + Regex.Escape(item) + @"\s*[:：]\s*",
                    "\n" + item + "："
                );
            }

            // 冒號後面不要保留奇怪空白
            all = Regex.Replace(all, @"：\s*", "：");

            // 中文 + 英文括號之間補空格
            all = Regex.Replace(all, @"([^\s])\(", "$1 (");

            // 修正英文被拆
            all = Regex.Replace(all, @"Visual\s*acuity", "Visual acuity");
            all = Regex.Replace(all, @"Intraocular\s*pressure", "Intraocular pressure");

            all = Regex.Replace(
                all,
                @"視力\(Visual acuity\)：\s*近視、\s*散光\s*、\s*視力異常\s*\(\s*Myopia、Astigmatism、visual abnormal\s*\)",
                "視力(Visual acuity)：近視、散光、視力異常 (Myopia、Astigmatism、visual abnormal)"
            );

            all = Regex.Replace(
                all,
                @"眼壓\(Intraocular pressure\)：\s*正常範圍\s*\(\s*Within normal limits\s*\)",
                "眼壓(Intraocular pressure)：正常範圍 (Within normal limits)"
            );

            // 建議、眼底攝影報告分段
            all = Regex.Replace(all, @"建議\(Suggestion\)\s*", "\n建議(Suggestion)\n");
            all = Regex.Replace(all, @"\s*眼底攝影報告\s*", "\n眼底攝影報告\n");

            // 句號後斷行
            all = Regex.Replace(all, @"。", "。\n");

            // 再整理空行
            all = Regex.Replace(all, @"\n{3,}", "\n\n").Trim();

            return all;
        }
    }
}